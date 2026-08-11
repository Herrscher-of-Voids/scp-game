namespace Scp.Godot
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using global::Godot;
    using Scp.Application;
    using Scp.Domain;

    /// <summary>
    /// 中文：控制读取存档页面的目录列表、筛选排序、详情、载入和删除；所有玩家文字不包含绝对路径、异常消息或堆栈。
    /// English: Controls the save directory list, filters, sorting, details, loading and deletion; player text never contains absolute paths, exception messages or stack traces.
    /// 参数与单位：时间来自 UTC 元数据，Tick 为游戏小时；布局使用响应式容器，基准视口为 1280×720。
    /// Parameters and units: time comes from UTC metadata and Tick is an in-game hour; responsive containers target a 1280×720 viewport.
    /// 边界与确定性：空目录、筛选无结果、损坏、过新和终局档均保留明确状态；列表排序使用稳定的 SaveId 兜底。
    /// Edge cases and determinism: empty directories, empty filters, corruption, future versions and ended saves remain explicit; SaveId provides stable ordering.
    /// </summary>
    public sealed partial class LoadGameScreen : Control
    {
        private SaveRepository _repository = null!;
        private SaveDirectoryEntry[] _entries = Array.Empty<SaveDirectoryEntry>();
        private SaveDirectoryEntry? _selected;
        private VBoxContainer _list = null!;
        private Label _details = null!;
        private Label _status = null!;
        private OptionButton _sort = null!;
        private OptionButton _difficulty = null!;
        private OptionButton _state = null!;
        private LineEdit _search = null!;
        private Button _load = null!;
        private Button _backup = null!;
        private Button _delete = null!;
        private Button _refresh = null!;
        private ConfirmationDialog _confirm = null!;
        private LineEdit _confirmInput = null!;
        private string _confirmSaveId = string.Empty;
        private bool _confirmBackup;
        private bool _confirmStrong;
        private bool _busy;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            _repository = GameLaunchContext.CreateRepository();
            BuildUi();
            RefreshEntries();
        }

        private void BuildUi()
        {
            var background = new PanelContainer();
            background.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddThemeStyleboxOverride("panel", Box(new Color("060607"), new Color("77777e"), 0));
            AddChild(background);
            var margin = new MarginContainer { OffsetLeft = 28, OffsetTop = 22, OffsetRight = -28, OffsetBottom = -22 };
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 8);
            margin.AddChild(root);
            root.AddChild(new Label { Text = "读取存档 / LOAD ARCHIVE", HorizontalAlignment = HorizontalAlignment.Center });
            root.AddChild(new HSeparator());
            root.AddChild(BuildToolbar());
            var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            body.AddChild(BuildListPanel());
            body.AddChild(BuildDetailsPanel());
            root.AddChild(body);
            root.AddChild(BuildActions());
            _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_status);
            BuildConfirmation();
        }

        private Control BuildToolbar()
        {
            var bar = new HBoxContainer();
            bar.AddChild(new Label { Text = "排序" });
            _sort = AddOptions(bar, new[] { "最近保存", "创建时间", "名称", "难度" });
            bar.AddChild(new Label { Text = "难度" });
            _difficulty = AddOptions(bar, new[] { "全部", "Easy", "Normal", "Hard", "Realistic", "未知" });
            bar.AddChild(new Label { Text = "状态" });
            _state = AddOptions(bar, new[] { "全部", "可载入", "任命待确认", "备份可恢复", "终局", "异常/不兼容" });
            _search = new LineEdit { PlaceholderText = "按存档名搜索", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClearButtonEnabled = true };
            bar.AddChild(_search);
            _refresh = new Button { Text = "刷新", FocusMode = FocusModeEnum.All };
            _refresh.Pressed += RefreshEntries;
            bar.AddChild(_refresh);
            _sort.ItemSelected += _ => RenderEntries();
            _difficulty.ItemSelected += _ => RenderEntries();
            _state.ItemSelected += _ => RenderEntries();
            _search.TextChanged += _ => RenderEntries();
            return bar;
        }

        private Control BuildListPanel()
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.58f };
            panel.AddThemeStyleboxOverride("panel", Box(new Color("101013"), new Color("77777e"), 1));
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            panel.AddChild(scroll);
            _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _list.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(_list);
            return panel;
        }

        private Control BuildDetailsPanel()
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.42f };
            panel.AddThemeStyleboxOverride("panel", Box(new Color("101013"), new Color("77777e"), 1));
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            panel.AddChild(scroll);
            _details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(_details);
            return panel;
        }

        private Control BuildActions()
        {
            var bar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var back = new Button { Text = "返回主标题", FocusMode = FocusModeEnum.All };
            back.Pressed += ReturnToTitle;
            bar.AddChild(back);
            _load = new Button { Text = "载入", FocusMode = FocusModeEnum.All };
            _load.Pressed += LoadPrimary;
            bar.AddChild(_load);
            _backup = new Button { Text = "载入备份", FocusMode = FocusModeEnum.All };
            _backup.Pressed += AskBackup;
            bar.AddChild(_backup);
            _delete = new Button { Text = "删除存档", FocusMode = FocusModeEnum.All };
            _delete.Pressed += AskDelete;
            bar.AddChild(_delete);
            return bar;
        }

        private void BuildConfirmation()
        {
            _confirm = new ConfirmationDialog { Title = "操作确认", OkButtonText = "确认", CancelButtonText = "返回" };
            _confirm.Confirmed += ConfirmAction;
            _confirm.Canceled += ClearConfirmation;
            _confirmInput = new LineEdit { PlaceholderText = "输入完整存档名或 SaveId" };
            _confirm.AddChild(_confirmInput);
            AddChild(_confirm);
        }

        private void RefreshEntries()
        {
            if (_busy) return;
            _entries = _repository.EnumerateDirectory();
            _selected = _entries.FirstOrDefault(entry => entry.SaveId == _selected?.SaveId) ?? _entries.FirstOrDefault();
            RenderEntries();
        }

        private void RenderEntries()
        {
            foreach (Node child in _list.GetChildren()) child.QueueFree();
            IEnumerable<SaveDirectoryEntry> filtered = _entries;
            string search = _search?.Text.Trim() ?? string.Empty;
            if (search.Length > 0) filtered = filtered.Where(entry => entry.Metadata.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (_difficulty != null && _difficulty.Selected > 0)
            {
                GameDifficulty difficulty = _difficulty.Selected == 5 ? GameDifficulty.Unknown : (GameDifficulty)(_difficulty.Selected - 1);
                filtered = filtered.Where(entry => entry.Metadata.Difficulty == difficulty);
            }
            if (_state != null && _state.Selected > 0) filtered = filtered.Where(MatchesState);
            filtered = SortEntries(filtered);
            SaveDirectoryEntry[] visible = filtered.ToArray();
            if (visible.Length == 0) _list.AddChild(new Label { Text = _entries.Length == 0 ? "没有存档。" : "没有符合筛选条件的存档。" });
            foreach (SaveDirectoryEntry entry in visible)
            {
                var button = new Button { Text = FormatRow(entry), Alignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = FocusModeEnum.All, ButtonPressed = entry.SaveId == _selected?.SaveId };
                button.Pressed += () => Select(entry);
                _list.AddChild(button);
            }
            UpdateDetails();
        }

        private IEnumerable<SaveDirectoryEntry> SortEntries(IEnumerable<SaveDirectoryEntry> entries)
        {
            return _sort.Selected switch
            {
                1 => entries.OrderByDescending(entry => entry.Metadata.SavedAtUtc).ThenByDescending(entry => entry.Metadata.CreatedAtUtc).ThenBy(entry => entry.SaveId, StringComparer.Ordinal),
                2 => entries.OrderByDescending(entry => entry.Metadata.CreatedAtUtc).ThenByDescending(entry => entry.Metadata.SavedAtUtc).ThenBy(entry => entry.SaveId, StringComparer.Ordinal),
                3 => entries.OrderBy(entry => entry.Metadata.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.SaveId, StringComparer.Ordinal),
                _ => entries.OrderBy(entry => entry.Metadata.Difficulty).ThenBy(entry => entry.Metadata.CreatedAtUtc).ThenBy(entry => entry.SaveId, StringComparer.Ordinal)
            };
        }

        private bool MatchesState(SaveDirectoryEntry entry)
        {
            return _state.Selected switch
            {
                1 => entry.PrimaryState == SaveFileState.Available && !entry.Metadata.IsEnded,
                2 => entry.PrimaryState == SaveFileState.Available && !entry.Metadata.BriefingAcknowledged,
                3 => entry.PrimaryState == SaveFileState.InvalidOrCorrupt && entry.BackupState == SaveFileState.Available,
                4 => entry.Metadata.IsEnded,
                _ => entry.PrimaryState != SaveFileState.Available && !(entry.PrimaryState == SaveFileState.InvalidOrCorrupt && entry.BackupState == SaveFileState.Available)
            };
        }

        private void Select(SaveDirectoryEntry entry) { _selected = entry; RenderEntries(); }

        private string FormatRow(SaveDirectoryEntry entry)
        {
            SaveFileMetadata metadata = entry.Metadata;
            string name = metadata.DisplayName.Length == 0 ? entry.SaveId : metadata.DisplayName;
            return (entry.SaveId == _selected?.SaveId ? "▶ " : "  ") + name + " · " + metadata.Identity + " · " + metadata.Difficulty + " · 周期 " + metadata.CurrentCycle.ToString(CultureInfo.InvariantCulture) + " · " + FoundationCalendar.FormatYearMonth(metadata.CalendarYear, metadata.CalendarMonth) + " 第 " + (metadata.DayOfCycle + 1).ToString(CultureInfo.InvariantCulture) + " 天 · " + entry.StatusMessage;
        }

        private void UpdateDetails()
        {
            if (_selected == null) { _details.Text = "请选择一个存档。"; SetButtons(); return; }
            SaveFileMetadata m = _selected.Metadata;
            _details.Text = "存档详情\n\nSaveId：" + _selected.SaveId + "\n名称：" + (m.DisplayName.Length == 0 ? "（不可读）" : m.DisplayName) + "\n身份：" + m.Identity + "\n难度：" + m.Difficulty + "\n种子：" + m.Seed + "\n模式 / 类型：" + m.Mode + " / " + m.SaveKind + "\n创建：" + m.CreatedAtUtc.ToString("u") + "\n保存：" + m.SavedAtUtc.ToString("u") + "\n游戏版本：" + m.GameVersion + "\nSchema：v" + (_selected.PrimaryState == SaveFileState.IncompatibleVersion ? "过新" : "5") + "\n主档：" + _selected.PrimaryState + "\n备份：" + _selected.BackupState + "\n任命：" + (m.BriefingAcknowledged ? "已确认" : "待确认") + "\nO5席位：" + (m.O5Seat.Length == 0 ? "（无）" : m.O5Seat) + "\nTick：" + m.WorldTick + "\n周期：" + m.CurrentCycle + "\n游戏内日期：" + FoundationCalendar.FormatYearMonth(m.CalendarYear, m.CalendarMonth) + " 第 " + (m.DayOfCycle + 1) + " 天\n终局：" + (m.IsEnded ? m.EndReason.ToString() : "否") + "\n\n状态：" + _selected.StatusMessage;
            SetButtons();
        }

        private void SetButtons()
        {
            bool primary = _selected?.PrimaryState == SaveFileState.Available && !_selected.Metadata.IsEnded;
            bool backup = _selected?.PrimaryState == SaveFileState.InvalidOrCorrupt && _selected.BackupState == SaveFileState.Available;
            _load.Disabled = _busy || !primary;
            _backup.Disabled = _busy || !backup;
            _delete.Disabled = _busy || _selected == null;
        }

        private void LoadPrimary() => StartLoad(false);
        private void AskBackup() { if (_selected == null) return; _confirmBackup = true; _confirmStrong = false; _confirmSaveId = _selected.SaveId; ShowConfirmation("主档已损坏。载入备份会回退到上一保存版本，最近进度可能丢失。是否确认？", false); }
        private void AskDelete()
        {
            if (_selected == null) return;
            _confirmBackup = false; _confirmSaveId = _selected.SaveId; _confirmStrong = _selected.Metadata.Difficulty == GameDifficulty.Realistic || _selected.PrimaryState != SaveFileState.Available;
            ShowConfirmation(_confirmStrong ? "这是真实难度或异常存档。将删除整个存档及备份，请输入确认内容。" : "将删除整个存档及备份，且无法恢复。是否确认？", _confirmStrong);
        }

        private void ShowConfirmation(string text, bool strong)
        {
            _confirmInput.Visible = strong;
            _confirmInput.Text = string.Empty;
            _confirm.DialogText = strong ? text + "\n请输入：" + (_selected?.Metadata.DisplayName.Length > 0 ? _selected.Metadata.DisplayName : _confirmSaveId) : text;
            _confirm.PopupCentered();
        }

        private void ConfirmAction()
        {
            if (_selected == null) return;
            if (_confirmStrong)
            {
                string expected = _selected.Metadata.DisplayName.Length > 0 ? _selected.Metadata.DisplayName : _confirmSaveId;
                if (!string.Equals(expected, _confirmInput.Text, StringComparison.Ordinal)) { SetStatus("确认内容不匹配，未执行删除。"); ClearConfirmation(); return; }
            }
            if (_confirmBackup) StartLoad(true); else DeleteSelected();
            ClearConfirmation();
        }

        private void ClearConfirmation() { _confirmBackup = false; _confirmStrong = false; _confirmSaveId = string.Empty; }

        /// <summary>
        /// 中文：把玩家明确选择的主档或备份作为精确一次性工作提交给统一加载页；实际验证、读取和最近目标更新均在加载页成功路径中完成。
        /// English: Submits the explicitly selected primary or backup as exact one-shot work to the unified loader; validation, reading, and latest-target update occur only on the loader's success path.
        /// 参数/返回/边界：useBackup 精确选择备份且禁止回退；方法无返回值，切换失败保留本页。
        /// Parameter/return/boundary: useBackup selects backup exactly and forbids fallback; the method returns nothing and keeps this page if transition fails.
        /// </summary>
        private void StartLoad(bool useBackup)
        {
            if (_selected == null || _busy) return;
            GameLaunchContext.SetWork(new GameLaunchRequest { Kind = useBackup ? GameLaunchKind.BackupContinue : GameLaunchKind.ContinueGame, SaveId = _selected.SaveId, UpdateLatest = true, ReturnScene = "res://LoadGame.tscn" });
            Error error = GetTree().ChangeSceneToFile("res://TerminalLoading.tscn");
            if (error != Error.Ok) { GD.PrintErr("LoadGame loading scene change failed: " + error); SetStatus("无法建立终端接入，请稍后重试。"); }
        }

        private void DeleteSelected()
        {
            if (_selected == null) return;
            _busy = true; SetButtons();
            SaveDirectoryOperationResult result = _repository.DeleteSave(_selected.SaveId);
            _busy = false;
            SetStatus(result.Message);
            _selected = null;
            RefreshEntries();
        }

        private void ReturnToTitle() { if (_busy) return; Error error = GetTree().ChangeSceneToFile("res://Main.tscn"); if (error != Error.Ok) { GD.PrintErr("LoadGame title scene change failed: " + error); SetStatus("无法返回主标题，请稍后重试。"); } }

        private static OptionButton AddOptions(Control parent, string[] labels) { var option = new OptionButton(); foreach (string label in labels) option.AddItem(label); parent.AddChild(option); return option; }
        private static Theme CreateTheme() { var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } }; var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 }; theme.SetColor("font_color", "Label", new Color("d8d8dc")); theme.SetColor("font_color", "Button", new Color("d8d8dc")); theme.SetColor("font_hover_color", "Button", Colors.White); theme.SetColor("font_focus_color", "Button", Colors.White); return theme; }
        private static StyleBoxFlat Box(Color fill, Color border, int width) { var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 }; box.SetBorderWidthAll(width); return box; }
        private void SetStatus(string message) { _status.Text = message; _status.AddThemeColorOverride("font_color", message.Contains("损坏") || message.Contains("删除") ? new Color("e65a5a") : new Color("aaa8ae")); }
    }
}
