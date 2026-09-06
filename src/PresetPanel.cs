using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModListPresets {
    public sealed class PresetPanel : MonoBehaviour {
        ModsScreen screen;
        GameObject launcher;
        PresetModal modal;
        RectTransform panel, toolbar, filterBar, rows;
        LocText status, summary;
        KInputTextField searchInput;
        KScrollRect scroll;
        KButton buttonTemplate, libraryTab, currentTab, applyButton;
        LocText textTemplate;
        GameObject applyFooter, dialogShade;
        Settings settings = new Settings();
        Preset selected;
        string selectedFile;
        readonly Dictionary<string, bool> previewOverrides = new Dictionary<string, bool>(StringComparer.Ordinal);
        string view = "library", search = "", filter = "all";
        float refreshAt = -1f;
        bool scrollToTopOnRender;
        int scrollToTopFrames;
        Vector2 lastHostSize = new Vector2(-1f, -1f);

        static readonly Color PanelColor = new Color(.20f, .20f, .28f, 1f);
        static readonly Color HeaderColor = new Color(.27f, .26f, .36f, 1f);
        static readonly Color SurfaceColor = new Color(.145f, .145f, .205f, 1f);
        static readonly Color RowColor = new Color(.235f, .23f, .31f, 1f);
        static readonly Color InputColor = new Color(.105f, .105f, .15f, 1f);
        static readonly Color TrackColor = new Color(.09f, .09f, .13f, 1f);
        static readonly Color HandleColor = new Color(.45f, .41f, .58f, 1f);
        static readonly Color Ink = new Color(.93f, .93f, .96f, 1f);
        static readonly Color MutedInk = new Color(.68f, .68f, .75f, 1f);
        static readonly Color WarningInk = new Color(1f, .72f, .36f, 1f);

        public void Initialize(ModsScreen owner) {
            screen = owner;
            ResolveTemplates();
            if (launcher == null && buttonTemplate != null) {
                Transform footer = buttonTemplate.transform.parent;
                KButton button = CloneButton(footer, "模组清单", Open, 142f);
                launcher = button.gameObject;
                launcher.name = "ModListPresetsButton";
                launcher.transform.SetAsLastSibling();
            }
        }

        public void Close() {
            refreshAt = -1f;
            scrollToTopOnRender = false;
            scrollToTopFrames = 0;
            CloseDialog();
            if (modal != null) {
                PresetModal closing = modal;
                modal = null;
                closing.Owner = null;
                closing.Deactivate();
            }
            panel = null; rows = null; scroll = null;
        }

        void Update() {
            if (refreshAt >= 0f && Time.unscaledTime >= refreshAt) {
                refreshAt = -1f;
                Guard(RenderRows);
            }
            ResizePanel(false);
        }

        void LateUpdate() {
            if (scrollToTopFrames <= 0 || rows == null || scroll == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            scrollToTopFrames--;
        }

        void ResolveTemplates() {
            if (screen == null) return;
            if (buttonTemplate == null) {
                FieldInfo field = typeof(ModsScreen).GetField("closeButton", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) buttonTemplate = field.GetValue(screen) as KButton;
                if (buttonTemplate == null) buttonTemplate = screen.GetComponentsInChildren<KButton>(true).FirstOrDefault();
            }
            if (textTemplate == null) {
                FieldInfo field = typeof(ModsScreen).GetField("windowTitle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) textTemplate = field.GetValue(screen) as LocText;
                if (textTemplate == null) textTemplate = screen.GetComponentsInChildren<LocText>(true).FirstOrDefault();
            }
        }

        void Guard(System.Action action) {
            try { action(); }
            catch (Exception e) {
                Say("操作未完成：" + e.Message, true);
                Debug.LogWarning("[ModListPresets] " + e);
            }
        }

        void Open() {
            Close();
            ResolveTemplates();
            if (buttonTemplate == null || textTemplate == null)
                throw new InvalidOperationException("无法取得缺氧原生界面样式，请重新打开模组页面后再试。");
            Directory.CreateDirectory(GameBridge.Library);
            LoadSettings();
            selected = null; selectedFile = null; previewOverrides.Clear(); view = "library"; search = ""; filter = "all";

            GameObject root = new GameObject("ModListPresetsModal", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.SetActive(false);
            root.transform.SetParent(screen.transform, false);
            Stretch(root.GetComponent<RectTransform>());
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 32000;
            modal = root.AddComponent<PresetModal>();
            modal.Owner = this;
            BuildPanel(root.transform);
            root.SetActive(true);
            modal.Activate();
            ResizePanel(true);
            RenderToolbar();
            RequestScrollToTop();
            RenderRows();
        }

        void LoadSettings() {
            settings = new Settings();
            string config = Path.Combine(GameBridge.Root, "settings.json");
            if (!File.Exists(config)) return;
            try { settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(config), PresetCore.JsonSettings) ?? new Settings(); }
            catch (Exception e) { Debug.LogWarning("[ModListPresets] Settings ignored: " + e.Message); }
        }

        void BuildPanel(Transform root) {
            panel = Box(root, "Panel", PanelColor);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 12, 12); column.spacing = 8f;
            column.childControlHeight = true; column.childForceExpandHeight = false;
            column.childControlWidth = true; column.childForceExpandWidth = true;

            RectTransform header = Row(panel, 46f, HeaderColor);
            LocText title = Label(header, "模组清单", 0f, 24f); title.fontStyle = FontStyles.Bold;
            Label(header, "保存、恢复与调整加载顺序", 0f, 15f, MutedInk);
            KButton settingsButton = null;
            settingsButton = CloneButton(header, "设置...", delegate { ShowSettingsMenu(settingsButton); }, 88f);
            CloneButton(header, "关闭", Close, 80f);

            RectTransform tabs = Row(panel, 40f, Color.clear);
            libraryTab = CloneButton(tabs, "清单库", delegate { SwitchView("library"); }, 150f);
            currentTab = CloneButton(tabs, "当前模组 / 顺序", delegate { SwitchView("current"); }, 180f);
            Label(tabs, "", 0f, 14f);

            toolbar = Row(panel, 42f, SurfaceColor);
            filterBar = Row(panel, 38f, Color.clear);
            summary = Label(panel, "", 0f, 15f, MutedInk); Height(summary.gameObject, 48f);

            RectTransform scrollRoot = Box(panel, "List", SurfaceColor);
            LayoutElement grow = scrollRoot.gameObject.AddComponent<LayoutElement>(); grow.flexibleHeight = 1f; grow.minHeight = 130f;
            RectTransform viewport = Box(scrollRoot, "Viewport", Color.clear);
            Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-31f, -5f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f); content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 5f; list.padding = new RectOffset(3, 3, 3, 3);
            list.childControlHeight = true; list.childForceExpandHeight = false;
            list.childControlWidth = true; list.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rows = content;

            RectTransform track = Box(scrollRoot, "Scrollbar", TrackColor);
            Anchor(track, new Vector2(1f, 0f), Vector2.one, new Vector2(-23f, 5f), new Vector2(-5f, -5f));
            RectTransform sliding = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            sliding.SetParent(track, false); Anchor(sliding, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            RectTransform handle = Box(sliding, "Handle", HandleColor); Stretch(handle);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<KImage>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            track.gameObject.AddComponent<MinimumScrollbarHandle>().MinimumPixels = 24f;
            scroll = scrollRoot.gameObject.AddComponent<KScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport; scroll.content = content; scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.scrollSensitivity = 28f; scroll.forceContentMatchWidth = true;

            applyFooter = Row(panel, 44f, Color.clear).gameObject;
            applyButton = CloneButton(applyFooter.transform, "应用这份清单", ApplyPreview, 180f);
            Label(applyFooter.transform, "仅修改当前游戏模式；管理工具自身保持启用。", 0f, 14f, MutedInk);
            status = Label(panel, "清单只记录配置，不包含模组本体；缺失模组会自动跳过。", 0f, 14f, MutedInk);
            Height(status.gameObject, 38f);
        }

        void ResizePanel(bool force) {
            if (panel == null || modal == null) return;
            RectTransform host = modal.transform as RectTransform;
            Vector2 size = host == null ? Vector2.zero : host.rect.size;
            if (size.x <= 1f || size.y <= 1f) {
                RectTransform screenRect = screen.transform as RectTransform;
                if (screenRect != null) size = screenRect.rect.size;
            }
            if (!force && (size - lastHostSize).sqrMagnitude < 1f) return;
            lastHostSize = size;
            panel.sizeDelta = new Vector2(Mathf.Max(430f, Mathf.Min(860f, size.x - 48f)), Mathf.Max(360f, Mathf.Min(620f, size.y - 42f)));
        }

        void SwitchView(string next) {
            selected = null; selectedFile = null; previewOverrides.Clear(); view = next; search = ""; filter = "all";
            RequestScrollToTop();
            RenderToolbar(); RenderRows();
        }

        void RenderToolbar() {
            if (toolbar == null) return;
            ClearChildren(toolbar); searchInput = null;
            if (view == "library") {
                searchInput = Input(toolbar, "搜索清单名称", 0f); WireSearch(searchInput);
                CloneButton(toolbar, "保存当前", ShowSaveDialog, 112f);
                CloneButton(toolbar, "导入", ShowImportDialog, 86f);
                KButton moreButton = null;
                moreButton = CloneButton(toolbar, "更多...", delegate { ShowLibraryActions(moreButton); }, 92f);
            } else if (view == "current") {
                searchInput = Input(toolbar, "搜索模组名称 / 工坊 ID", 0f); WireSearch(searchInput);
                Label(toolbar, "输入序号后按 Enter 或点“移动”", 250f, 14f, MutedInk);
            } else {
                CloneButton(toolbar, "返回清单库", delegate { SwitchView("library"); }, 120f);
                searchInput = Input(toolbar, "在预览中搜索名称 / ID", 0f); WireSearch(searchInput);
                KButton actionsButton = null;
                actionsButton = CloneButton(toolbar, "清单操作...", delegate { ShowPresetActions(actionsButton); }, 116f);
            }
            UpdateTabs();
            RenderFilterBar();
            if (applyFooter != null) applyFooter.SetActive(view == "preview");
        }

        void RenderFilterBar() {
            if (filterBar == null) return;
            ClearChildren(filterBar);
            filterBar.gameObject.SetActive(view != "library");
            if (view == "library") return;

            Label(filterBar, "查看", 42f, 14f, MutedInk);
            KButton filterButton = null;
            filterButton = CloneButton(filterBar, FilterTitle() + "...", delegate { ShowFilterMenu(filterButton); }, 142f);
            Label(filterBar, "", 0f, 14f);
            if (view == "preview") {
                Label(filterBar, "应用后状态", 90f, 14f, MutedInk);
                KButton batchButton = null;
                batchButton = CloneButton(filterBar, "批量设置...", delegate { ShowBatchMenu(batchButton); }, 116f);
            }
        }

        string FilterTitle() {
            if (filter == "enabled") return view == "current" ? "已启用" : "应用后启用";
            if (filter == "disabled") return view == "current" ? "已停用" : "应用后停用";
            if (filter == "missing") return "缺失";
            return "全部";
        }

        void ShowFilterMenu(KButton anchor) {
            List<KeyValuePair<string, System.Action>> items = new List<KeyValuePair<string, System.Action>>();
            items.Add(MenuItem(FilterMenuTitle("all", "全部"), delegate { SetFilter("all"); }));
            items.Add(MenuItem(FilterMenuTitle("enabled", view == "current" ? "已启用" : "应用后启用"), delegate { SetFilter("enabled"); }));
            items.Add(MenuItem(FilterMenuTitle("disabled", view == "current" ? "已停用" : "应用后停用"), delegate { SetFilter("disabled"); }));
            if (view == "preview") items.Add(MenuItem(FilterMenuTitle("missing", "缺失"), delegate { SetFilter("missing"); }));
            ShowMenu(anchor, 164f, items);
        }

        string FilterMenuTitle(string value, string title) { return filter == value ? "✓  " + title : title; }

        void SetFilter(string value) {
            filter = value; CloseDialog(); RequestScrollToTop(); RenderFilterBar(); RenderRows();
        }

        void ShowBatchMenu(KButton anchor) {
            List<KeyValuePair<string, System.Action>> items = new List<KeyValuePair<string, System.Action>>();
            items.Add(MenuItem("按清单原状态", delegate { SetAllPreviewStates(null); }));
            items.Add(MenuItem("全部启用", delegate { SetAllPreviewStates(true); }));
            items.Add(MenuItem("全部停用", delegate { SetAllPreviewStates(false); }));
            ShowMenu(anchor, 164f, items);
        }

        void SetAllPreviewStates(bool? enabled) {
            if (selected == null) return;
            CloseDialog(); previewOverrides.Clear();
            if (enabled.HasValue)
                foreach (KMod.Mod mod in GameBridge.Manager.mods.Where(GameBridge.Installed))
                    if (mod.staticID != GameBridge.SelfId) previewOverrides[GameBridge.Describe(mod).Key] = enabled.Value;
            RenderRows();
            Say(!enabled.HasValue ? "已恢复清单原状态；尚未修改游戏配置。" :
                (enabled.Value ? "应用后将启用所有已安装模组。" : "应用后将停用所有模组，管理工具除外。"));
        }

        void WireSearch(KInputTextField field) {
            field.text = search;
            field.onValueChanged.AddListener(delegate(string value) {
                search = value;
                RequestScrollToTop();
                refreshAt = Time.unscaledTime + .16f;
            });
        }

        void UpdateTabs() {
            if (libraryTab != null && libraryTab.bgImage != null)
                libraryTab.bgImage.ColorState = view == "current" ? KImage.ColorSelector.Inactive : KImage.ColorSelector.Active;
            if (currentTab != null && currentTab.bgImage != null)
                currentTab.bgImage.ColorState = view == "current" ? KImage.ColorSelector.Active : KImage.ColorSelector.Inactive;
        }

        void RenderRows() {
            if (rows == null || scroll == null) return;
            bool moveToTop = scrollToTopOnRender;
            float position = moveToTop ? 1f : scroll.verticalNormalizedPosition;
            ClearChildren(rows);
            if (view == "library") RenderLibrary(); else if (view == "current") RenderCurrent(); else RenderPreview();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = position;
            if (moveToTop) {
                scrollToTopOnRender = false;
                scrollToTopFrames = 2;
            }
            if (applyFooter != null) applyFooter.SetActive(view == "preview");
        }

        void RequestScrollToTop() { scrollToTopOnRender = true; }

        void RenderLibrary() {
            string[] files = Directory.GetFiles(GameBridge.Library, "*.json").OrderByDescending(delegate(string f) { return f; }).ToArray();
            int shown = 0;
            summary.text = "已保存 " + files.Length + " 份清单。预览后才会显示应用按钮；手动清单永不自动覆盖。";
            foreach (string file in files) {
                Preset preset;
                try { preset = PresetCore.Read(file); }
                catch (Exception e) { Label(Row(rows, 54f, RowColor), "无法读取 " + Path.GetFileName(file) + "：" + e.Message, 0f, 14f, WarningInk); continue; }
                if (!Matches(preset.Name)) continue;
                shown++;
                RectTransform row = Row(rows, 64f, RowColor);
                string mode = String.IsNullOrEmpty(preset.Mode) ? "原版" : "眼冒金星";
                Label(row, preset.Name + "\n" + mode + " · 启用 " + preset.Mods.Count(delegate(Entry m) { return m.Enabled; }) + " / " + preset.Mods.Count + " · " + FormatCreatedUtc(preset.CreatedUtc), 0f, 15f);
                Preset captured = preset; string capturedFile = file;
                CloneButton(row, "预览", delegate { Preview(captured, capturedFile); }, 86f);
            }
            if (files.Length == 0) EmptyRow("还没有清单。点击上方“保存当前”，为现在的模组组合起一个名字。");
            else if (shown == 0) EmptyRow("没有匹配的清单。");
        }

        void RenderCurrent() {
            var mods = GameBridge.Manager.mods;
            summary.text = "当前启用 " + mods.Count(delegate(KMod.Mod m) { return m.IsEnabledForActiveDlc(); }) + " / " + mods.Count + "。筛选不改变全局序号；输入序号后按 Enter 或点“移动”。";
            int shown = 0;
            for (int i = 0; i < mods.Count; i++) {
                Entry entry = GameBridge.Describe(mods[i]);
                if (!Matches(entry.Title + " " + entry.Id)) continue;
                if (filter == "enabled" && !entry.Enabled) continue;
                if (filter == "disabled" && entry.Enabled) continue;
                shown++;
                RectTransform row = Row(rows, 58f, RowColor);
                Label(row, (i + 1) + ". " + (entry.Enabled ? "[启用] " : "[停用] ") + entry.Title + "\n" + entry.Platform + " · " + entry.Id, 0f, 15f);
                KInputTextField order = Input(row, "序号", 68f);
                order.characterValidation = TMP_InputField.CharacterValidation.Integer; order.characterLimit = 5; order.text = (i + 1).ToString();
                string key = entry.Key;
                System.Action move = delegate {
                    int target;
                    if (!Int32.TryParse(order.text, out target)) throw new InvalidOperationException("请输入有效序号。");
                    float position = scroll.verticalNormalizedPosition;
                    GameBridge.Move(key, target - 1, settings.AutoBackup, screen);
                    RenderRows(); scroll.verticalNormalizedPosition = position;
                    Say("顺序已保存，其他模组会自动顺移；离开模组页面后按提示重启。");
                };
                order.onSubmit.AddListener(delegate(string ignored) { Guard(move); });
                CloneButton(row, "移动", move, 76f);
            }
            if (shown == 0) EmptyRow("没有匹配的模组。");
        }

        void RenderPreview() {
            if (selected == null) { SwitchView("library"); return; }
            Dictionary<string, Entry> current = GameBridge.Manager.mods.Where(GameBridge.Installed).Select(GameBridge.Describe).ToDictionary(delegate(Entry m) { return m.Key; });
            Dictionary<string, Entry> target = selected.Mods.ToDictionary(delegate(Entry m) { return m.Key; });
            int missing = selected.Mods.Count(delegate(Entry e) { return !current.ContainsKey(e.Key); });
            int finalEnabled = current.Values.Count(delegate(Entry e) {
                Entry saved; target.TryGetValue(e.Key, out saved);
                return e.StaticId == GameBridge.SelfId || PresetCore.DesiredEnabled(saved, e.Key, previewOverrides);
            });
            int finalDisabled = current.Count - finalEnabled;
            int enable = current.Values.Count(delegate(Entry e) {
                Entry saved; target.TryGetValue(e.Key, out saved);
                return !e.Enabled && PresetCore.DesiredEnabled(saved, e.Key, previewOverrides);
            });
            int disable = current.Values.Count(delegate(Entry e) {
                Entry saved; target.TryGetValue(e.Key, out saved);
                return e.Enabled && e.StaticId != GameBridge.SelfId && !PresetCore.DesiredEnabled(saved, e.Key, previewOverrides);
            });
            bool modeMatches = selected.Mode == GameBridge.ActiveMode;
            string blocked = !modeMatches ? "【模式不符，不能应用】 " : "";
            summary.text = blocked + selected.Name + "：应用后启用 " + finalEnabled + "、停用 " + finalDisabled + "、缺失 " + missing + "。\n本次变化：启用 " + enable + "、停用 " + disable + "；缺失项自动跳过，预览选择不会改写清单。";
            if (applyButton != null) applyButton.isInteractable = modeMatches;
            int index = 0, shown = 0;
            foreach (Entry entry in selected.Mods) {
                index++;
                Entry local; current.TryGetValue(entry.Key, out local);
                bool effectiveEnabled = local != null && PresetCore.DesiredEnabled(entry, entry.Key, previewOverrides);
                if (!Matches(entry.Title + " " + entry.Id)) continue;
                if (filter == "enabled" && (local == null || !effectiveEnabled)) continue;
                if (filter == "disabled" && (local == null || effectiveEnabled)) continue;
                if (filter == "missing" && local != null) continue;
                if (filter != "all" && filter != "missing" && local == null) continue;
                shown++;
                string state = local == null ? "缺失 · 自动跳过" : "已安装";
                if (local != null && (local.Version != entry.Version || local.WorkshopVersion != entry.WorkshopVersion)) state += " · 版本不同 " + entry.Version + " → " + local.Version;
                RectTransform row = Row(rows, 64f, RowColor);
                Label(row, index + ". " + entry.Title + "\n" + state + " · " + entry.Platform + " · " + entry.Id, 0f, 15f, local == null ? MutedInk : Ink);
                Entry captured = entry;
                if (local == null) Label(row, "未安装", 94f, 14f, MutedInk);
                else if (local.StaticId == GameBridge.SelfId) Label(row, "启用", 94f, 14f, MutedInk);
                else StateButton(row, effectiveEnabled, delegate(bool value) {
                    previewOverrides[captured.Key] = value; refreshAt = Time.unscaledTime + .05f;
                }, 104f);
                if (local == null && entry.Platform == "Steam") CloneButton(row, "工坊页面", delegate {
                    ulong id; if (UInt64.TryParse(captured.Id, out id)) Application.OpenURL("https://steamcommunity.com/sharedfiles/filedetails/?id=" + id);
                }, 96f);
            }
            foreach (Entry entry in current.Values.Where(delegate(Entry e) { return !target.ContainsKey(e.Key); })) {
                if (!Matches(entry.Title + " " + entry.Id)) continue;
                bool effectiveEnabled = entry.StaticId == GameBridge.SelfId || PresetCore.DesiredEnabled(null, entry.Key, previewOverrides);
                if (filter == "enabled" && !effectiveEnabled) continue;
                if (filter == "disabled" && effectiveEnabled) continue;
                if (filter == "missing") continue;
                shown++;
                RectTransform row = Row(rows, 58f, RowColor);
                Label(row, "清单外 · " + entry.Title + "\n已安装 · " + entry.Platform + " · " + entry.Id, 0f, 14f, MutedInk);
                string key = entry.Key;
                if (entry.StaticId == GameBridge.SelfId) Label(row, "启用", 94f, 14f, MutedInk);
                else StateButton(row, effectiveEnabled, delegate(bool value) {
                    previewOverrides[key] = value; refreshAt = Time.unscaledTime + .05f;
                }, 104f);
            }
            if (shown == 0) EmptyRow("没有匹配的预览项。");
        }

        void Preview(Preset preset, string sourceFile = null) {
            selected = preset; selectedFile = sourceFile; previewOverrides.Clear(); view = "preview"; search = ""; filter = "all";
            RequestScrollToTop();
            RenderToolbar(); RenderRows();
        }

        void PreviewBackup() {
            if (!File.Exists(GameBridge.BackupFile)) throw new IOException("还没有备份。开启自动备份后，应用清单或调整顺序时会保留上一份配置。");
            Preview(PresetCore.Read(GameBridge.BackupFile));
        }

        void ApplyPreview() {
            if (view != "preview" || selected == null) throw new InvalidOperationException("请先预览一份清单。");
            GameBridge.Apply(selected, previewOverrides, settings.AutoBackup, screen);
            SwitchView("current"); Say("清单已应用。离开模组页面后，请按游戏提示重启。");
        }

        void ExportSelected() {
            if (selected == null) throw new InvalidOperationException("请先预览一份清单。");
            string file = PresetCore.NewFile(GameBridge.ExportFolder);
            PresetCore.WriteJson(file, selected); OpenFolder(GameBridge.ExportFolder); Say("已导出：" + file);
        }

        void ShowLibraryActions(KButton anchor) {
            List<KeyValuePair<string, System.Action>> items = new List<KeyValuePair<string, System.Action>>();
            items.Add(MenuItem("上一份备份", delegate { CloseDialog(); PreviewBackup(); }));
            items.Add(MenuItem("打开清单文件夹", delegate { CloseDialog(); OpenFolder(GameBridge.Library); }));
            ShowMenu(anchor, 184f, items);
        }

        void ShowPresetActions(KButton anchor) {
            if (selected == null) throw new InvalidOperationException("请先预览一份清单。");
            bool deletable = PresetCore.IsManagedPresetFile(GameBridge.Library, selectedFile);
            List<KeyValuePair<string, System.Action>> items = new List<KeyValuePair<string, System.Action>>();
            items.Add(MenuItem("导出副本", delegate { CloseDialog(); ExportSelected(); }));
            if (deletable) items.Add(MenuItem("删除清单…", delegate { ShowDeleteConfirmation(anchor); }));
            ShowMenu(anchor, 148f, items);
        }

        void ShowDeleteConfirmation(KButton anchor) {
            if (selected == null || !PresetCore.IsManagedPresetFile(GameBridge.Library, selectedFile))
                throw new InvalidOperationException("这不是清单库中的文件，不能删除。");
            string name = selected.Name;
            ShowAnchoredCard(anchor, 360f, 126f, delegate(RectTransform body) {
                Label(body, "删除“" + name + "”？\n不会修改当前游戏模组。", 0f, 14f, WarningInk);
                RectTransform actions = Row(body, 40f, Color.clear);
                CloneButton(actions, "取消", CloseDialogAction, 88f);
                CloneButton(actions, "删除", DeleteSelectedPreset, 88f);
            });
        }

        void DeleteSelectedPreset() {
            if (!PresetCore.IsManagedPresetFile(GameBridge.Library, selectedFile))
                throw new InvalidOperationException("只能删除清单库中的 JSON 文件。");
            string name = selected == null ? Path.GetFileNameWithoutExtension(selectedFile) : selected.Name;
            PresetCore.DeleteManagedPreset(GameBridge.Library, selectedFile);
            CloseDialog(); SwitchView("library"); Say("已删除清单：" + name);
        }

        void ShowSaveDialog() {
            ShowDialog("保存当前清单", 205f, delegate(RectTransform body) {
                Label(body, "名称", 0f, 14f, MutedInk);
                KInputTextField name = Input(body, "例如：水生地图稳定版", 0f);
                RectTransform actions = Row(body, 40f, Color.clear);
                CloneButton(actions, "取消", CloseDialogAction, 88f);
                CloneButton(actions, "保存", delegate {
                    Preset preset = GameBridge.Capture(name.text.Trim());
                    string file = PresetCore.NewFile(GameBridge.Library);
                    PresetCore.Validate(preset); PresetCore.WriteJson(file, preset);
                    CloseDialog(); Preview(preset, file); Say("清单已保存并选中；不会覆盖同名清单。");
                }, 88f);
            });
        }

        void ShowImportDialog() {
            ShowDialog("导入清单", 225f, delegate(RectTransform body) {
                Label(body, "选择 JSON 文件，或粘贴完整路径", 0f, 14f, MutedInk);
                KInputTextField path = Input(body, "D:\\...\\preset.json", 0f);
                RectTransform actions = Row(body, 40f, Color.clear);
                CloneButton(actions, "取消", CloseDialogAction, 82f);
                CloneButton(actions, "选择文件", delegate {
                    string picked = NativeFiles.PickJson(GameBridge.Library);
                    if (!String.IsNullOrEmpty(picked)) path.text = picked;
                }, 104f);
                CloneButton(actions, "导入并预览", delegate {
                    Preset preset = PresetCore.Read(path.text.Trim().Trim('"'));
                    string file = PresetCore.NewFile(GameBridge.Library);
                    PresetCore.WriteJson(file, preset);
                    CloseDialog(); Preview(preset, file); Say("已导入；尚未修改启用状态。确认预览后再应用。");
                }, 122f);
            });
        }

        void ShowSettingsMenu(KButton anchor) {
            List<KeyValuePair<string, System.Action>> items = new List<KeyValuePair<string, System.Action>>();
            items.Add(MenuItem((settings.AutoBackup ? "✓ " : "□ ") + "自动备份", delegate {
                Settings next = new Settings { AutoBackup = !settings.AutoBackup };
                PresetCore.WriteJson(Path.Combine(GameBridge.Root, "settings.json"), next); settings = next; CloseDialog();
                Say(settings.AutoBackup ? "自动备份已开启，只保留上一次操作前的配置。" : "自动备份已关闭，已有备份会保留。");
            }));
            ShowMenu(anchor, 160f, items);
        }

        void CloseDialogAction() { CloseDialog(); }

        KeyValuePair<string, System.Action> MenuItem(string title, System.Action action) {
            return new KeyValuePair<string, System.Action>(title, action);
        }

        void ShowMenu(KButton anchor, float width, IList<KeyValuePair<string, System.Action>> items) {
            float height = 10f + items.Count * 38f;
            ShowAnchoredCard(anchor, width, height, delegate(RectTransform card) {
                foreach (KeyValuePair<string, System.Action> item in items)
                    CloneButton(card, item.Key, item.Value, 0f);
            });
        }

        void ShowAnchoredCard(KButton anchor, float width, float height, System.Action<RectTransform> build) {
            CloseDialog();
            if (modal == null || anchor == null) return;
            dialogShade = Box(modal.transform, "PopupLayer", new Color(0f, 0f, 0f, 0f)).gameObject;
            Stretch(dialogShade.GetComponent<RectTransform>()); dialogShade.transform.SetAsLastSibling();
            Button outside = dialogShade.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.targetGraphic = null;
            outside.onClick.AddListener(delegate { CloseDialog(); });

            RectTransform card = Box(dialogShade.transform, "Popup", HeaderColor);
            card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.pivot = new Vector2(1f, 1f); card.sizeDelta = new Vector2(width, height);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5); layout.spacing = 4f;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            build(card);
            Canvas.ForceUpdateCanvases();
            Vector3[] corners = new Vector3[4]; (anchor.transform as RectTransform).GetWorldCorners(corners);
            card.position = corners[3] + new Vector3(0f, -4f, 0f);
        }

        void ShowDialog(string title, float height, System.Action<RectTransform> build) {
            CloseDialog();
            dialogShade = Box(modal.transform, "DialogShade", new Color(0f, 0f, 0f, .62f)).gameObject;
            Stretch(dialogShade.GetComponent<RectTransform>()); dialogShade.transform.SetAsLastSibling();
            RectTransform card = Box(dialogShade.transform, title, HeaderColor);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(480f, height);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12); layout.spacing = 8f;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            LocText heading = Label(card, title, 0f, 20f); heading.fontStyle = FontStyles.Bold; Height(heading.gameObject, 30f);
            build(card);
        }

        internal bool CloseDialog() {
            if (dialogShade == null) return false;
            Destroy(dialogShade); dialogShade = null; return true;
        }

        internal void ModalClosed(PresetModal source) {
            if (modal == source) modal = null;
            dialogShade = null; panel = null; rows = null; scroll = null; refreshAt = -1f;
            scrollToTopOnRender = false; scrollToTopFrames = 0;
        }

        void Say(string message) { Say(message, false); }
        void Say(string message, bool warning) {
            if (status != null) { status.text = message; status.color = warning ? WarningInk : MutedInk; }
        }
        bool Matches(string value) { return String.IsNullOrEmpty(search) || (value ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0; }
        static string FormatCreatedUtc(string value) {
            System.DateTime created;
            if (!System.DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out created))
                return "时间未知";
            return created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        void EmptyRow(string message) { LocText text = Label(Row(rows, 66f, RowColor), message, 0f, 16f, MutedInk); text.alignment = TextAlignmentOptions.Center; }

        static void OpenFolder(string folder) {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }

        RectTransform Box(Transform parent, string name, Color color) {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(KImage));
            go.transform.SetParent(parent, false);
            KImage image = go.GetComponent<KImage>(); image.colorStyleSetting = null; image.color = color;
            return go.GetComponent<RectTransform>();
        }

        RectTransform Row(Transform parent, float height, Color color) {
            RectTransform row = Box(parent, "Row", color); Height(row.gameObject, height);
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 8f; group.padding = new RectOffset(8, 8, 4, 4); group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlHeight = true; group.childForceExpandHeight = true;
            group.childControlWidth = true; group.childForceExpandWidth = false;
            return row;
        }

        LocText Label(Transform parent, string value, float width, float size) { return Label(parent, value, width, size, Ink); }
        LocText Label(Transform parent, string value, float width, float size, Color color) {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.SetActive(false); go.transform.SetParent(parent, false);
            LocText text = go.AddComponent<LocText>();
            text.key = ""; text.allowOverride = true; text.textStyleSetting = null;
            text.font = textTemplate.font; text.fontSharedMaterial = textTemplate.fontSharedMaterial;
            text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false; text.richText = false; text.text = value;
            Width(go, width); go.SetActive(true); return text;
        }

        KButton CloneButton(Transform parent, string title, System.Action action, float width) {
            GameObject go = Instantiate(buttonTemplate.gameObject, parent, false); go.name = title;
            KButton button = go.GetComponent<KButton>(); button.ClearOnClick(); button.ClearOnPointerEvents();
            button.onClick += delegate { Guard(action); };
            ToolTip tip = go.GetComponent<ToolTip>(); if (tip != null) tip.toolTip = "";
            LocText label = go.GetComponentsInChildren<LocText>(true).FirstOrDefault();
            if (label != null) { label.key = ""; label.text = title; label.fontSize = Mathf.Min(label.fontSize, 16f); label.alignment = TextAlignmentOptions.Center; }
            Width(go, width); Height(go, 34f); return button;
        }

        KInputTextField Input(Transform parent, string placeholder, float width) {
            RectTransform root = Box(parent, "Input", InputColor); root.gameObject.SetActive(false); Width(root.gameObject, width); Height(root.gameObject, 34f);
            RectTransform viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root, false); Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            LocText text = Label(viewport, "", 0f, 15f); Stretch(text.rectTransform); text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.ScrollRect;
            LocText hint = Label(viewport, placeholder, 0f, 15f, MutedInk); Stretch(hint.rectTransform); hint.enableWordWrapping = false;
            KInputTextField input = root.gameObject.AddComponent<KInputTextField>();
            input.textViewport = viewport; input.textComponent = text; input.placeholder = hint; input.targetGraphic = root.GetComponent<KImage>();
            input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false; input.customCaretColor = true; input.caretColor = Ink;
            root.gameObject.SetActive(true); return input;
        }

        void Check(Transform parent, string title, bool value, System.Action<bool> action, float width) {
            KButton button = null; bool current = value;
            button = CloneButton(parent, (current ? "✓  " : "□  ") + title, delegate {
                current = !current; action(current);
                LocText label = button.GetComponentsInChildren<LocText>(true).FirstOrDefault();
                if (label != null) label.text = (current ? "✓  " : "□  ") + title;
            }, width);
        }

        void StateButton(Transform parent, bool value, System.Action<bool> action, float width) {
            KButton button = null; bool current = value;
            button = CloneButton(parent, current ? "✓  启用" : "停用", delegate {
                current = !current; action(current);
                LocText label = button.GetComponentsInChildren<LocText>(true).FirstOrDefault();
                if (label != null) label.text = current ? "✓  启用" : "停用";
                if (button.bgImage != null) button.bgImage.ColorState = current ? KImage.ColorSelector.Active : KImage.ColorSelector.Inactive;
            }, width);
            if (button.bgImage != null) button.bgImage.ColorState = current ? KImage.ColorSelector.Active : KImage.ColorSelector.Inactive;
        }

        static void ClearChildren(Transform parent) {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) {
                GameObject child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child);
            }
        }

        static void Width(GameObject go, float width) {
            LayoutElement element = go.GetComponent<LayoutElement>(); if (element == null) element = go.AddComponent<LayoutElement>();
            if (width > 0f) { element.preferredWidth = width; element.minWidth = width; element.flexibleWidth = 0f; }
            else { element.minWidth = 40f; element.flexibleWidth = 1f; }
        }
        static void Height(GameObject go, float height) {
            LayoutElement element = go.GetComponent<LayoutElement>(); if (element == null) element = go.AddComponent<LayoutElement>();
            element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0f;
        }
        static void Stretch(RectTransform rect) { Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); }
        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
    }

    public sealed class MinimumScrollbarHandle : MonoBehaviour {
        public float MinimumPixels = 24f;
        Scrollbar scrollbar;

        void OnEnable() {
            scrollbar = GetComponent<Scrollbar>();
            Canvas.willRenderCanvases += EnforceMinimum;
        }

        void OnDisable() { Canvas.willRenderCanvases -= EnforceMinimum; }

        void EnforceMinimum() {
            if (scrollbar == null || scrollbar.handleRect == null) return;
            RectTransform sliding = scrollbar.handleRect.parent as RectTransform;
            if (sliding == null || sliding.rect.height <= 0f) return;
            scrollbar.size = Mathf.Max(scrollbar.size, Mathf.Clamp01(MinimumPixels / sliding.rect.height));
        }
    }

    public sealed class PresetModal : KModalScreen {
        internal PresetPanel Owner;
        public override void OnKeyDown(KButtonEvent e) {
            if (!e.Consumed && e.IsAction(Action.Escape) && Owner != null && Owner.CloseDialog()) { e.Consumed = true; return; }
            base.OnKeyDown(e);
        }
        protected override void OnDeactivate() {
            base.OnDeactivate();
            PresetPanel owner = Owner; Owner = null;
            if (owner != null) owner.ModalClosed(this);
        }
    }
}
