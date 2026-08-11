namespace Scp.Godot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 中文：主标题设置页，使用临时副本编辑并在应用时原子保存；取消始终丢弃临时值并返回主标题。
    /// English: Main-title settings screen editing a temporary clone and atomically saving only on Apply; Cancel always discards the clone and returns to the title.
    /// 控制对象：六分类导航、显示/效果/音频/控制/语言控件和重绑定冲突检测。
    /// Controlled objects: six category navigation, display/effects/audio/controls/language widgets and same-device rebinding conflict checks.
    /// </summary>
    public sealed partial class SettingsScreen : Control
    {
        private static readonly string[] Categories = { "显示", "界面与字体", "动态效果与无障碍", "音频", "控制", "语言与本地化" };
        private static readonly string[] Actions = { "confirm", "cancel", "pause", "move_up", "move_down", "move_left", "move_right", "map_zoom", "map_pan", "time_speed" };
        private ApplicationSettingsStore _store = null!;
        private ApplicationSettings _applied = null!;
        private ApplicationSettings _draft = null!;
        private VBoxContainer _content = null!;
        private Label _status = null!;
        private int _category;
        private readonly Dictionary<string, OptionButton> _options = new();
        private readonly Dictionary<string, HSlider> _sliders = new();
        private readonly Dictionary<string, CheckButton> _checks = new();
        private readonly Dictionary<string, LineEdit> _bindings = new();
        private ConfirmationDialog _displayConfirmation = null!;
        private Timer _displayConfirmationTimer = null!;
        // 中文：全局音频管理器统一承担按钮反馈和四总线预览，避免设置页维护第二套音频状态。
        // English: The global audio manager owns button feedback and four-bus previews so the settings screen never maintains duplicate audio state.
        private AudioManager _audio = null!;
        private bool _awaitingDisplayConfirmation;
        private ApplicationSettings _previousDisplaySettings = null!;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            _audio = GetNode<AudioManager>("/root/AudioManager");
            _store = new ApplicationSettingsStore(ProjectSettings.GlobalizePath("user://settings/settings.json"));
            _applied = _store.Load();
            _draft = _applied.Clone();
            _audio.ApplySettings(_applied);
            Theme = CreateTheme();
            BuildUi();
            RenderCategory();
        }

        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), GodotArt.OverseerBackground);

        private void BuildUi()
        {
            var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 34, OffsetTop = 24, OffsetRight = -34, OffsetBottom = -24 };
            AddChild(margin);
            var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
            root.AddChild(new Label { Text = "设置 / SETTINGS", HorizontalAlignment = HorizontalAlignment.Center });
            root.AddChild(new HSeparator());
            var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; root.AddChild(body);
            var navigation = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) }; navigation.AddThemeConstantOverride("separation", 4); body.AddChild(navigation);
            for (int i = 0; i < Categories.Length; i++) { int index = i; var button = new Button { Text = Categories[i], Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.All }; _audio.BindButton(button); button.Pressed += () => { _category = index; RenderCategory(); }; navigation.AddChild(button); }
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; panel.AddThemeStyleboxOverride("panel", Box(GodotArt.OverseerPanel, GodotArt.OverseerRule, 1)); body.AddChild(panel);
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; panel.AddChild(scroll);
            _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _content.AddThemeConstantOverride("separation", 8); scroll.AddChild(_content);
            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; root.AddChild(actions);
            var cancel = new Button { Text = "取消", FocusMode = FocusModeEnum.All }; cancel.Pressed += Cancel; actions.AddChild(cancel);
            var defaults = new Button { Text = "恢复默认", FocusMode = FocusModeEnum.All }; defaults.Pressed += RestoreDefaults; actions.AddChild(defaults);
            var apply = new Button { Text = "应用", FocusMode = FocusModeEnum.All }; apply.Pressed += Apply; actions.AddChild(apply);
            _audio.BindButton(cancel);
            _audio.BindButton(defaults);
            _audio.BindButton(apply);
            _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart }; root.AddChild(_status);

            // 中文：显示模式会立刻影响窗口可见性，因此应用后必须在十秒内确认；取消或超时均恢复进入页面前的有效显示快照。
            // English: Display changes immediately affect window visibility, so Apply requires confirmation within ten seconds; cancel or timeout restores the valid snapshot captured on entry.
            _displayConfirmation = new ConfirmationDialog { Title = "确认显示设置", OkButtonText = "保留设置", CancelButtonText = "恢复" };
            _displayConfirmation.Confirmed += ConfirmDisplaySettings;
            _displayConfirmation.Canceled += RevertDisplaySettings;
            AddChild(_displayConfirmation);
            _displayConfirmationTimer = new Timer { OneShot = true, WaitTime = 10.0 };
            _displayConfirmationTimer.Timeout += RevertDisplaySettings;
            AddChild(_displayConfirmationTimer);
        }

        private void RenderCategory()
        {
            foreach (Node child in _content.GetChildren()) child.QueueFree();
            _options.Clear(); _sliders.Clear(); _checks.Clear(); _bindings.Clear();
            _content.AddChild(new Label { Text = Categories[_category] + " / " + (Categories[_category] == "显示" ? "DISPLAY" : "CONFIGURATION") });
            _content.AddChild(new HSeparator());
            switch (_category)
            {
                case 0: RenderDisplay(); break;
                case 1: RenderInterface(); break;
                case 2: RenderEffects(); break;
                case 3: RenderAudio(); break;
                case 4: RenderControls(); break;
                case 5: RenderLanguage(); break;
            }
        }

        private void RenderDisplay()
        {
            int modeIndex = _draft.Borderless ? 3 : _draft.WindowMode == "maximized" ? 1 : _draft.WindowMode == "fullscreen" ? 2 : 0;
            AddOption("window_mode", "窗口模式", new[] { "窗口化", "最大化", "全屏", "无边框窗口" }, modeIndex, index =>
            {
                _draft.WindowMode = index == 1 ? "maximized" : index == 2 ? "fullscreen" : "windowed";
                _draft.Borderless = index == 3;
                ApplyPreview();
            });
            AddOption("resolution", "分辨率", new[] { "1280 × 720", "1600 × 900", "1920 × 1080" }, _draft.WindowWidth == 1600 ? 1 : _draft.WindowWidth == 1920 ? 2 : 0, index => { int[] widths = { 1280, 1600, 1920 }; int[] heights = { 720, 900, 1080 }; _draft.WindowWidth = widths[index]; _draft.WindowHeight = heights[index]; ApplyPreview(); });
            AddCheck("vsync", "VSync", _draft.VSync, value => { _draft.VSync = value; ApplyPreview(); });
        }

        private void RenderInterface() => AddOption("ui_scale", "UI 缩放", new[] { "80%", "90%", "100%", "110%", "125%", "150%" }, Array.IndexOf(new[] { 80, 90, 100, 110, 125, 150 }, _draft.UiScalePercent), index => { _draft.UiScalePercent = new[] { 80, 90, 100, 110, 125, 150 }[index]; ApplyPreview(); });
        private void RenderEffects()
        {
            AddCheck("dynamic_background", "动态背景", _draft.DynamicBackground, value => { _draft.DynamicBackground = value; ApplyPreview(); });
            AddCheck("scanlines", "扫描线", _draft.Scanlines, value => { _draft.Scanlines = value; ApplyPreview(); });
            AddCheck("interface_animations", "界面动画", _draft.InterfaceAnimations, value => { _draft.InterfaceAnimations = value; ApplyPreview(); });
            AddCheck("crisis_flicker", "危机闪烁", _draft.CrisisFlicker, value => { _draft.CrisisFlicker = value; ApplyPreview(); });
            AddCheck("high_contrast_focus", "高对比焦点", _draft.HighContrastFocus, value => { _draft.HighContrastFocus = value; ApplyPreview(); });
            AddCheck("reduce_motion", "减少动态效果", _draft.ReduceMotion, value => { _draft.ReduceMotion = value; ApplyPreview(); });
        }

        private void RenderAudio()
        {
            AddSlider("master", "Master 音量", _draft.MasterVolume, value => { _draft.MasterVolume = (int)value; ApplyPreview(); }); AddCheck("master_mute", "Master 静音", _draft.MasterMuted, value => { _draft.MasterMuted = value; ApplyPreview(); });
            AddSlider("music", "Music 音量", _draft.MusicVolume, value => { _draft.MusicVolume = (int)value; ApplyPreview(); }); AddCheck("music_mute", "Music 静音", _draft.MusicMuted, value => { _draft.MusicMuted = value; ApplyPreview(); });
            AddSlider("ambience", "Ambience 音量", _draft.AmbienceVolume, value => { _draft.AmbienceVolume = (int)value; ApplyPreview(); }); AddCheck("ambience_mute", "Ambience 静音", _draft.AmbienceMuted, value => { _draft.AmbienceMuted = value; ApplyPreview(); });
            // 中文：Dialogue 独立控制不可辨识会议语音；将其静音不会移除完整字幕。
            // English: Dialogue independently controls unintelligible council voices; muting it never removes full subtitles.
            AddSlider("dialogue", "Voice / Dialogue 音量", _draft.DialogueVolume, value => { _draft.DialogueVolume = (int)value; ApplyPreview(); }); AddCheck("dialogue_mute", "Voice / Dialogue 静音", _draft.DialogueMuted, value => { _draft.DialogueMuted = value; ApplyPreview(); });
            AddSlider("ui", "UI 音量", _draft.UiVolume, value => { _draft.UiVolume = (int)value; ApplyPreview(); }); AddCheck("ui_mute", "UI 静音", _draft.UiMuted, value => { _draft.UiMuted = value; ApplyPreview(); });
        }

        private void RenderControls()
        {
            _content.AddChild(new Label { Text = "核心动作 · 选择键位文本后输入新键名；同设备重复键位会被拒绝。" });
            foreach (string action in Actions) { var row = new HBoxContainer(); row.AddChild(new Label { Text = ActionLabel(action), CustomMinimumSize = new Vector2(250, 0) }); var edit = new LineEdit { Text = _draft.KeyBindings[action], CustomMinimumSize = new Vector2(250, 34) }; edit.TextSubmitted += value => Rebind(action, value); row.AddChild(edit); _bindings[action] = edit; _content.AddChild(row); }
        }

        private void RenderLanguage() => AddOption("language", "语言", new[] { "简体中文", "繁體中文（香港）", "English" }, _draft.Language == "zh_HK" ? 1 : _draft.Language == "en" ? 2 : 0, index => { _draft.Language = new[] { "zh_CN", "zh_HK", "en" }[index]; RenderCategory(); });

        private void AddOption(string key, string label, string[] values, int selected, Action<int> changed)
        {
            _content.AddChild(new Label { Text = label }); var option = new OptionButton(); _audio.BindButton(option); foreach (string value in values) option.AddItem(value); option.Select(Math.Max(0, selected)); option.ItemSelected += index => changed((int)index); _options[key] = option; _content.AddChild(option);
        }
        private void AddCheck(string key, string label, bool value, Action<bool> changed) { var check = new CheckButton { Text = label, ButtonPressed = value }; _audio.BindButton(check); check.Toggled += value => changed(value); _checks[key] = check; _content.AddChild(check); }
        private void AddSlider(string key, string label, int value, Action<double> changed) { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(220, 0) }); var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill }; slider.ValueChanged += value => changed(value); _sliders[key] = slider; row.AddChild(slider); _content.AddChild(row); }

        private void Rebind(string action, string value)
        {
            value = value.Trim(); if (value.Length == 0) { _audio.PlayUiWarning(); _status.Text = "键位不能为空。"; return; }
            string? conflict = _draft.KeyBindings.FirstOrDefault(pair => pair.Key != action && string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase)).Key;
            if (conflict != null) { _audio.PlayUiWarning(); _status.Text = "同设备冲突：" + ActionLabel(conflict) + " 已使用 " + value + "。"; _bindings[action].Text = _draft.KeyBindings[action]; return; }
            _draft.KeyBindings[action] = value; _status.Text = "已更新临时键位：" + ActionLabel(action); 
        }

        private void RestoreDefaults() { _draft = ApplicationSettings.CreateDefault(); RenderCategory(); _status.Text = "已恢复默认值，但尚未保存。"; }
        private void Apply()
        {
            try
            {
                ApplicationSettings previous = _applied.Clone();
                _store.Save(_draft);
                _previousDisplaySettings = previous;
                _applied = _draft.Clone();
                ApplyEngineSettings(_applied);
                _audio.ApplySettings(_applied);
                if (previous.WindowMode != _applied.WindowMode
                    || previous.WindowWidth != _applied.WindowWidth
                    || previous.WindowHeight != _applied.WindowHeight
                    || previous.Borderless != _applied.Borderless)
                {
                    _awaitingDisplayConfirmation = true;
                    _displayConfirmation.DialogText = "显示设置已预览。十秒内点击“保留设置”，否则自动恢复。";
                    _displayConfirmation.PopupCentered();
                    _displayConfirmationTimer.Start();
                    return;
                }
                ChangeScene("res://Main.tscn", "无法返回主标题，请稍后重试。");
            }
            catch (Exception exception) { GD.PrintErr("Settings save failed: " + exception); _status.Text = "设置保存失败，请检查存储权限。"; }
        }

        private void ConfirmDisplaySettings()
        {
            _awaitingDisplayConfirmation = false;
            _displayConfirmationTimer.Stop();
            ChangeScene("res://Main.tscn", "无法返回主标题，请稍后重试。");
        }

        private void RevertDisplaySettings()
        {
            if (!_awaitingDisplayConfirmation) return;
            _awaitingDisplayConfirmation = false;
            _displayConfirmationTimer.Stop();
            _applied = _previousDisplaySettings.Clone();
            _draft = _applied.Clone();
            _store.Save(_applied);
            ApplyEngineSettings(_applied);
            _audio.ApplySettings(_applied);
            _audio.PlayUiWarning();
            _status.Text = "显示设置已恢复。";
        }

        private void Cancel()
        {
            _draft = _applied.Clone();
            ApplyEngineSettings(_applied);
            _audio.ApplySettings(_applied);
            ChangeScene("res://Main.tscn", "无法返回主标题，请稍后重试。");
        }

        /// <summary>
        /// 中文：将已应用或临时快照映射到 Godot 窗口、音频总线和界面缩放；设置页外不承担全项目语言迁移。
        /// English: Maps an applied or draft snapshot to the Godot window, audio buses and UI scale; full-project localization remains outside this page.
        ///
        /// 中文：临时副本可以即时预览，但不会在此方法中写入设置文件；取消时由已应用快照反向恢复。
        /// English: A draft can be previewed immediately, but this method never writes the settings file; Cancel restores from the applied snapshot.
        /// 参数与单位：settings 的窗口尺寸为像素，缩放和音量为百分比；减少动态效果由具体表现层读取。
        /// Parameters and units: window dimensions are pixels, scale and volume are percentages; individual presentation layers read motion flags.
        /// </summary>
        private void ApplyPreview()
        {
            ApplyEngineSettings(_draft);
            _audio.ApplySettings(_draft);
        }

        private void ApplyEngineSettings(ApplicationSettings settings)
        {
            DisplayServer.WindowSetMode(settings.WindowMode == "fullscreen" ? DisplayServer.WindowMode.Fullscreen : settings.WindowMode == "maximized" ? DisplayServer.WindowMode.Maximized : DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(settings.WindowWidth, settings.WindowHeight)); DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, settings.Borderless);
            DisplayServer.WindowSetVsyncMode(settings.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            GetTree().Root.ContentScaleFactor = settings.UiScalePercent / 100.0f;
            _audio.ApplySettings(settings);
            foreach (var pair in settings.KeyBindings) if (InputMap.HasAction(pair.Key)) { InputMap.ActionEraseEvents(pair.Key); InputEvent? inputEvent = CreateInputEvent(pair.Value); if (inputEvent != null) InputMap.ActionAddEvent(pair.Key, inputEvent); }
        }
        /// <summary>中文：将设置页的稳定显示文本转换成基础键盘或鼠标事件；未知文本返回空并保留项目默认动作。English: Converts stable settings labels into basic keyboard or mouse events; unknown labels return null and preserve the project default action.</summary>
        private static InputEvent? CreateInputEvent(string binding)
        {
            if (binding.Equals("Mouse Wheel", StringComparison.OrdinalIgnoreCase)) return new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp };
            if (binding.Equals("Middle Mouse", StringComparison.OrdinalIgnoreCase)) return new InputEventMouseButton { ButtonIndex = MouseButton.Middle };
            Key key = binding.ToUpperInvariant() switch { "ENTER" => Key.Enter, "ESCAPE" => Key.Escape, "SPACE" => Key.Space, "P" => Key.P, "W" => Key.W, "A" => Key.A, "S" => Key.S, "D" => Key.D, _ => Key.None };
            return key == Key.None ? null : new InputEventKey { PhysicalKeycode = key };
        }
        private static int GetVolume(ApplicationSettings s, string bus) => bus switch { "Master" => s.MasterVolume, "Music" => s.MusicVolume, "Ambience" => s.AmbienceVolume, _ => s.UiVolume };
        private static bool GetMute(ApplicationSettings s, string bus) => bus switch { "Master" => s.MasterMuted, "Music" => s.MusicMuted, "Ambience" => s.AmbienceMuted, _ => s.UiMuted };
        private static string ActionLabel(string action) => action switch { "confirm" => "确认", "cancel" => "返回", "pause" => "暂停", "move_up" => "向上", "move_down" => "向下", "move_left" => "向左", "move_right" => "向右", "map_zoom" => "地图缩放", "map_pan" => "地图拖动", _ => "时间速度" };
        private void ChangeScene(string path, string message) { Error error = GetTree().ChangeSceneToFile(path); if (error != Error.Ok) { GD.PrintErr("Settings scene change failed: " + path + " error=" + error); _status.Text = message; } }
        private static Theme CreateTheme() { var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } }; var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 }; theme.SetColor("font_color", "Label", new Color("d8d8dc")); theme.SetColor("font_color", "Button", new Color("d8d8dc")); theme.SetColor("font_hover_color", "Button", Colors.White); return theme; }
        private static StyleBoxFlat Box(Color fill, Color border, int width) { var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12 }; box.SetBorderWidthAll(width); return box; }
    }
}
