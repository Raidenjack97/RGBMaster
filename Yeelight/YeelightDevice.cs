﻿using Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using YeelightAPI.Models;

namespace Yeelight
{
    public class YeelightDevice : Device
    {
        private readonly HashSet<OperationType> yeelightSupportedOps = new HashSet<OperationType>() { OperationType.GetBrightness, OperationType.SetBrightness, OperationType.GetColor, OperationType.SetColor };

        public override string DeviceName => InternalDevice.Name;

        public override HashSet<OperationType> SupportedOperations => yeelightSupportedOps;

        /// <summary>
        /// Serializer settings
        /// </summary>
        public static readonly JsonSerializerSettings DeviceSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly YeelightAPI.Device InternalDevice;
        private Socket musicModeSocket;

        public YeelightDevice(YeelightAPI.Device internalDevice)
        {
            InternalDevice = internalDevice;
        }

        public async override Task Connect()
        {
            await InternalDevice.Connect();
            var supportedOps = InternalDevice.SupportedOperations;

            if (!supportedOps.Contains(YeelightAPI.Models.METHODS.SetMusicMode))
            {
                throw new YeelightDeviceNotSupportedException();
            }

            var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).First();

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 0);


            using (var musicModeSocketListener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    musicModeSocketListener.Bind(localEndPoint);

                    musicModeSocketListener.Listen(1);

                    await InternalDevice.StartMusicMode(ipAddress.ToString(), ((IPEndPoint)musicModeSocketListener.LocalEndPoint).Port);

                    this.musicModeSocket = musicModeSocketListener.Accept();
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to bind the socket to the specified ip and port.", ex);
                }
            }
        }

        public override Task Disconnect()
        {
            musicModeSocket.Close();
            InternalDevice.Disconnect();
            return Task.CompletedTask;
        }

        public override byte GetBrightnessPercentage()
        {
            // TODO - Also implement background lighting???
            // TODO2 - Keep the last known brightness at all time in a private member? is it a sensible approach?            
            var task = InternalDevice.GetProp(YeelightAPI.Models.PROPERTIES.bright);
            task.Wait();
            return (byte)task.Result;
        }

        public override Color GetColor()
        {
            var task = InternalDevice.GetProp(YeelightAPI.Models.PROPERTIES.rgb);
            task.Wait();
            return RGBColorHelper.ParseColor((int)task.Result);
        }

        public override void SetBrightnessPercentage(byte brightness)
        {
            var serverParams = new List<object>() { brightness };

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

            musicModeSocket.Send(sentData);
        }

        public override void SetColor(Color color)
        {
            var colorValue = RGBColorHelper.ComputeRGBColor(color.R, color.G, color.B);

            Command colorCommand = new Command()
            {
                Id = 1,
                Method = "set_rgb",
                Params = new List<object>() { colorValue, "smooth", 30 }
            };

            string colorData = JsonConvert.SerializeObject(colorCommand, DeviceSerializerSettings);
            byte[] colorSentData = Encoding.ASCII.GetBytes(colorData + "\r\n"); // \r\n is the end of the message, it needs to be sent for the message to be read by the device

            musicModeSocket.Send(colorSentData);
        }
    }
}
