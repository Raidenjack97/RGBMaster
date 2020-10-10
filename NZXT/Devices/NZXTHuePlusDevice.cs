﻿using NZXTSharp;
using NZXTSharp.HuePlus;
using Provider;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;

namespace NZXT
{
    public class NZXTHuePlusDevice : Device
    {
        private readonly HuePlus internalDevice;
        public NZXTHuePlusDevice(Guid discoveredProviderGuid, HuePlus internalDevice) : base(new NZXTDeviceMetadata(discoveredProviderGuid, internalDevice.Name))
        {
            this.internalDevice = internalDevice;
        }
        protected override Task ConnectInternal()
        {
            return Task.CompletedTask;
        }

        protected override Task DisconnectInternal()
        {
            return Task.CompletedTask;
        }

        protected override byte GetBrightnessPercentageInternal()
        {
            throw new NotImplementedException();
        }

        protected override Color GetColorInternal()
        {
            throw new NotImplementedException();
        }

        protected override void SetBrightnessPercentageInternal(byte brightness)
        {
            throw new NotImplementedException();
        }

        protected override void SetColorInternal(Color color)
        {
            Fixed nzxtColor = new Fixed(new NZXTSharp.Color(color.R, color.G, color.B));
            internalDevice.ApplyEffect(nzxtColor);
        }

        protected override void TurnOffInternal()
        {
            throw new NotImplementedException();
        }

        protected override void TurnOnInternal()
        {
            throw new NotImplementedException();
        }
    }
}
