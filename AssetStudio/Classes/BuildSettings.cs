namespace AssetStudio
{
    public sealed class BuildSettings : Object
    {
        public string[] levels;
        public string[] scenes;

        public BuildSettings(ObjectReader reader) : base(reader)
        {
            if (reader.version < (5, 1)) //5.1 down
            {
                levels = reader.ReadStringArray();
            }
            else
            {
                scenes = reader.ReadStringArray();
            }
        }
    }
}
