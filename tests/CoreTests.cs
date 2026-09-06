using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ModListPresets;

class CoreTests {
    static int count;
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
    static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch { rejected = true; } Assert(rejected, name); }
    static Entry E(string id) { return new Entry { Platform = "Steam", Id = id, Title = "测试模组", Enabled = true }; }
    static Preset P(params Entry[] entries) { return new Preset { Name = "稳定清单", Mode = "EXPANSION1_ID", Mods = entries.ToList() }; }
    static int Main() {
        string folder = Path.Combine(Path.GetTempPath(), "oni-presets-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            Assert(!new Settings().AutoBackup, "Automatic backup defaults off");
            var list = new List<int> {1,2,3,4}; PresetCore.Move(list, 3, 0);
            Assert(list.SequenceEqual(new [] {4,1,2,3}), "Move last to first shifts others");
            PresetCore.Move(list, 0, 3); Assert(list.SequenceEqual(new [] {1,2,3,4}), "Move first to last");
            PresetCore.Move(list, 1, 1); Assert(list.SequenceEqual(new [] {1,2,3,4}), "Same position preserves list");
            Reject(delegate { PresetCore.Move(list, 1, -1); }, "Reject negative position");
            Reject(delegate { PresetCore.Move(list, 1, 4); }, "Reject out of range");
            Assert(list.SequenceEqual(new [] {1,2,3,4}), "Rejected move does not mutate");
            var installed = new [] {E("1"),E("2"),E("3"),E("4")};
            var ordered = PresetCore.Ordered(P(E("3"), E("99"), E("1")), installed);
            Assert(ordered.Select(x => x.Id).SequenceEqual(new [] {"3","1","2","4"}), "Cross-machine missing items skipped; unlisted order preserved");
            var states = new Dictionary<string,bool>(); var enabledEntry = E("1"); enabledEntry.Enabled = true;
            Assert(PresetCore.DesiredEnabled(enabledEntry, enabledEntry.Key, states), "Preview defaults to saved enabled state");
            states[enabledEntry.Key] = false;
            Assert(!PresetCore.DesiredEnabled(enabledEntry, enabledEntry.Key, states), "Preview can override enabled to disabled");
            var disabledEntry = E("2"); disabledEntry.Enabled = false; states[disabledEntry.Key] = true;
            Assert(PresetCore.DesiredEnabled(disabledEntry, disabledEntry.Key, states), "Preview can override disabled to enabled");
            Assert(!PresetCore.DesiredEnabled(null, "Steam:3", states), "Unlisted mod defaults to disabled");
            var local = new Entry { Platform = "Local", Id = "1", StaticId = "same" };
            installed[0].StaticId = "same";
            ordered = PresetCore.Ordered(P(local), new [] {installed[0],local});
            Assert(Object.ReferenceEquals(ordered[0],local), "Source identity distinguishes local and workshop");
            Reject(delegate { PresetCore.Validate(P(E("1"), E("1"))); }, "Duplicate identities rejected");
            Reject(delegate { var p=P(); p.Schema=2; PresetCore.Validate(p); }, "Future schema rejected");
            Reject(delegate { var p=P(); p.Mode="DLC5_ID"; PresetCore.Validate(p); }, "Invalid game mode rejected");
            Reject(delegate { PresetCore.Validate(P(E("http://invalid"))); }, "Invalid workshop ID rejected");
            Reject(delegate { var p=P(); p.Name=" "; PresetCore.Validate(p); }, "Empty name rejected");
            Reject(delegate { var p=P(); p.Mods=null; PresetCore.Validate(p); }, "Missing array rejected");
            string file = Path.Combine(folder,"清单.json"); var saved=P(E("3231839363"), E("3746975301")); saved.Mods[0].Enabled=false;
            PresetCore.WriteJson(file,saved); var loaded=PresetCore.Read(file);
            Assert(loaded.Name==saved.Name && !loaded.Mods[0].Enabled && loaded.Mods[1].Enabled, "Unicode export/import preserves enabled and disabled states");
            saved.Name="第二版"; PresetCore.WriteJson(file,saved);
            Assert(PresetCore.Read(file).Name=="第二版", "Atomic replacement readable");
            Assert(Directory.GetFiles(folder,"*.tmp").Length==0, "Atomic writes leave no temporary files");
            File.WriteAllText(file,"{broken"); Reject(delegate { PresetCore.Read(file); }, "Corrupt JSON rejected");
            File.WriteAllText(file,"{\"$type\":\"Untrusted.Type, Unknown\",\"Name\":\"安全\",\"Mode\":\"\",\"Mods\":[]}");
            Assert(PresetCore.Read(file).Name=="安全", "JSON type metadata cannot instantiate types");
            File.WriteAllText(file,new string('x',4*1024*1024+1)); Reject(delegate { PresetCore.Read(file); }, "Oversized file rejected");
            Assert(PresetCore.NewFile(folder)!=PresetCore.NewFile(folder), "Save names unique without overwriting presets");
            string managed = Path.Combine(folder, "preset.json");
            Assert(PresetCore.IsManagedPresetFile(folder, managed), "Managed preset deletion accepts direct JSON child");
            Assert(!PresetCore.IsManagedPresetFile(folder, Path.Combine(folder, "nested", "preset.json")), "Managed preset deletion rejects nested path");
            Assert(!PresetCore.IsManagedPresetFile(folder, Path.Combine(folder, "preset.txt")), "Managed preset deletion rejects other extensions");
            File.WriteAllText(managed, "{}"); PresetCore.DeleteManagedPreset(folder, managed);
            Assert(!File.Exists(managed), "Managed preset deletion removes selected file");
            Reject(delegate { PresetCore.DeleteManagedPreset(folder, Path.Combine(folder, "nested", "preset.json")); }, "Managed preset deletion blocks outside direct child");
            Console.WriteLine("TOTAL: " + count + " passed"); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(folder,true); }
    }
}
