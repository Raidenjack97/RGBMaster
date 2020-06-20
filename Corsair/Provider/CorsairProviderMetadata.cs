﻿using Common;
using System.Drawing;

namespace Corsair.Provider
{
    public class CorsairProviderMetadata : ProviderMetadata
    {
        public override string ProviderName => "Corsair Sync";

        public override string ProviderShortDescription => "Corsair Sync provider for all corsair synced devices.";

        public override string ProviderFullDescription => ProviderShortDescription;

        public override Bitmap ProviderIcon => null;
    }
}