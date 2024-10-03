namespace AssetStudio
{
    public sealed class PlayerSettings : Object
    {
        public string companyName;
        public string productName;

        public PlayerSettings(ObjectReader reader) : base(reader)
        {
            if (version >= (3, 0))
            {
                if (version >= (5, 4)) //5.4.0 and up
                {
                    var productGUID = reader.ReadBytes(16);
                }

                var AndroidProfiler = reader.ReadBoolean();
                //bool AndroidFilterTouchesWhenObscured 2017.2 and up
                //bool AndroidEnableSustainedPerformanceMode 2018 and up
                reader.AlignStream();
                int defaultScreenOrientation = reader.ReadInt32();
                int targetDevice = reader.ReadInt32();
                if (version < (5, 3)) //5.3 down
                {
                    if (version < 5) //5.0 down
                    {
                        int targetPlatform = reader.ReadInt32(); //4.0 and up targetGlesGraphics
                        if (version >= (4, 6)) //4.6 and up
                        {
                            var targetIOSGraphics = reader.ReadInt32();
                        }
                    }
                    int targetResolution = reader.ReadInt32();
                }
                else
                {
                    var useOnDemandResources = reader.ReadBoolean();
                    reader.AlignStream();
                }
                if (version >= (3, 5)) //3.5 and up
                {
                    var accelerometerFrequency = reader.ReadInt32();
                }
            }
            companyName = reader.ReadAlignedString();
            productName = reader.ReadAlignedString();
        }
    }
}
