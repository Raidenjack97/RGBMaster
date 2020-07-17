﻿using AppExecutionManager.EventManagement;
using AppExecutionManager.State;
using Common;
using EffectsExecution;
using Logitech;
using MagicHome;
using Provider;
using RazerChroma;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Yeelight;

namespace RGBMasterWPFRunner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private SemaphoreSlim changeConnectedDevicesSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim initializeProvidersSemaphore = new SemaphoreSlim(1, 1);

        private readonly Dictionary<Guid, Provider.BaseProvider> supportedProviders = new Dictionary<Guid, Provider.BaseProvider>();
        private readonly Dictionary<Guid, EffectExecutor> supportedEffectsExecutors = new Dictionary<Guid, EffectExecutor>();
        private readonly Dictionary<Guid, Device> concreteDevices = new Dictionary<Guid, Device>();

        public MainWindow()
        {
            InitializeComponent();

            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            AppState.Instance.AppVersion = string.Format($"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}");

            // TODO - For fuck sake, check why the fuck it is impossible to write to AppData via Serilog when the user's name contains spaces?!
            // var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @$"{AppState.Instance.AppVersion}.txt");

            var path = Path.Combine(
                    Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)),
                    @$"RGBMaster\Logs\{AppState.Instance.AppVersion}.txt"
                );

            var globalLog = new LoggerConfiguration()
                .MinimumLevel
                .Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo
                .File(
                    path,
                    fileSizeLimitBytes: 2 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 5
                ).CreateLogger();

            // Note - this line makes our "global log" the root log of the app.
            // meaning - when a library writes to log via microsoft's diagnostic it is sinked to this globalLog object.
            // We may want to change this behaviour in the subject as we grow. :)
            Log.Logger = globalLog;

            globalLog.Information("Initializing RGBMaster.....");

            CreateAndSetSupportedProviders(new List<BaseProvider>() { new YeelightProvider(), new MagicHomeProvider(), new RazerChromaProvider(), new LogitechProvider() });

            CreateAndSetSupportedEffectsExecutors(new List<EffectExecutor>() { new MusicEffectExecutor(), new DominantDisplayColorEffectExecutor(), new StaticColorEffectExecutor() });
            SetUIStateEffects();

            EventManager.Instance.SubscribeToEffectChanged(Instance_EffectChanged);
            EventManager.Instance.SubscribeToSelectedDevicesChanged(Instance_SelectedDevicesChanged);
            EventManager.Instance.SubscribeToInitializeProvidersRequests(InitializeProviders);
            EventManager.Instance.SubscribeToStartSyncingRequested(StartSyncing);
            EventManager.Instance.SubscribeToStopSyncingRequested(StopSyncing);
            EventManager.Instance.SubscribeToStaticColorChanges(ChangeStaticColor);
            EventManager.Instance.SubscribeToTurnOnAllLightsRequests(TurnOnAllLights);
        }

        private async void TurnOnAllLights(object sender, EventArgs e)
        {
            var tasks = new List<Task>();

            foreach (var provider in AppState.Instance.RegisteredProviders)
            {
                foreach (var device in provider.Devices)
                {
                    if (device.IsChecked)
                    {
                        var deviceGuid = device.Device.DeviceGuid;
                        tasks.Add(Task.Run(() => concreteDevices[deviceGuid].TurnOn()));
                    }
                }
            }

            await Task.WhenAll(tasks);

        }

        private async void StopSyncing(object sender, EventArgs e)
        {
            AppState.Instance.IsEffectRunning = false;

            await supportedEffectsExecutors[AppState.Instance.SelectedEffect.EffectMetadataGuid].Stop();
        }

        private async void ChangeStaticColor(object sender, StaticColorEffectProps staticColorEffectProps)
        {
            AppState.Instance.StaticColorEffectProperties = staticColorEffectProps;

            var selectedEffectExecutor = supportedEffectsExecutors[AppState.Instance.SelectedEffect.EffectMetadataGuid];

            if (AppState.Instance.IsEffectRunning && selectedEffectExecutor.GetType() == typeof(StaticColorEffectExecutor))
            {
                ((StaticColorEffectMetadata)selectedEffectExecutor.executedEffectMetadata).UpdateProps(staticColorEffectProps);

                await selectedEffectExecutor.Start();
            }
        }

        private async void StartSyncing(object sender, EventArgs e)
        {
            AppState.Instance.IsEffectRunning = true;

            await supportedEffectsExecutors[AppState.Instance.SelectedEffect.EffectMetadataGuid].Start();
        }

        private void SetUIStateEffects()
        {
            AppState.Instance.Effects.Clear();

            foreach (var supportedEffectExecutor in supportedEffectsExecutors.Values)
            {
                AppState.Instance.Effects.Add(supportedEffectExecutor.executedEffectMetadata);
            }

            AppState.Instance.SelectedEffect = AppState.Instance.Effects.FirstOrDefault();
        }

        private void CreateAndSetSupportedEffectsExecutors(IEnumerable<EffectExecutor> effectExecutors)
        {
            foreach (var effectExecutor in effectExecutors)
            {
                supportedEffectsExecutors.Add(effectExecutor.executedEffectMetadata.EffectMetadataGuid, effectExecutor);
            }
        }

        private void CreateAndSetSupportedProviders(IEnumerable<BaseProvider> providers)
        {
            foreach (var provider in providers)
            {
                supportedProviders[provider.ProviderMetadata.ProviderGuid] = provider;

                AppState.Instance.SupportedProviders.Add(provider.ProviderMetadata);
            }
        }

        private async void InitializeProviders(object sender, EventArgs e)
        {
            await initializeProvidersSemaphore.WaitAsync();
            concreteDevices.Clear();
            AppState.Instance.RegisteredProviders.Clear();
            //AppState.Instance.SelectedDevices.Clear();

            var tasks = new List<Task>();

            var discoveredDevices = new List<Device>();

            foreach (var provider in supportedProviders.Values)
            {
                var didInitialize = await provider.InitializeProvider();

                if (!didInitialize)
                {
                    continue;
                }

                var devices = await provider.Discover();

                discoveredDevices.AddRange(devices);

                if ((devices?.Count).GetValueOrDefault() > 0)
                {
                    AppState.Instance.RegisteredProviders.Add(new RegisteredProvider()
                    {
                        Provider = provider.ProviderMetadata,
                        Devices = new System.Collections.ObjectModel.ObservableCollection<DiscoveredDevice>(devices.Select(device => new DiscoveredDevice() { Device = device.DeviceMetadata, IsChecked = false }))
                    });
                }
            }

            foreach (var device in discoveredDevices)
            {
                concreteDevices.Add(device.DeviceMetadata.DeviceGuid, device);
            }

            initializeProvidersSemaphore.Release();
        }


        private async void Instance_EffectChanged(object sender, EffectMetadata e)
        {
            var newEffectExecutor = supportedEffectsExecutors[e.EffectMetadataGuid];
            newEffectExecutor.ChangeConnectedDevices(AppState.Instance.RegisteredProviders.Select(provider => provider.Devices).SelectMany(devices => devices).Where(device => device.IsChecked).Select(dev => this.concreteDevices[dev.Device.DeviceGuid]));

            if (AppState.Instance.IsEffectRunning)
            {
                if (e.EffectMetadataGuid != AppState.Instance.SelectedEffect.EffectMetadataGuid)
                {
                    await supportedEffectsExecutors[AppState.Instance.SelectedEffect.EffectMetadataGuid].Stop();
                    await supportedEffectsExecutors[e.EffectMetadataGuid].Start();
                }
            }

            AppState.Instance.SelectedEffect = e;
        }

        private async void Instance_SelectedDevicesChanged(object sender, List<DiscoveredDevice> devices)
        {
            var newSelectedDevices = devices;

            await changeConnectedDevicesSemaphore.WaitAsync();

            if (newSelectedDevices == null)
            {
                return;
            }

            foreach (var item in newSelectedDevices)
            {
                var concreteDevice = concreteDevices[item.Device.DeviceGuid];

                if (!item.IsChecked && concreteDevice.IsConnected)
                {
                    concreteDevice.TurnOff();
                    await concreteDevice.Disconnect();
                }
                else if (item.IsChecked && !concreteDevice.IsConnected)
                {
                    await concreteDevice.Connect();
                    concreteDevice.TurnOn();
                }
            }

            supportedEffectsExecutors[AppState.Instance.SelectedEffect.EffectMetadataGuid].ChangeConnectedDevices(newSelectedDevices.Where(device => device.IsChecked).Select(dev => this.concreteDevices[dev.Device.DeviceGuid]));

            changeConnectedDevicesSemaphore.Release();
        }
    }
}
