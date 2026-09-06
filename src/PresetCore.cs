using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ModListPresets {
    public sealed class Entry {
        public string Platform;
        public string Id;
        public string StaticId;
        public string Title;
        public string Version;
        public long WorkshopVersion;
        public bool Enabled;
        [JsonIgnore] public string Key { get { return Platform + ":" + Id; } }
    }
    public sealed class Preset {
        public int Schema = 1;
        public string Name;
        public string CreatedUtc;
        public string GameVersion;
        public string Mode;
        public List<Entry> Mods = new List<Entry>();
    }
    public sealed class Settings { public bool AutoBackup = false; }
    public static class PresetCore {
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None, MaxDepth = 20,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
        public static void Validate(Preset p) {
            if (p == null || p.Schema != 1) throw new InvalidDataException("清单格式或版本不支持。");
            if (String.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100) throw new InvalidDataException("清单名称须为 1–100 字。");
            if (p.Mode != "" && p.Mode != "EXPANSION1_ID") throw new InvalidDataException("清单游戏模式不支持。");
            if (p.Mods == null || p.Mods.Count > 2000) throw new InvalidDataException("清单项目过多或缺失。");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in p.Mods) {
                if (m == null || String.IsNullOrWhiteSpace(m.Id) || m.Id.Length > 256 ||
                    !new [] {"Local", "Steam", "Epic", "Rail", "Dev"}.Contains(m.Platform))
                    throw new InvalidDataException("清单存在无效模组标识。");
                if (m.Platform == "Steam") { ulong id; if (!UInt64.TryParse(m.Id, out id) || id == 0) throw new InvalidDataException("创意工坊 ID 无效。"); }
                if (!keys.Add(m.Key)) throw new InvalidDataException("清单存在重复模组：" + m.Id);
            }
        }
        public static Preset Read(string path) {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4 * 1024 * 1024) throw new InvalidDataException("文件不存在或超过 4 MB。");
            var p = JsonConvert.DeserializeObject<Preset>(File.ReadAllText(path, Encoding.UTF8), JsonSettings);
            Validate(p); return p;
        }
        public static void WriteJson(string path, object data) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, JsonConvert.SerializeObject(data, Formatting.Indented, JsonSettings), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        // Exact source identity avoids confusing a workshop original with a FIX fork.
        public static List<Entry> Ordered(Preset p, IList<Entry> installed) {
            var byKey = installed.ToDictionary(m => m.Key, StringComparer.Ordinal);
            var result = new List<Entry>(); var used = new HashSet<string>();
            foreach (var e in p.Mods) { Entry m; if (byKey.TryGetValue(e.Key, out m)) { result.Add(m); used.Add(m.Key); } }
            result.AddRange(installed.Where(m => !used.Contains(m.Key)));
            return result;
        }
        public static bool DesiredEnabled(Entry saved, string key, IDictionary<string, bool> overrides) {
            bool value;
            if (overrides != null && overrides.TryGetValue(key, out value)) return value;
            return saved != null && saved.Enabled;
        }
        public static void Move<T>(List<T> list, int source, int destination) {
            if (source < 0 || source >= list.Count || destination < 0 || destination >= list.Count)
                throw new ArgumentOutOfRangeException("destination", "序号超出范围。");
            if (source == destination) return;
            T item = list[source]; list.RemoveAt(source); list.Insert(destination, item);
        }
        public static bool IsManagedPresetFile(string folder, string path) {
            if (String.IsNullOrWhiteSpace(folder) || String.IsNullOrWhiteSpace(path)) return false;
            try {
                string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string file = Path.GetFullPath(path);
                string parent = Path.GetDirectoryName(file).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return String.Equals(root, parent, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase);
            } catch { return false; }
        }
        public static void DeleteManagedPreset(string folder, string path) {
            if (!IsManagedPresetFile(folder, path)) throw new InvalidOperationException("只能删除清单库中的 JSON 文件。");
            if (!File.Exists(path)) throw new FileNotFoundException("清单文件不存在。", path);
            File.Delete(path);
        }
        public static string NewFile(string folder) { return Path.Combine(folder, System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json"); }
    }
}
