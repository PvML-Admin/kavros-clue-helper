using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KavrosClueHelper
{
    // Names for the client picker. Companion writes %AppData%\KavrosOverlay\client-names.json.
    // A client with no saved name is "Client N".
    internal static class ClientIdentity
    {
        public static string Label(uint pid, int index, string title)
        {
            string fromShare = NameFromShare(pid);
            if (!string.IsNullOrEmpty(fromShare)) return fromShare;
            return "Client " + (index + 1).ToString();
        }

        static string sharePath;
        static DateTime shareTime;
        static Dictionary<int, string> shareNames = new Dictionary<int, string>();

        static string NameFromShare(uint pid)
        {
            if (pid == 0) return null;
            try
            {
                if (sharePath == null)
                    sharePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KavrosOverlay", "client-names.json");
                if (!File.Exists(sharePath))
                {
                    shareTime = DateTime.MinValue;
                    shareNames = new Dictionary<int, string>();
                    return null;
                }
                DateTime written = File.GetLastWriteTimeUtc(sharePath);
                if (written != shareTime)
                {
                    shareTime = written;
                    shareNames = ParseShare(File.ReadAllText(sharePath, Encoding.UTF8));
                }
                string name;
                if (shareNames.TryGetValue((int)pid, out name) && !string.IsNullOrEmpty(name)) return name;
            }
            catch (Exception) { }
            return null;
        }

        internal static Dictionary<int, string> ParseShare(string json)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(json)) return map;
            foreach (Match obj in Regex.Matches(json, "\\{[^}]+\\}"))
            {
                Match pid = Regex.Match(obj.Value, "\"pid\"\\s*:\\s*(-?\\d+)");
                Match name = Regex.Match(obj.Value, "\"name\"\\s*:\\s*\"([^\"]*)\"");
                int id;
                if (pid.Success && name.Success && int.TryParse(pid.Groups[1].Value, out id) && name.Groups[1].Value.Length > 0)
                    map[id] = name.Groups[1].Value;
            }
            return map;
        }

        internal static void ResetShareForTests(string path)
        {
            sharePath = path;
            shareTime = DateTime.MinValue;
            shareNames = new Dictionary<int, string>();
        }
    }
}
