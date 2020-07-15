﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public abstract class EffectMetadata
    {
        public Guid EffectMetadataGuid { get; private set; }

        public abstract EffectType Type { get; }
        public abstract string EffectName { get; }
        public abstract string ShortDescription { get; }
        public abstract string FullDescription { get; }
        public abstract string IconGlyph { get; }

        public EffectMetadata()
        {
            EffectMetadataGuid = Guid.NewGuid();
        }
    }

    public abstract class EffectMetadata<Props> : EffectMetadata
    {
        public Props EffectProperties { get; set; }

        public void UpdateProps(Props newProps)
        {
            EffectProperties = newProps;
        }
    }
}
