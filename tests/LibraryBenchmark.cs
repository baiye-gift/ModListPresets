using System;
using System.Diagnostics;
using System.IO;
using ModListPresets;
using Newtonsoft.Json;

class LibraryBenchmark {
    static int Main() {
        string folder = Path.Combine(Path.GetTempPath(), "oni-presets-benchmark-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            const int presets = 80, mods = 200, passes = 8;
            for (int i = 0; i < presets; i++) {
                var preset = new Preset { Name = "性能验证清单 " + i, Mode = "EXPANSION1_ID" };
                for (int j = 0; j < mods; j++)
                    preset.Mods.Add(new Entry { Platform = "Steam", Id = (1000000 + j).ToString(), Title = "模组 " + j, Enabled = j % 2 == 0 });
                PresetCore.WriteJson(Path.Combine(folder, i.ToString("D3") + ".json"), preset);
            }
            var library = new PresetLibrary();
            library.Load(folder); // Warm both deserialization and the OS file cache.
            string[] files = Directory.GetFiles(folder, "*.json");
            int baselineMods = 0, cachedMods = 0;
            var timer = Stopwatch.StartNew();
            for (int pass = 0; pass < passes; pass++)
                foreach (string file in files) baselineMods += PresetCore.Read(file).Mods.Count;
            timer.Stop(); double baselineMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            for (int pass = 0; pass < passes; pass++)
                foreach (PresetFile file in library.Load(folder)) {
                    if (file.Preset == null) throw new Exception(file.Error);
                    cachedMods += file.Preset.Mods.Count;
                }
            timer.Stop();
            if (baselineMods != presets * mods * passes || cachedMods != baselineMods)
                throw new Exception("Benchmark did not process all fixture entries.");
            Console.WriteLine(JsonConvert.SerializeObject(new {
                Presets = presets, ModsPerPreset = mods, RepeatedSearches = passes,
                BaselineMilliseconds = Math.Round(baselineMs, 2), CachedMilliseconds = Math.Round(timer.Elapsed.TotalMilliseconds, 2),
                EntriesChecked = cachedMods,
                Scope = "Temporary synthetic presets; JSON library loading only, excludes Unity rendering."
            }, Formatting.Indented));
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(folder, true); }
    }
}
