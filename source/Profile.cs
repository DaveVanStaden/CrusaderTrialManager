using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CrusaderTrailManager {
    public class Profile {
        public int Version = 1;
        public bool Enabled;
        public List<Rule> Rules = new List<Rule>();
        public static Profile Load(string path) {
            if (!File.Exists(path)) return new Profile();
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = System.Xml.XmlReader.Create(path, settings)) {
                var result = (Profile)new XmlSerializer(typeof(Profile)).Deserialize(reader);
                result.Validate(); return result;
            }
        }
        public void Validate() {
            if (Version != 1 || Rules == null || Rules.Count > 5000) throw new InvalidDataException("Invalid profile version or rule list.");
            var seen = new HashSet<string>();
            foreach (var r in Rules) {
                if (r == null || r.Trail < 0 || r.Trail > 2 || r.Mission < 1 || r.Mission > new int[]{50,30,20}[r.Trail]
                    || r.Player < 1 || r.Player > 8 || r.Donor < -1 || r.Donor > 28 || r.ReplacementLord < -1 || r.ReplacementLord > 28)
                    throw new InvalidDataException("Invalid mission, player slot or lord selection.");
                if (!seen.Add(r.Trail + ":" + r.Mission + ":" + r.Player)) throw new InvalidDataException("Duplicate rule for the same player slot.");
                if(r.CastleVariant < -1 || r.CastleVariant > 7)throw new InvalidDataException("Invalid built-in castle selection.");
                if(!string.IsNullOrEmpty(r.CustomCastle) && (string.IsNullOrEmpty(r.CustomLordFolder) || Path.GetFileName(r.CustomCastle)!=r.CustomCastle || !string.Equals(Path.GetExtension(r.CustomCastle),".aivjson",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Invalid custom castle selection.");
                if(!string.IsNullOrEmpty(r.CustomLordFolder) && r.CastleVariant>=0)throw new InvalidDataException("Choose a castle from the custom lord's own set.");
                if (r.Donor >= 0 && !string.IsNullOrWhiteSpace(r.CastleFile)) throw new InvalidDataException("Choose either built-in castles or a castle file.");
                if (!string.IsNullOrEmpty(r.CustomLordFolder) && (r.ReplacementLord >= 0 || r.Donor >= 0 || !string.IsNullOrEmpty(r.CastleFile) || !string.IsNullOrEmpty(r.AiFile))) throw new InvalidDataException("A custom lord cannot be combined with separate overrides.");
            }
        }
        public void Save(string path) {
            Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var writer = new StreamWriter(temp)) new XmlSerializer(typeof(Profile)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
    }
    public class Rule {
        public int Trail;
        public int Mission = 1;
        public int Player = 2;
        public int Donor = -1;
        // Zero-based built-in lord index; -1 preserves the mission's original opponent.
        public int ReplacementLord = -1;
        public string CastleFile = "";
        public string AiFile = "";
        public string CustomLordFolder = "";
        public string CustomConfig = "";
        public int CastleVariant = -1;
        public string CustomCastle = "";
    }
}
