﻿using Colore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Colore.Effects.Keyboard;
using YeelightAPI;
using ColoreColor = Colore.Data.Color;
using Colore.Data;
using System.Timers;
using System.Net;
using System.Net.Sockets;
using System.Dynamic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YeelightAPI.Models;
using System.Drawing;
using NAudio.Wave;

namespace chroma_yeelight
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int count1 = 0;
        private int count2 = 0;
        private int count3 = 0;
        private int count4 = 0;

        /// <summary>
        /// Serializer settings
        /// </summary>
        public static readonly JsonSerializerSettings DeviceSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };


        public MainWindow()
        {
            InitializeComponent();
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<Device, Socket> deviceToSocket = new Dictionary<Device, Socket>();

            var currDevices = await DeviceLocator.Discover();

            if (!currDevices.Any())
            {
                throw new NoDevicesFoundException();
            }

            var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            currDevices.ForEach(async dev =>
            {
                await dev.Connect();

                var supportedOps = dev.SupportedOperations;

                if (!supportedOps.Contains(YeelightAPI.Models.METHODS.SetMusicMode))
                {
                    throw new YeelightDeviceNotSupportedException();
                }
            });

            using (Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    listener.Bind(localEndPoint);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to bind the socket to the specified ip and port.", ex);
                }

                listener.Listen(int.MaxValue);

                foreach (var device in currDevices)
                {
                    try
                    {
                        var setMusicResult = await device.StartMusicMode(ipAddress.ToString(), 11000);
                        if (!setMusicResult)
                        {
                            throw new DeviceCommandFailedException();
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("An unexpected error has occured. Please check your connection and try to reset the bulbs if the issue persists.", ex);
                    }
                }

                currDevices.ForEach(dev => deviceToSocket[dev] = listener.Accept());

                var chroma = await ColoreProvider.CreateNativeAsync();
                
                var captureInstance = SoundHelper.GetCaptureInstance();

                captureInstance.DataAvailable += (ss, ee) => this.OnNewSoundReceived(ss, ee, currDevices, deviceToSocket, chroma);
                captureInstance.RecordingStopped += (ss, ee) => captureInstance.Dispose();

                try
                {
                    captureInstance.StartRecording();
                }
                catch (Exception ex)
                {
                    throw new AudioCaptureAccessDeniedException("Hello! Yes! Yes, Eliran Sabag. Make sure there are no background softwares running in your pc that are capturing background activity! (including MOBO software, Realtek HD, Asus Sonic etc.)", ex);
                }
            }
        }

        private async void OnNewSoundReceived(object sender, NAudio.Wave.WaveInEventArgs e, List<Device> currDevices, Dictionary<Device, Socket> deviceToSocketsMap, IChroma chroma)
        {
            float max = 0;
            float sample = 0;

            var buffer = new WaveBuffer(e.Buffer);
            // interpret as 32 bit floating point audio
            for (int index = 0; index < e.BytesRecorded / 4; index++)
            {
                sample = buffer.FloatBuffer[index];

                // absolute value 
                //if (sample < 0) sample = -sample;
                if (sample < 0) sample = -sample;
                // is this the max value?
                if (sample > max) max = sample;
            }

            ColoreColor color = ColoreColor.Black;
            // ColorHelper.ComputeRGBColor(ColoreColor.Purple.R, ColoreColor.Purple.G, ColoreColor.Purple.B)

            if (max > 0.1 && max <= 0.3)
            {
                max = 0.01f;
                count1++;
                color = ColoreColor.Red;
            }

            else if (max > 0.3 && max <= 0.50)
            {
                max = 0.3f;
                count2++;
                color = ColoreColor.Orange;
            }

            else if (max > 0.50 && max <= 0.65)
            {
                max = 0.6f;
                count3++;
                color = ColoreColor.Yellow;
            }

            else if (max > 0.65)
            {
                max = 1f;
                count4++;
                color = new ColoreColor(0, 255, 255);
            }


            await chroma.SetAllAsync(color);
            var colorValue = ColorHelper.ComputeRGBColor(color.R, color.G, color.B);

            var serverParams = new List<object>() { max > 0 ? 100 * max : 1 };

            // We create 2 commands that opposite each other.
            // When one is extremely bright, the other one is extremely dark.
            Command brightnessCommand = new Command()
            {
                Id = 1,
                Method = "set_bright",
                Params = serverParams
            };

            string data = JsonConvert.SerializeObject(brightnessCommand, DeviceSerializerSettings);
            byte[] sentData = Encoding.ASCII.GetBytes(data + "\r\n"); // \r\n is the end of the message, it needs to be sent for the message to be read by the device

            Command colorCommand = new Command()
            {
                Id = 1,
                Method = "set_rgb",
                Params = new List<object>() { colorValue, "smooth", 100 }
            };

            string colorData = JsonConvert.SerializeObject(colorCommand, DeviceSerializerSettings);
            byte[] colorSentData = Encoding.ASCII.GetBytes(colorData + "\r\n"); // \r\n is the end of the message, it needs to be sent for the message to be read by the device

            for (int i = 0; i < currDevices.Count; i++)
            {
                deviceToSocketsMap[currDevices[i]].Send(sentData);
                deviceToSocketsMap[currDevices[i]].Send(colorSentData);
            }
        }
    }
}
