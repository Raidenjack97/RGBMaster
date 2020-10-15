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

                if (sample < 0)
                {
                    sample = -sample;
                }

                if (sample > max)
                {
                    max = sample;
                }
            }

            Color color = Color.Black;

            var musicEffectProperties = ((MusicEffectMetadata)executedEffectMetadata).EffectProperties;

            double maxAudioPoint = max * 100;
            byte desiredBrightnessPercentage = 0;

            // We scan the audio points of the effect properties (assuming they are kept ordered in our state, which
            // is probably a bad thing, we'll think about it later). The first audio point which minimum is surpassed by the maximum
            // level of played audio will represent the desired brightness and color of the sound.
            for (int i = musicEffectProperties.AudioPoints.Count - 1; i >= 0; i--)
            {
                var audioPoint = musicEffectProperties.AudioPoints[i];
                if (maxAudioPoint >= audioPoint.MinimumAudioPoint)
                {
                    desiredBrightnessPercentage = (byte)audioPoint.MinimumAudioPoint;
                    color = audioPoint.Color;
                    break;
                }
            }

            var tasks = new List<Task>();

            foreach (var device in Devices)
            {
                if (device.DeviceMetadata.SupportedOperations.Contains(OperationType.SetBrightness))
                {
                    tasks.Add(Task.Run(() => device.SetBrightnessPercentage(desiredBrightnessPercentage)));
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
