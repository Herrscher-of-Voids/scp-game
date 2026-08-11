namespace Scp.Godot
{
    using global::Godot;

    public sealed partial class EventArchiveScreen : Control
    {
        /// <summary>
        /// 中文：展示破碎时间线的空事件档案目录、未来字段预览、筛选搜索框架和许可边界；本阶段不加载具体 SCP 情景。
        /// English: Displays the empty broken-timeline archive, future field preview, filter/search shell and licence boundary; this phase loads no concrete SCP scenario.
        /// 参数与单位：页面尺寸和控件间距使用 Godot 逻辑像素；搜索文本只影响本页提示，不访问网络或存档。
        /// Parameters and units: page dimensions and spacing use Godot logical pixels; search text only changes local status and accesses no network or save.
        /// 返回值与边界：返回按钮回到时间线选择页并恢复破碎时间线；空目录不产生创建、加载或模拟副作用。
        /// Return and boundaries: the back button returns to timeline selection and restores broken timeline; the empty directory creates, loads or simulates nothing.
        /// 确定性与原因：字段、筛选项和许可提示使用固定文本，避免未核验作品被误显示为正式内容。
        /// Determinism and rationale: fields, filters and licence notices are fixed text so unverified works cannot appear as official content.
        /// </summary>
        private AudioManager _audio = null!;
        private Label _status = null!;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            Theme = CreateTheme();
            _audio = GetNode<AudioManager>("/root/AudioManager");
            BuildUi();
            QueueRedraw();
        }

        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color("050506"));

        private void BuildUi()
        {
            var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 38, OffsetTop = 24, OffsetRight = -38, OffsetBottom = -24 };
            AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);

            var header = new HBoxContainer();
            var back = CreateButton("← 返回时间线选择", 220);
            back.Pressed += ReturnToTimeline;
            header.AddChild(back);
            header.AddChild(new Label { Text = "事件档案 / EVENT ARCHIVE", HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            header.AddChild(new Control { CustomMinimumSize = new Vector2(220, 0) });
            root.AddChild(header);
            root.AddChild(new HSeparator());

            var filters = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            filters.AddThemeConstantOverride("separation", 12);
            root.AddChild(filters);
            filters.AddChild(CreateSearch());
            filters.AddChild(CreateFilter("身份", new[] { "全部身份" }));
            filters.AddChild(CreateFilter("时代", new[] { "全部时代" }));
            filters.AddChild(CreateFilter("状态", new[] { "全部状态" }));

            var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", Box(new Color("0d0d10"), new Color("55555c")));
            root.AddChild(panel);
            var body = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            body.AddThemeConstantOverride("separation", 12);
            panel.AddChild(body);
            body.AddChild(Heading("暂无已核准情景档案", 30));
            body.AddChild(Heading("NO APPROVED EVENT DOSSIERS", 15));
            body.AddChild(new HSeparator());
            var sample = new Label
            {
                Text = "[ 灰化字段预览 ]\n\n标题：—\n时代：—\n可用身份：—\n来源与许可：待核验\n特殊规则：—\n简介：—",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(520, 210)
            };
            sample.AddThemeColorOverride("font_color", new Color("77777e"));
            body.AddChild(sample);
            body.AddChild(Heading("情景只有在作者、原始页面、翻译、许可与项目改编说明全部核验后才会开放。\nSCP 衍生内容遵守 CC BY-SA 3.0。", 15));

            _status = new Label { Text = "搜索与筛选框架已就绪；当前没有可检索的正式情景。", HorizontalAlignment = HorizontalAlignment.Center };
            _status.AddThemeColorOverride("font_color", new Color("aaa8ae"));
            root.AddChild(_status);
        }

        private LineEdit CreateSearch()
        {
            var search = new LineEdit { PlaceholderText = "搜索情景档案", CustomMinimumSize = new Vector2(320, 38) };
            search.TextChanged += _ => _status.Text = "当前没有已核准情景，搜索结果为空。";
            return search;
        }

        private OptionButton CreateFilter(string label, string[] values)
        {
            var option = new OptionButton { TooltipText = label, CustomMinimumSize = new Vector2(160, 38) };
            foreach (string value in values) option.AddItem(value);
            option.ItemSelected += _ => _status.Text = "当前没有已核准情景，筛选结果为空。";
            _audio.BindButton(option);
            return option;
        }

        private void ReturnToTimeline()
        {
            TimelineSelectionScreen.ReturnToBrokenTimeline();
            Error error = GetTree().ChangeSceneToFile("res://TimelineSelection.tscn");
            if (error != Error.Ok) GD.PrintErr("Event archive return failed: " + error);
        }

        private Button CreateButton(string text, float width)
        {
            var button = new Button { Text = text, Flat = true, CustomMinimumSize = new Vector2(width, 42) };
            _audio.BindButton(button);
            return button;
        }

        private static Label Heading(string text, int size)
        {
            var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("c8c8ce"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            return theme;
        }

        private static StyleBoxFlat Box(Color fill, Color border)
        {
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 18, ContentMarginBottom = 18 };
            box.SetBorderWidthAll(1);
            return box;
        }
    }
}
