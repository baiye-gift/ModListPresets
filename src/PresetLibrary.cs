using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModListPresets {
    public sealed class PresetFile {
        public string Path;
        public Preset Preset;
        public string Error;
    }

    public sealed class PresetLibrary {
        sealed class CachedPreset {
            public long Length;
            public System.DateTime ModifiedUtc, CreatedUtc;
            public Preset Preset;
            public bool Matches(FileInfo file) {
                return file.Exists && Length == file.Length && ModifiedUtc == file.LastWriteTimeUtc && CreatedUtc == file.CreationTimeUtc;
            }
        }
        Dictionary<string, CachedPreset> cache = new Dictionary<string, CachedPreset>(StringComparer.OrdinalIgnoreCase);

        public void Invalidate() { cache.Clear(); }

        public List<PresetFile> Load(string folder) {
            var result = new List<PresetFile>();
            var next = new Dictionary<string, CachedPreset>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(Path.GetFullPath(folder), "*.json").OrderByDescending(f => f)) {
                var item = new PresetFile { Path = path };
                try {
                    var file = new FileInfo(path);
                    CachedPreset cached;
                    if (cache.TryGetValue(path, out cached) && cached.Matches(file)) {
                        item.Preset = cached.Preset;
                        next[path] = cached;
                    } else {
                        cached = new CachedPreset { Length = file.Length, ModifiedUtc = file.LastWriteTimeUtc, CreatedUtc = file.CreationTimeUtc };
                        item.Preset = PresetCore.Read(path);
                        file.Refresh();
                        // A file changed while reading is retried on the next render.
                        if (cached.Matches(file)) {
                            cached.Preset = item.Preset;
                            next[path] = cached;
                        }
                    }
                }
                catch (Exception e) { item.Error = e.Message; }
                result.Add(item);
            }
            // Drop removed files and retry failures instead of retaining stale errors.
            cache = next;
            return result;
        }
    }
}
