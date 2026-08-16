namespace Scp.Godot
{
    using System;
    using global::Godot;

    public sealed partial class TimelineSelectionScreen : Control
    {
        /// <summary>
        /// 中文：控制三种时间线的全屏选择、导航和安全故障转场；内部使用 SaveMode 稳定语义，当前阶段不创建串联或事件存档。
        /// English: Controls full-screen selection, navigation and safe signal-failure transitions for three timelines; SaveMode remains the stable internal semantic and this phase creates no chained or event save.
        /// 参数与单位：转场和滚轮冷却使用现实秒；页面尺寸使用 Godot 逻辑像素；所有切换不消费游戏随机流。
        /// Parameters and units: transition and wheel cooldown use real seconds; layout uses Godot logical pixels; selection never consumes game randomness.
        /// 返回值与边界：按钮通过场景切换产生界面副作用；当前只有新生时间线可进入，延续与破碎时间线均被禁用。
        /// Return and boundaries: buttons cause scene changes; only the new timeline is currently enterable, while continuation and broken timelines remain disabled.
        /// 确定性与原因：模式顺序、文案、默认选择和噪点种子固定，保证相同输入产生相同页面状态。
        /// Determinism and rationale: order, copy, default selection and grain seed are fixed so identical input produces identical page state.
        /// </summary>
        private const double TransitionSeconds = 0.24;
        private const double WheelCooldownSeconds = 0.34;
        private static int _returnSelection;
        private readonly string[] _names = { "新生时间线", "延续时间线", "破碎时间线" };
        private readonly string[] _tags = { "NEW / STANDALONE", "CONTINUATION / CHAINED", "EVENT ARCHIVE" };
        private readonly string[] _descriptions =
        {
            "开启一条全新的时间线。",
            "回到原本的时间线，以不同身份继续前行。",
            "进入一条已经毁灭的时间线，重新经历他们的人生。\n也许，你能拯救他们。"
        };
        // 中文：玩家可见状态只表达当前时间线是否可进入；禁止在游戏内暴露开发计划、内容接入、权限实现或其他内部资料。
        // English: Player-facing states only indicate whether a timeline can be entered; development plans, content intake, access implementation, and other internal material must never appear in game.
        private readonly string[] _states =
        {
            "可用 · 建立新的基金会历史",
            "尚未开放",
            "尚未开放"
        };

        private AudioManager _audio = null!;
        private Label _index = null!;
        private Label _tag = null!;
        private Button _name = null!;
        private Label _description = null!;
        private Label _state = null!;
        private Button[] _tabs = Array.Empty<Button>();
        private Control _content = null!;
        private ColorRect _interference = null!;
        private Label _signal = null!;
        private int _selected;
        private bool _transitioning;
        private double _wheelCooldown;

        public static void ReturnToBrokenTimeline() => _returnSelection = 2;

        public static void ReturnToNewTimeline() => _returnSelection = 0;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            Theme = CreateTheme();
            _audio = GetNode<AudioManager>("/root/AudioManager");
            BuildUi();
            _selected = Mathf.Clamp(_returnSelection, 0, 2);
            _returnSelection = 0;
            Refresh(false);
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            _wheelCooldown = Math.Max(0.0, _wheelCooldown - delta);
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            if (_transitioning) return;
            if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode is Key.Left or Key.A) SelectRelative(-1);
                else if (key.Keycode is Key.Right or Key.D) SelectRelative(1);
                else if (key.Keycode == Key.Escape) ChangeScene("res://Main.tscn");
            }
            else if (inputEvent is InputEventMouseButton mouse && mouse.Pressed && _wheelCooldown <= 0.0)
            {
                if (mouse.ButtonIndex == MouseButton.WheelUp) SelectRelative(-1);
                else if (mouse.ButtonIndex == MouseButton.WheelDown) SelectRelative(1);
            }
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("050506"));
            var random = new RandomNumberGenerator { Seed = 20260812 };
            for (int index = 0; index < 700; index++)
            {
                DrawRect(new Rect2(random.RandfRange(0, Size.X), random.RandfRange(0, Size.Y), 1, 1), new Color(0.86f, 0.86f, 0.9f, 0.025f));
            }
            for (int line = 0; line < 22; line++)
            {
                float y = line * Math.Max(1.0f, Size.Y / 22.0f);
                DrawLine(new Vector2(0, y), new Vector2(Size.X, y), new Color(0.8f, 0.8f, 0.84f, 0.018f), 1);
            }
        }

        private void BuildUi()
        {
            var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 44, OffsetTop = 24, OffsetRight = -44, OffsetBottom = -24 };
            root.AddThemeConstantOverride("separation", 10);
            AddChild(root);

            var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
            root.AddChild(header);
            var back = CreateButton("← 返回主标题", 180);
            back.Pressed += () => ChangeScene("res://Main.tscn");
            header.AddChild(back);
            header.AddChild(CreateLabel("选择时间线 / SELECT TIMELINE", 24, HorizontalAlignment.Center, true));
            header.AddChild(new Control { CustomMinimumSize = new Vector2(180, 0) });
            root.AddChild(new HSeparator());

            var stage = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            stage.AddThemeConstantOverride("separation", 22);
            root.AddChild(stage);
            var left = CreateArrow("‹");
            left.Pressed += () => SelectRelative(-1);
            stage.AddChild(left);

            _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            _content.AddThemeConstantOverride("separation", 14);
            stage.AddChild(_content);
            _index = CreateLabel("", 16, HorizontalAlignment.Center, true);
            _tag = CreateLabel("", 16, HorizontalAlignment.Center, true);
            _tag.AddThemeColorOverride("font_color", new Color("9f9fa6"));
            _name = CreateButton("", 720);
            _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _name.AddThemeFontSizeOverride("font_size", 52);
            _name.Pressed += EnterSelected;
            _description = CreateLabel("", 24, HorizontalAlignment.Center, true);
            _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _description.CustomMinimumSize = new Vector2(720, 90);
            _state = CreateLabel("", 16, HorizontalAlignment.Center, true);
            _state.AddThemeColorOverride("font_color", new Color("b5b2b8"));
            _content.AddChild(_index);
            _content.AddChild(_tag);
            _content.AddChild(_name);
            _content.AddChild(new HSeparator());
            _content.AddChild(_description);
            _content.AddChild(_state);

            var right = CreateArrow("›");
            right.Pressed += () => SelectRelative(1);
            stage.AddChild(right);

            var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 48) };
            tabs.AddThemeConstantOverride("separation", 18);
            root.AddChild(tabs);
            _tabs = new Button[3];
            for (int index = 0; index < _tabs.Length; index++)
            {
                int target = index;
                _tabs[index] = CreateButton(_names[index], 210);
                _tabs[index].Pressed += () => Select(target);
                tabs.AddChild(_tabs[index]);
            }
            root.AddChild(CreateLabel("← / → · A / D · 鼠标滚轮切换", 14, HorizontalAlignment.Center, false));

            _interference = new ColorRect { AnchorRight = 1, AnchorBottom = 1, Color = new Color(0.7f, 0.7f, 0.75f, 0), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_interference);
            _signal = CreateLabel("SIGNAL REINDEXING", 18, HorizontalAlignment.Center, false);
            _signal.AnchorLeft = 0.35f;
            _signal.AnchorTop = 0.48f;
            _signal.AnchorRight = 0.65f;
            _signal.AnchorBottom = 0.54f;
            _signal.Modulate = new Color(1, 1, 1, 0);
            _signal.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_signal);
        }

        private void SelectRelative(int direction)
        {
            Select((_selected + direction + 3) % 3);
        }

        private void Select(int target)
        {
            if (_transitioning || target == _selected) return;
            _transitioning = true;
            _wheelCooldown = WheelCooldownSeconds;
            _audio.PlayTimelineInterference();
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(_content, "modulate:a", 0.18f, TransitionSeconds * 0.42);
            tween.TweenProperty(_interference, "color:a", 0.12f, TransitionSeconds * 0.25);
            tween.TweenProperty(_signal, "modulate:a", 0.65f, TransitionSeconds * 0.25);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() =>
            {
                _selected = target;
                Refresh(true);
            }));
            tween.SetParallel(true);
            tween.TweenProperty(_content, "modulate:a", 1f, TransitionSeconds * 0.58);
            tween.TweenProperty(_interference, "color:a", 0f, TransitionSeconds * 0.58);
            tween.TweenProperty(_signal, "modulate:a", 0f, TransitionSeconds * 0.58);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() => _transitioning = false));
        }

        private void Refresh(bool animate)
        {
            _index.Text = $"0{_selected + 1} / 03";
            _tag.Text = _tags[_selected];
            _name.Text = _names[_selected];
            _description.Text = _descriptions[_selected];
            _state.Text = _states[_selected];
            _name.Disabled = _selected != 0;
            _name.TooltipText = _selected == 0 ? "进入新生时间线。" : "尚未开放。";
            for (int index = 0; index < _tabs.Length; index++)
            {
                _tabs[index].Text = index == _selected ? $"— {_names[index]} —" : _names[index];
            }
            if (!animate)
            {
                _content.Modulate = Colors.White;
            }
        }

        private void EnterSelected()
        {
            if (_selected == 0) ChangeScene("res://NewGameSetup.tscn");
        }

        private void ChangeScene(string path)
        {
            Error error = GetTree().ChangeSceneToFile(path);
            if (error != Error.Ok) GD.PrintErr("Timeline scene change failed: " + path + " error=" + error);
        }

        private Button CreateArrow(string text)
        {
            var button = CreateButton(text, 64);
            button.CustomMinimumSize = new Vector2(64, 64);
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            button.AddThemeFontSizeOverride("font_size", 36);
            button.AddThemeStyleboxOverride("focus", CreateArrowFocusStyle());
            return button;
        }

        private static StyleBoxFlat CreateArrowFocusStyle()
        {
            var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f, 0.38f), BorderColor = new Color("b8b8be") };
            style.SetBorderWidthAll(1);
            style.CornerRadiusTopLeft = 2;
            style.CornerRadiusTopRight = 2;
            style.CornerRadiusBottomLeft = 2;
            style.CornerRadiusBottomRight = 2;
            return style;
        }

        private Button CreateButton(string text, float width)
        {
            var button = new Button { Text = text, Flat = true, FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(width, 42) };
            _audio.BindButton(button);
            return button;
        }

        private static Label CreateLabel(string text, int size, HorizontalAlignment alignment, bool expand)
        {
            var label = new Label { Text = text, HorizontalAlignment = alignment, SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkCenter };
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
            theme.SetColor("font_color", "Label", new Color("dedee2"));
            theme.SetColor("font_color", "Button", new Color("c8c8ce"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_focus_color", "Button", Colors.White);
            theme.SetColor("font_disabled_color", "Button", new Color("68686e"));
            return theme;
        }
    }
}
