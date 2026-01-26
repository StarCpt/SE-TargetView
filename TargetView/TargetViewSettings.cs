using System.IO;
using System.Xml;
using System.Xml.Serialization;
using VRage.FileSystem;
using VRage.Input;
using VRageMath;

namespace TargetView
{
    public class TargetViewSettings
    {
        private const string fileName = "TargetViewSettings.xml";
        private static string FilePath => Path.Combine(MyFileSystem.UserDataPath, "Storage", fileName);

        protected TargetViewSettings()
        {
        }

        public bool Enabled { get; set; } = true;
        public int Ratio { get; set; } = 1;
        public bool HeadFix { get; set; } = true;
        public bool OcclusionFix { get; set; } = true;

        public int MinDistance { get; set; } = 100;

        public Vector2I Position { get; set; } = new Vector2I(0, 0);
        public Vector2I Size { get; set; } = new Vector2I(500, 500);
        public MyKeys ZoomKey { get; set; } = MyKeys.M;

        public static TargetViewSettings Load()
        {
            string file = FilePath;
            if (File.Exists(file))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(TargetViewSettings));
                    using (XmlReader xml = XmlReader.Create(file))
                    {
                        return (TargetViewSettings)serializer.Deserialize(xml);
                    }
                }
                catch { }
            }

            return new TargetViewSettings();
        }

        public void Save()
        {
            try
            {
                string file = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                XmlSerializer serializer = new XmlSerializer(typeof(TargetViewSettings));
                using (StreamWriter stream = File.CreateText(file))
                {
                    serializer.Serialize(stream, this);
                }
            }
            catch { }
        }
    }
}
