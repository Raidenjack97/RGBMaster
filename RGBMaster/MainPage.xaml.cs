﻿using Infrastructure;
using Logitech;
using MagicHome;
using RazerChroma;
using RGBMaster.Pages;
using RGBMaster.State;
using RGBMaster.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Yeelight;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RGBMaster
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // List of ValueTuple holding the Navigation Tag and the relative Navigation Page
        private readonly Dictionary<string, Type> pageToType = new Dictionary<string, Type>()
        {
            { "DevicesPage", typeof(DevicesPage) },
            { "EffectsPage", typeof(EffectsPage) }
        };

        private readonly IEnumerable<Provider> SupportedProviders = new List<Provider>()
        {
            new YeelightProvider(), new MagicHomeProvider(), /*new RazerChromaProvider(),*/ new LogitechProvider()
        };

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            //await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () => { await SeekAndRediscoverDevices(); });
            await SeekAndRediscoverDevices();
            RegisterToSelectionChangesInDevices();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void RegisterToSelectionChangesInDevices()
        {
            AppState.Instance.SelectedDevices.CollectionChanged += async (sender, e) =>
            {
                if (AppState.Instance.IsEffectRunning && AppState.Instance.SelectedEffect != null)
                {
                    await AppState.Instance.SelectedEffect?.ChangeConnectedDevices(AppState.Instance.SelectedDevices);
                }
            };
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async Task SeekAndRediscoverDevices()
        {
            var registrationAndDiscoveryTasks = new List<Task<RegisteredProvider>>();

            foreach (var provider in SupportedProviders)
            {
                registrationAndDiscoveryTasks.Add(provider.InitializeProvider().ContinueWith(async ct =>
                {
                    if (provider.IsRegistered)
                    {
                        var discoveredDevices = await provider.Discover();

                        return new RegisteredProvider()
                        {
                            Provider = provider,
                            Devices = new ObservableCollection<DiscoveredDevice>(discoveredDevices.Select(discoveredDevice => new DiscoveredDevice() { Device = discoveredDevice, IsChecked = false }).ToList())
                        };
                    }

                    return null;
                }).Unwrap());
            }

            await Task.WhenAll(registrationAndDiscoveryTasks);

            AppState.Instance.RegisteredProviders.Clear();

            foreach (var task in registrationAndDiscoveryTasks)
            {
                if (task.Result != null)
                {
                    AppState.Instance.RegisteredProviders.Add(task.Result);
                }
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectionTag = (string)args.SelectedItemContainer.Tag;

            var navigationResult = MainAppContentFrame.Navigate(pageToType[selectionTag], null, args.RecommendedNavigationTransitionInfo);
        }

        private void MainAppContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {

        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            var oldInnerButton = FindName("InnerButton") as FontIcon;
            var oldOuterButton = FindName("OuterButton") as FontIcon;

            AppBarStartStopGrid.Children.Remove(oldInnerButton);
            AppBarStartStopGrid.Children.Remove(oldOuterButton);

            UIElement innerButton;
            UIElement outerButton;

            if (AppState.Instance.IsEffectRunning)
            {
                await AppState.Instance.SelectedEffect?.Stop();

                AppState.Instance.IsEffectRunning = false;

                outerButton = new FontIcon() { Glyph = "\uE739", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Name = "OuterButton" };
                innerButton = new FontIcon() { Glyph = "\uE73B", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Foreground = new SolidColorBrush(Colors.Red), Name = "InnerButton" };

                AppBarButton button = (AppBarButton)sender;

                outerButton = new FontIcon() { Glyph = "\uE768", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Name = "OuterButton" };
                innerButton = new FontIcon() { Glyph = "\uF5B0", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Foreground = new SolidColorBrush(Colors.Green), Name = "InnerButton" };

                button.Label = "Start";
            }
            else
            {
                await AppState.Instance.SelectedEffect.ChangeConnectedDevices(AppState.Instance.SelectedDevices);
                await AppState.Instance.SelectedEffect.Start();

                AppState.Instance.IsEffectRunning = true;

                AppBarButton button = (AppBarButton)sender;

                outerButton = new FontIcon() { Glyph = "\uE739", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Name = "OuterButton" };
                innerButton = new FontIcon() { Glyph = "\uE73B", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Foreground = new SolidColorBrush(Colors.Red), Name = "InnerButton" };

                button.Label = "Stop";
            }

            AppBarStartStopGrid.Children.Add(innerButton);
            AppBarStartStopGrid.Children.Add(outerButton);
        }
    }
}
