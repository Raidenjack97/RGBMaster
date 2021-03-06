﻿using Common;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EffectsExecution
{
    public class MusicEffectExecutor : EffectExecutor
    {
        private WasapiLoopbackCapture captureInstance = null;

        public MusicEffectExecutor() : base(new MusicEffectMetadata())
        {

        }

        protected override Task StopInternal()
        {
            captureInstance.StopRecording();

            captureInstance.Dispose();

            return Task.CompletedTask;
        }

        protected override Task StartInternal()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
            {
                
            }

            captureInstance = new WasapiLoopbackCapture();

            captureInstance.DataAvailable += (ss, ee) => OnNewSoundReceived(ss, ee);
            captureInstance.RecordingStopped += (ss, ee) => captureInstance.Dispose();

            captureInstance.StartRecording();

            return Task.CompletedTask;
        }

        private void OnNewSoundReceived(object sender, NAudio.Wave.WaveInEventArgs e)
        {
            float max = 0;
            var buffer = new WaveBuffer(e.Buffer);
            // interpret as 32 bit floating point audio
            for (int index = 0; index < e.BytesRecorded / 4; index++)
            {
                float sample = buffer.FloatBuffer[index];

                // absolute value 
                //if (sample < 0) sample = -sample;
                if (sample < 0) sample = -sample;
                // is this the max value?
                if (sample > max) max = sample;
            }

            Color color = Color.Black;

            if (max > 0.01 && max <= 0.1)
            {
                max = 1;
                color = Color.Red;
            }

            if (max > 0.1 && max <= 0.2)
            {
                max = 1;
                color = Color.Orange;
            }

            else if (max > 0.2 && max <= 0.35)
            {
                max = 30;
                color = Color.Yellow;
            }

            else if (max > 0.35 && max <= 0.5)
            {
                max = 30;
                color = Color.Cyan;
            }

            else if (max > 0.5 && max <= 0.65)
            {
                max = 60;
                color = Color.Blue;
            }

            else if (max > 0.65)
            {
                max = 100;
                color = Color.Violet;
            }
            else
            {
                return;
            }

            var tasks = new List<Task>();

            foreach (var device in Devices)
            {
                if (device.DeviceMetadata.SupportedOperations.Contains(OperationType.SetBrightness))
                {
                    tasks.Add(Task.Run(() => device.SetBrightnessPercentage((byte)(max))));
                }

                if (device.DeviceMetadata.SupportedOperations.Contains(OperationType.SetColor))
                {
                    tasks.Add(Task.Run(() => device.SetColor(color)));
                }
            }

            Task.WaitAll(tasks.ToArray());
        }
    }
}
