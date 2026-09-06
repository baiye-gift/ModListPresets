using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using KMod;
using UnityEngine;

namespace ModListPresets {
    public sealed class ModEntryPoint : UserMod2 {
        public override void OnLoad(Harmony harmony) {
            base.OnLoad(harmony);
            Debug.Log("[ModListPresets] 1.4.0 loaded; anchored menus and explicit post-apply states.");
        }
    }
    [HarmonyPatch(typeof(ModsScreen), "OnActivate")]
    public static class AttachPanel {
        public static void Postfix(ModsScreen __instance) {
            try {
                var panel = __instance.gameObject.GetComponent<PresetPanel>();
                if (panel == null) panel = __instance.gameObject.AddComponent<PresetPanel>();
                panel.Initialize(__instance);
            } catch (Exception e) { Debug.LogWarning("[ModListPresets] UI unavailable: " + e); }
        }
    }
    [HarmonyPatch(typeof(ModsScreen), "OnDeactivate")]
    public static class ClosePanel {
        public static void Prefix(ModsScreen __instance) {
            var panel = __instance.gameObject.GetComponent<PresetPanel>();
            if (panel != null) panel.Close();
        }
    }
    public static class GameBridge {
        public const string SelfId = "Local.ModListPresets";
        public static string Root { get { return Path.Combine(KMod.Manager.GetDirectory(), "config", "ModListPresets"); } }
        public static string Library { get { return Path.Combine(Root, "presets"); } }
        public static string ExportFolder { get { return Path.Combine(Root, "exports"); } }
        public static string BackupFile { get { return Path.Combine(Root, "previous.json"); } }
        public static string ActiveMode { get { return DlcManager.IsExpansion1Active() ? "EXPANSION1_ID" : ""; } }
        public static string GameVersion {
            get {
                // Read the installed game's constant at runtime, rather than embedding this build's value.
                var build = AccessTools.Field(typeof(KleiVersion), "ChangeList");
                return build == null ? Application.version : Convert.ToString(build.GetRawConstantValue());
            }
        }
        public static Manager Manager { get { return Global.Instance.modManager; } }
        public static Entry Describe(Mod m) {
            return new Entry { Platform = m.label.distribution_platform.ToString(), Id = m.label.id,
                StaticId = m.staticID, Title = m.label.title, Version = m.packagedModInfo == null ? "" : m.packagedModInfo.version,
                WorkshopVersion = m.label.version, Enabled = m.IsEnabledForActiveDlc() };
        }
        public static bool Installed(Mod m) { return m.status != Mod.Status.NotInstalled && m.status != Mod.Status.UninstallPending; }
        public static Preset Capture(string name) {
            return new Preset { Name = name, CreatedUtc = System.DateTime.UtcNow.ToString("o"),
                GameVersion = GameVersion, Mode = ActiveMode,
                Mods = Manager.mods.Where(Installed).Select(Describe).ToList() };
        }
        public static void CheckContext(Preset p) {
            PresetCore.Validate(p);
            if (p.Mode != ActiveMode) throw new InvalidOperationException("清单属于另一游戏模式，请先切换原版 / 眼冒金星。");
        }
        public static void Apply(Preset p, IDictionary<string, bool> overrides, bool autoBackup, ModsScreen screen) {
            CheckContext(p);
            var desired = p.Mods.ToDictionary(e => e.Key, StringComparer.Ordinal);
            foreach (var m in Manager.mods.Where(Installed)) {
                var current = Describe(m); Entry entry;
                desired.TryGetValue(current.Key, out entry);
                bool enabled = PresetCore.DesiredEnabled(entry, current.Key, overrides);
                if (m.staticID == SelfId) enabled = true;
                if (enabled && m.available_content == 0) throw new InvalidOperationException("此模组目前不兼容或无可用内容，请在预览中设为停用：" + current.Title);
            }
            Commit(delegate {
                var all = Manager.mods;
                var order = PresetCore.Ordered(p, all.Select(Describe).ToList());
                var byKey = all.ToDictionary(m => Describe(m).Key, StringComparer.Ordinal);
                foreach (var m in all.Where(Installed)) {
                    Entry entry; var key = Describe(m).Key;
                    desired.TryGetValue(key, out entry);
                    bool enabled = PresetCore.DesiredEnabled(entry, key, overrides);
                    // Keep this recovery tool available even when importing another computer's list.
                    if (m.staticID == SelfId) enabled = true;
                    m.SetEnabledForActiveDlc(enabled);
                }
                all.Clear(); all.AddRange(order.Select(e => byKey[e.Key]));
            }, autoBackup, screen);
        }
        public static void Move(string key, int destination, bool autoBackup, ModsScreen screen) {
            int source = Manager.mods.FindIndex(m => Describe(m).Key == key);
            if (source == destination) return;
            if (source < 0 || destination < 0 || destination >= Manager.mods.Count) throw new InvalidOperationException("序号超出范围。");
            Commit(delegate { PresetCore.Move(Manager.mods, source, destination); }, autoBackup, screen);
        }
        static void Commit(System.Action change, bool autoBackup, ModsScreen screen) {
            var original = Manager.mods.ToList();
            var flags = original.Select(m => m.enabledForDlc == null ? null : new List<string>(m.enabledForDlc)).ToList();
            var file = Path.Combine(KMod.Manager.GetDirectory(), "mods.json");
            byte[] disk = File.Exists(file) ? File.ReadAllBytes(file) : null;
            if (autoBackup) PresetCore.WriteJson(BackupFile, Capture("应用前备份"));
            try {
                change();
                if (!Manager.Save()) throw new IOException("游戏无法保存模组配置。");
            } catch {
                Manager.mods.Clear(); Manager.mods.AddRange(original);
                for (int i = 0; i < original.Count; i++) original[i].enabledForDlc = flags[i];
                if (disk != null) File.WriteAllBytes(file, disk);
                throw;
            }
            // No DLL hot loading. The normal ModsScreen exit compares its footprint and offers restart.
            try {
                if (Manager.on_update != null) Manager.on_update(screen);
                AccessTools.Method(typeof(ModsScreen), "BuildDisplay").Invoke(screen, null);
                AccessTools.Method(typeof(ModsScreen), "UpdateToggleAllButton").Invoke(screen, null);
            } catch (Exception e) { Debug.LogWarning("[ModListPresets] Saved; refresh after reopening Mods: " + e); }
        }
    }
}
