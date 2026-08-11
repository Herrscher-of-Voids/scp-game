namespace Scp.Godot
{
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 《SCP：常态的代价》的正式主标题界面。
    /// Official main title screen for "SCP: Necessary Measures".
    ///
    /// 控制对象：标题、基金会标志、主菜单、内测反馈和许可提示。
    /// Controlled objects: title, Foundation emblem, main menu, beta feedback and licence notice.
    /// 参数与单位：所有比例使用视口归一化坐标，字号使用像素；不依赖现实时间。
    /// Parameters and units: all layout ratios use normalized viewport coordinates and font sizes use pixels; no real-time dependency.
    /// 返回值：本控制器通过场景切换和弹窗产生界面状态，没有业务返回值。
    /// Return value: this controller produces UI state through scene switching and dialogs; it has no business return value.
    /// 边界情况：没有存档时“继续游戏”禁用；未完成页面用明确提示，不伪装为已实现功能。
    /// Edge cases: "Continue" is disabled without a save; unfinished pages show an explicit notice instead of pretending to work.
    /// 确定性：背景噪点使用固定种子，保证同一版本预览一致；菜单逻辑不产生随机结果。
    /// Determinism: background grain uses a fixed seed so previews remain consistent; menu logic creates no random results.
    /// 设计原因：主菜单必须先于 O5 总览出现，并让玩家明确知道哪些功能可用。
    /// Design reason: the main menu must appear before the O5 overview and clearly communicate which features are available.
    /// </summary>
    public sealed partial class MainTitleScreen : Control
    {
        private const string GameTitle = "SCP：常态的代价";
        private const string EnglishTitle = "SCP: NECESSARY MEASURES";
        private const string Motto = "收容 · 控制 · 保护";
        private const int BackgroundSeed = 20260808;

        private VBoxContainer _menu = null!;
        private Control _root = null!;
        private Label _statusLabel = null!;
        private ConfirmationDialog _dialog = null!;
        private Button _continueButton = null!;
        // 中文：Autoload 音频管理器跨场景保留播放器；本界面只控制标题曲生命周期和菜单事件。
        // English: The Autoload audio manager retains players across scenes; this screen only controls title-music lifetime and menu events.
        private AudioManager _audio = null!;
        private SaveProbeResult _continueProbe = new SaveProbeResult { Status = SaveProbeStatus.NoSave };
        private ConfirmationAction _confirmationAction;

        /// <summary>
        /// 中文：单个 ConfirmationDialog 的互斥动作状态，避免退出与备份回退回调重复订阅或串线。
        /// English: Mutually exclusive action state for the single ConfirmationDialog, preventing accumulated or crossed quit/backup callbacks.
        /// </summary>
        private enum ConfirmationAction
        {
            None,
            Quit,
            BackupContinue
        }

        /// <summary>
        /// 创建主标题界面，并绑定所有玩家可操作的菜单入口。
        /// Builds the title screen and binds every player-facing menu entry.
        /// </summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            _audio = GetNode<AudioManager>("/root/AudioManager");
            BuildTheme();
            BuildUi();
            ProbeContinueSave();
            _audio.PlayMainTitleMusic();
            QueueRedraw();
        }

        /// <summary>
        /// 中文：主标题节点离开场景树时停止其专属音乐，防止设置页或 O5 总览继承标题氛围。
        /// English: Stops title-only music when this node leaves the scene tree so settings and the O5 overview never inherit it.
        /// </summary>
        public override void _ExitTree()
        {
            _audio?.StopMainTitleMusic();
        }

        /// <summary>
        /// 绘制纯黑底板和固定种子的极弱档案噪点；噪点透明度很低，不干扰文字阅读。
        /// Draws the near-black backdrop and deterministic, very faint archive grain; opacity stays low for readability.
        /// </summary>
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("060607"));
            var random = new RandomNumberGenerator();
            random.Seed = BackgroundSeed;
            for (int index = 0; index < 1200; index++)
            {
                float x = random.RandfRange(0.0f, Size.X);
                float y = random.RandfRange(0.0f, Size.Y);
                float alpha = random.RandfRange(0.018f, 0.045f);
                DrawRect(new Rect2(x, y, 1.0f, 1.0f), new Color(0.85f, 0.85f, 0.88f, alpha));
            }
        }

        private void BuildTheme()
        {
            var theme = new Theme();
            theme.DefaultFontSize = 22;
            theme.SetFontSize("font_size", "Label", 22);
            theme.SetFontSize("font_size", "Button", 26);
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("c5c5ca"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_focus_color", "Button", Colors.White);
            theme.SetColor("font_pressed_color", "Button", new Color("9b9ba0"));
            theme.SetColor("font_disabled_color", "Button", new Color("66666d"));
            theme.SetConstant("outline_size", "Button", 0);
            Theme = theme;
        }

        private void BuildUi()
        {
            _root = new VBoxContainer
            {
                AnchorLeft = 0.0f,
                AnchorTop = 0.0f,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetLeft = 32.0f,
                OffsetTop = 28.0f,
                OffsetRight = -32.0f,
                OffsetBottom = -28.0f
            };
            _root.AddThemeConstantOverride("separation", 0);
            AddChild(_root);

            var titleArea = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(0, 300),
                SizeFlagsVertical = SizeFlags.Fill,
                SizeFlagsStretchRatio = 0.38f
            };
            titleArea.AddThemeConstantOverride("separation", 8);
            _root.AddChild(titleArea);

            var logo = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://Assets/Resources/UI/SCPFoundationEmblemWhite.svg"),
                CustomMinimumSize = new Vector2(0, 100),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.Fill
            };
            titleArea.AddChild(logo);

            titleArea.AddChild(CreateCenteredLabel(GameTitle, 44, new Color("f4f0f0")));
            titleArea.AddChild(CreateCenteredLabel(EnglishTitle, 24, new Color("d4d0d4")));
            var rule = new HSeparator();
            rule.CustomMinimumSize = new Vector2(0, 1);
            titleArea.AddChild(rule);
            titleArea.AddChild(CreateCenteredLabel(Motto, 16, new Color("aaa8ae")));

            var menuArea = new CenterContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.62f
            };
            _root.AddChild(menuArea);
            _menu = new VBoxContainer();
            _menu.AddThemeConstantOverride("separation", 8);
            menuArea.AddChild(_menu);

            _continueButton = AddMenuButton("继续游戏", false, "正在检查存档", OnContinuePressed);
            AddMenuButton("新建游戏", true, string.Empty, OnNewGamePressed);
            AddMenuButton("读取存档", true, string.Empty, OnLoadPressed);
            AddMenuButton("设置", true, string.Empty, OnSettingsPressed);
            AddMenuButton("内测反馈", true, string.Empty, OnFeedbackPressed);
            AddMenuButton("制作人员与许可", true, string.Empty, OnCreditsPressed);
            AddMenuButton("退出游戏", true, string.Empty, OnQuitPressed);

            var footer = new HBoxContainer();
            footer.CustomMinimumSize = new Vector2(0, 28);
            _root.AddChild(footer);
            footer.AddChild(new Label { Text = "版本 v0.1.0-alpha · 内测编号 TEST-0417" });
            var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            footer.AddChild(spacer);
            footer.AddChild(new Label { Text = "SCP 内容 CC BY-SA 3.0 · 代码 GPLv3" });

            _statusLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _root.AddChild(_statusLabel);

            _dialog = new ConfirmationDialog
            {
                Title = "操作确认",
                OkButtonText = "确认",
                CancelButtonText = "返回"
            };
            _dialog.Confirmed += OnDialogConfirmed;
            _dialog.Canceled += () => _confirmationAction = ConfirmationAction.None;
            AddChild(_dialog);
        }

        private Label CreateCenteredLabel(string text, int fontSize, Color color)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        /// <summary>
        /// 创建一个严格居中的菜单项，并仅在悬停或焦点状态显示两侧横线。
        /// Creates a strictly centered menu item and shows side rules only while hovered or focused.
        /// 边界行为：禁用项仍保持与其他项目相同的文本中心位置，不附加右侧原因文字。
        /// Edge behavior: disabled items keep the same text center as other items and receive no right-side reason text.
        /// </summary>
        private Button AddMenuButton(string text, bool enabled, string disabledReason, System.Action callback)
        {
            var button = new Button
            {
                Text = text,
                Disabled = !enabled,
                Flat = true,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(330, 46),
                TooltipText = disabledReason,
                Alignment = HorizontalAlignment.Center
            };

            // 中文：鼠标悬停和键盘/手柄焦点都代表当前选择，因此两者均显示横线；默认状态隐藏横线。
            // English: Hover and keyboard/gamepad focus both represent selection, so both show side rules; the default state hides them.
            button.MouseEntered += () => UpdateMenuButtonVisual(button, text, true);
            button.MouseExited += () => UpdateMenuButtonVisual(button, text, button.HasFocus());
            button.FocusEntered += () => UpdateMenuButtonVisual(button, text, true);
            button.FocusExited += () => UpdateMenuButtonVisual(button, text, button.IsHovered());
            _audio.BindButton(button);
            button.Pressed += callback;
            _menu.AddChild(button);
            return button;
        }

        /// <summary>
        /// 统一更新菜单项文字，保证横线出现或隐藏时按钮仍按同一控件中心对齐。
        /// Updates one menu label consistently so the button remains centered when side rules appear or disappear.
        /// </summary>
        private static void UpdateMenuButtonVisual(Button button, string text, bool showSideRules)
        {
            button.Text = showSideRules ? "—  " + text + "  —" : text;
        }

        /// <summary>
        /// 中文：读取最近索引并按主档、备份、终局、版本、损坏或 I/O 分类更新按钮；文本原因只放状态和 Tooltip，不改变居中标签。
        /// English: Reads the latest index and updates the button by primary, backup, ended, version, corruption or I/O status; reasons stay in status and tooltip so the centered label does not move.
        /// </summary>
        private void ProbeContinueSave()
        {
            _continueProbe = GameLaunchContext.CreateRepository().ProbeLatest();
            bool enabled = _continueProbe.Status == SaveProbeStatus.PrimaryAvailable || _continueProbe.Status == SaveProbeStatus.BackupAvailable;
            _continueButton.Disabled = !enabled;
            _continueButton.TooltipText = _continueProbe.Message;
            if (!enabled)
            {
                SetStatus(_continueProbe.Message);
            }
        }

        private void OnContinuePressed()
        {
            if (_continueProbe.Status == SaveProbeStatus.PrimaryAvailable)
            {
                StartContinue(GameLaunchKind.ContinueGame);
            }
            else if (_continueProbe.Status == SaveProbeStatus.BackupAvailable)
            {
                _confirmationAction = ConfirmationAction.BackupContinue;
                _dialog.Title = "回退到备份存档";
                _dialog.DialogText = "主存档已损坏。继续将载入上一保存版本，最近一次进度可能丢失。是否确认回退？";
                _dialog.PopupCentered();
            }
        }

        /// <summary>
        /// 中文：提交精确的主档或玩家确认备份请求并进入统一加载页；本页不读档，备份也绝不静默尝试其他版本。
        /// English: Submits an exact primary or player-approved backup request and enters the unified loader; this screen performs no load and backup never silently tries another version.
        /// 参数/返回/边界：kind 只能是继续或备份继续；方法无返回值，场景切换失败时保留标题并显示玩家提示。
        /// Parameter/return/boundary: kind must be primary or backup continue; the method returns nothing and keeps the title with a player message when transition fails.
        /// </summary>
        private void StartContinue(GameLaunchKind kind)
        {
            GameLaunchContext.SetWork(new GameLaunchRequest { Kind = kind, SaveId = _continueProbe.SaveId, ReturnScene = "res://Main.tscn" });
            Error error = GetTree().ChangeSceneToFile("res://TerminalLoading.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("MainTitle loading scene change failed: " + error);
                SetStatus("无法建立终端接入，请稍后重试。");
            }
        }

        /// <summary>
        /// 中文：切换到独立读取存档场景；切换失败保留主标题并显示玩家可读提示。
        /// English: Changes to the independent load-game scene; a failed change keeps the title screen and shows a player-readable message.
        /// </summary>
        private void OnLoadPressed()
        {
            Error error = GetTree().ChangeSceneToFile("res://LoadGame.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("MainTitle load-game scene change failed: " + error);
                SetStatus("无法打开读取存档页面，请稍后重试。");
            }
        }
        /// <summary>
        /// 中文：切换到设置页面；切换失败保留主标题并显示玩家可读提示。
        /// English: Changes to the settings page; a failed change keeps the title screen and shows a player-readable message.
        /// </summary>
        private void OnSettingsPressed()
        {
            Error error = GetTree().ChangeSceneToFile("res://Settings.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("MainTitle settings scene change failed: " + error);
                SetStatus("无法打开设置页面，请稍后重试。");
            }
        }
        /// <summary>
        /// 中文：切换到独立的本地内测反馈页面；失败时保留主标题并显示错误，绝不把反馈标记为已提交。
        /// English: Changes to the independent local beta-feedback page; failures keep the title visible and never mark feedback as submitted.
        /// 返回与边界：Godot Error.Ok 表示切换请求成功，其他结果只显示玩家提示；本方法不访问网络或存档。
        /// Return and boundary: Error.Ok means the scene request succeeded; other results show a player message only, with no network or save access.
        /// </summary>
        private void OnFeedbackPressed()
        {
            Error error = GetTree().ChangeSceneToFile("res://Feedback.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("MainTitle feedback scene change failed: " + error);
                SetStatus("无法打开内测反馈页面，请稍后重试。");
            }
        }

        /// <summary>
        /// 中文：切换到独立制作人员与许可档案页；Godot 返回 Error.Ok 表示请求成功，否则保留主标题并向玩家显示错误。
        /// English: Changes to the standalone credits and licences archive; Godot Error.Ok means success, otherwise the title remains visible with a player-readable error.
        /// 控制与边界：本入口不读取、推断或改写署名资料，所有许可内容由目标页面固定展示。
        /// Control and boundary: this entry point never reads, infers or rewrites attribution data; the target page displays all licence content as fixed records.
        /// </summary>
        private void OnCreditsPressed()
        {
            Error error = GetTree().ChangeSceneToFile("res://Credits.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("MainTitle credits scene change failed: " + error);
                SetStatus("无法打开制作人员与许可页面，请稍后重试。");
            }
        }

        /// <summary>
        /// 切换到独立的时间线选择场景，玩家先选择新生、延续或破碎时间线，再进入对应配置入口。
        /// Changes to the standalone timeline-selection scene so the player chooses a new, continued, or broken timeline before its configuration entry.
        /// </summary>
        private void OnNewGamePressed()
        {
            GetTree().ChangeSceneToFile("res://TimelineSelection.tscn");
        }

        private void OnQuitPressed()
        {
            _confirmationAction = ConfirmationAction.Quit;
            _dialog.Title = "退出游戏";
            _dialog.DialogText = "确定退出《SCP：常态的代价》吗？";
            _dialog.PopupCentered();
        }

        /// <summary>
        /// 中文：唯一确认回调根据当前动作分派并立即清空状态，因此重复打开弹窗不会累积事件订阅。
        /// English: The sole confirmation callback dispatches by current action and immediately clears state, so reopening the dialog never accumulates subscriptions.
        /// </summary>
        private void OnDialogConfirmed()
        {
            ConfirmationAction action = _confirmationAction;
            _confirmationAction = ConfirmationAction.None;
            if (action == ConfirmationAction.Quit)
            {
                GetTree().Quit();
            }
            else if (action == ConfirmationAction.BackupContinue)
            {
                StartContinue(GameLaunchKind.BackupContinue);
            }
        }

        private void SetStatus(string message)
        {
            _statusLabel.Text = message;
        }
    }
}
