namespace Scp.Godot
{
    using System;
    using global::Godot;
    using Scp.Application;
    using Scp.Domain;

    /// <summary>
    /// 中文：新生时间线开局档案页，控制档案名称、固定 O5 身份与难度；创建时隐藏生成内部世界标识，并通过摘要确认和放弃修改保护完成开局。
    /// English: New-timeline opening dossier controlling archive name, fixed O5 identity, and difficulty; creation hides the generated internal world identifier and completes setup through summary confirmation and discard protection.
    /// 中文：玩家界面禁止展示种子、算法、文件系统、数据来源或开发状态；这些仅是内部创建和维护资料，绝不作为世界内文案。
    /// English: The player interface must never expose seeds, algorithms, file systems, data sources, or development status; these are internal creation and maintenance material, never in-world copy.
    /// </summary>
    public sealed partial class NewGameSetupScreen : Control
    {
        private const string DefaultSaveName = "新的存档";
        private LineEdit _saveName = null!;
        private OptionButton _identity = null!;
        private OptionButton _difficulty = null!;
        private Label _summary = null!;
        private Label _error = null!;
        private Button _back = null!;
        private Button _create = null!;
        private ConfirmationDialog _dialog = null!;
        private DialogAction _dialogAction;
        private bool _initializing;
        private bool _creating;
        private SetupSnapshot _initial = new SetupSnapshot();
        private SaveFile? _pendingSave;

        /// <summary>中文：单一确认框当前唯一动作，防止摘要确认和放弃修改回调串线或累积。English: The single dialog's sole current action, preventing crossed or accumulated summary and discard callbacks.</summary>
        private enum DialogAction { None, FinalSummary, DiscardChanges }

        /// <summary>中文：用于比较页面是否被玩家修改的最小快照，覆盖所有玩家可编辑字段。English: Minimal snapshot used to detect player edits across every player-editable field.</summary>
        private sealed record SetupSnapshot(string Name = "", int Difficulty = 1);

        public override void _Ready()
        {
            _initializing = true;
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            BuildUi();
            RefreshSummary();
            _initial = Capture();
            _initializing = false;
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("060607"));
            var random = new RandomNumberGenerator { Seed = 20260808 };
            for (int index = 0; index < 900; index++)
            {
                DrawRect(new Rect2(random.RandfRange(0, Size.X), random.RandfRange(0, Size.Y), 1, 1), new Color(0.9f, 0.9f, 0.92f, 0.025f));
            }
        }

        private void BuildUi()
        {
            var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 64, OffsetTop = 22, OffsetRight = -64, OffsetBottom = -22 };
            AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 14);
            margin.AddChild(root);
            root.AddChild(CreateHeading("新生时间线", 42, new Color("f4f0f0")));
            root.AddChild(CreateHeading("NEW TIMELINE / OPENING DOSSIER", 18, new Color("bdbdc2")));
            root.AddChild(CreateHeading("一份新的基金会历史即将建立。任命交接与前任遗产将在开局时自动生成。", 19, new Color("aaa8ae")));
            root.AddChild(new HSeparator());
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 26);
            scroll.AddChild(columns);
            columns.AddChild(BuildFormColumn());
            columns.AddChild(BuildSummaryColumn());
            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 58) };
            actions.AddThemeConstantOverride("separation", 28);
            _back = CreateButton("返回时间线");
            _back.Pressed += OnBackPressed;
            actions.AddChild(_back);
            _create = CreateButton("创建游戏");
            _create.Pressed += OnCreatePressed;
            actions.AddChild(_create);
            root.AddChild(actions);
            _error = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _error.AddThemeFontSizeOverride("font_size", 18);
            _error.AddThemeColorOverride("font_color", new Color("e65a5a"));
            root.AddChild(_error);
            _dialog = new ConfirmationDialog { Title = "操作确认", OkButtonText = "确认", CancelButtonText = "返回" };
            ConfigureDialogTypography();
            _dialog.Confirmed += OnDialogConfirmed;
            _dialog.Canceled += () => _dialogAction = DialogAction.None;
            AddChild(_dialog);
        }

        /// <summary>
        /// 中文：创建玩家可编辑的开局档案字段；身份固定为 O5，难度只记录玩家所选规程，内部世界标识不在此页显示或接受输入。
        /// English: Creates player-editable opening dossier fields; identity is fixed to O5, difficulty records the selected protocol only, and the internal world identifier is neither displayed nor editable here.
        /// </summary>
        private Control BuildFormColumn()
        {
            PanelContainer panel = CreatePanel("新生档案 / NEW TIMELINE", out VBoxContainer body);
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsStretchRatio = 0.52f;
            _saveName = CreateTextInput(body, "存档名", "输入这条历史的名称");
            _saveName.Text = DefaultSaveName;
            _saveName.TextChanged += _ => OnChanged();

            _identity = CreateOption(body, "起始身份");
            _identity.AddItem("O5 监督者");
            _identity.Disabled = true;
            body.AddChild(Muted("其他身份将在后续时间线开放。"));

            _difficulty = CreateOption(body, "难度");
            foreach (string text in new[] { "简单", "普通", "困难", "真实" }) _difficulty.AddItem(text);
            _difficulty.Selected = 1;
            _difficulty.ItemSelected += _ => OnChanged();
            return panel;
        }

        private Control BuildSummaryColumn()
        {
            PanelContainer panel = CreatePanel("完整摘要 / SUMMARY", out VBoxContainer body);
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsStretchRatio = 0.48f;
            _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ExpandFill };
            _summary.AddThemeFontSizeOverride("font_size", 21);
            _summary.AddThemeConstantOverride("line_spacing", 7);
            body.AddChild(_summary);
            return panel;
        }

        private void OnChanged()
        {
            if (!_initializing) RefreshSummary();
        }

        /// <summary>
        /// 中文：刷新玩家可见的任命摘要；空名称在预览和创建时均回退到默认名称，避免空档案记录进入后续流程。
        /// English: Refreshes the player-visible appointment summary; blank names fall back to the default in both preview and creation, preventing blank archive records from entering later flows.
        /// </summary>
        private void RefreshSummary()
        {
            if (_summary == null) return;
            _summary.Text = "历史名称：" + ResolveRequestedName()
                + "\n任命身份：O5 监督者"
                + "\n时间线：新生时间线"
                + "\n难度：" + _difficulty.GetItemText(_difficulty.Selected)
                + "\n\n确认后将建立一条独立的基金会历史。";
        }

        /// <summary>
        /// 中文：为确认与放弃修改弹窗设置统一的可读字号和控件高度；该样式只影响本页自建 ConfirmationDialog，不改变全局主题或其他页面的弹窗。
        /// English: Applies a consistent readable font size and control height to confirmation and discard dialogs; styling affects only this page's ConfirmationDialog and never the global theme or dialogs on other pages.
        /// </summary>
        private void ConfigureDialogTypography()
        {
            // 中文：ConfirmationDialog 的正文标签和两个操作按钮是内部子控件，不继承容器的字号覆写；必须直接设置，才能让实际可见文本与页面字号一致。
            // English: ConfirmationDialog body and action buttons are internal child controls that do not inherit the container font override; they must be styled directly for visible text to match the page scale.
            _dialog.AddThemeFontSizeOverride("title_font_size", 22);
            _dialog.GetLabel().AddThemeFontSizeOverride("font_size", 19);
            _dialog.GetOkButton().AddThemeFontSizeOverride("font_size", 19);
            _dialog.GetCancelButton().AddThemeFontSizeOverride("font_size", 19);
            _dialog.GetOkButton().CustomMinimumSize = new Vector2(150, 48);
            _dialog.GetCancelButton().CustomMinimumSize = new Vector2(150, 48);
        }

        private void OnCreatePressed()
        {
            if (!TryBuildCandidate(out SaveFile? candidate)) return;
            _pendingSave = candidate;
            _dialogAction = DialogAction.FinalSummary;
            _dialog.Title = "确认创建游戏";
            _dialog.DialogText = _summary.Text + "\n\n确认后将登记这份任命档案。";
            _dialog.OkButtonText = "确认创建";
            _dialog.PopupCentered(new Vector2I(820, 620));
        }

        private void OnBackPressed()
        {
            if (_creating) return;
            if (Capture() == _initial) { ReturnToTimeline(); return; }
            _dialogAction = DialogAction.DiscardChanges;
            _dialog.Title = "放弃修改";
            _dialog.DialogText = "配置已经修改。确定放弃并返回时间线选择吗？";
            _dialog.OkButtonText = "放弃修改";
            _dialog.PopupCentered(new Vector2I(820, 620));
        }

        private void OnDialogConfirmed()
        {
            DialogAction action = _dialogAction;
            _dialogAction = DialogAction.None;
            if (action == DialogAction.DiscardChanges) { ReturnToTimeline(); return; }
            if (action == DialogAction.FinalSummary) CommitPendingSave();
        }

        /// <summary>
        /// 中文：最终确认后只把候选 SaveFile 作为一次性内存请求交给统一加载页；世界创建、交接摘要和安全保存由加载页按固定顺序执行。
        /// English: After final approval, hands the candidate SaveFile to the unified loader as a one-shot memory request; world creation, briefing summary, and safe persistence execute there in a fixed order.
        /// 返回/边界：方法无返回值；场景切换失败恢复输入且候选仍只属于当前页面，不建立部分记录。
        /// Return/boundary: the method returns nothing; scene-transition failure restores input and the candidate remains owned only by this page without creating a partial record.
        /// </summary>
        private void CommitPendingSave()
        {
            if (_pendingSave == null || _creating) return;
            SetCreating(true);
            GameLaunchContext.SetWork(new GameLaunchRequest { Kind = GameLaunchKind.NewGame, SaveId = _pendingSave.SaveId, Candidate = _pendingSave, ReturnScene = "res://NewGameSetup.tscn" });
            ChangeScene("res://TerminalLoading.tscn", "无法建立终端接入，请稍后重试。");
        }

        /// <summary>
        /// 中文：建立候选存档时规范化空名称、查询现有名称并自动追加全角编号；旧档永不被此流程覆盖。内部标识由八个加密随机字节编码为 16 位大写十六进制，仅写入存档。
        /// English: When building a candidate, normalizes blank names, checks existing names, and automatically appends full-width sequence suffixes; this flow never overwrites an existing archive. The internal identifier is eight cryptographically random bytes encoded as 16 uppercase hexadecimal characters and written only to the save.
        /// 返回/边界：成功返回完整候选；读取既有档案失败时返回 false 并显示可反馈的登记错误，防止无法确认唯一名称时误建记录。
        /// Return/boundary: returns a complete candidate on success; failures reading existing archives return false with a reportable registration error, preventing creation when name uniqueness cannot be confirmed.
        /// </summary>
        private bool TryBuildCandidate(out SaveFile? save)
        {
            save = null;
            try
            {
                string name = GameLaunchContext.CreateRepository().CreateUniqueDisplayName(ResolveRequestedName());
                DateTime now = DateTime.UtcNow;
                save = new SaveFile
                {
                    SaveId = Guid.NewGuid().ToString("N"),
                    DisplayName = name,
                    Identity = IdentityRole.Overseer,
                    Difficulty = (GameDifficulty)(_difficulty.Selected + 1),
                    Seed = CreateInternalWorldIdentifier(),
                    CreatedAtUtc = now,
                    SavedAtUtc = now,
                    SaveKind = SaveKind.Manual,
                    GameVersion = "0.1.0-alpha",
                    Mode = SaveMode.Standalone,
                    BriefingAcknowledged = false
                };
                _error.Text = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                GD.PrintErr("NewGameSetup archive-name registration failed: " + exception);
                _error.Text = "无法核验现有档案名称。请稍后重试；如问题持续，请截图反馈。";
                return false;
            }
        }

        /// <summary>中文：空白或仅空格的输入统一使用默认档案名。English: Blank or whitespace-only input always uses the default archive name.</summary>
        private string ResolveRequestedName()
        {
            string name = _saveName.Text.Trim();
            return string.IsNullOrWhiteSpace(name) ? DefaultSaveName : name;
        }

        /// <summary>中文：生成仅供世界创建使用的 16 位内部标识，不显示、复制或允许玩家输入。English: Generates a 16-character internal identifier used only for world creation; it is never displayed, copied, or player-entered.</summary>
        private static string CreateInternalWorldIdentifier()
        {
            byte[] bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
            return Convert.ToHexString(bytes);
        }

        private void SetCreating(bool creating)
        {
            _creating = creating;
            _back.Disabled = creating;
            _create.Disabled = creating;
            _saveName.Editable = !creating;
            _identity.Disabled = true;
            _difficulty.Disabled = creating;
        }

        private void ChangeScene(string path, string playerMessage, bool restoreOnFailure = true)
        {
            Error error = GetTree().ChangeSceneToFile(path);
            if (error == Error.Ok) return;
            GD.PrintErr("NewGameSetup scene change failed: " + path + " error=" + error);
            _error.Text = playerMessage;
            if (restoreOnFailure) SetCreating(false);
        }

        private void ReturnToTimeline()
        {
            TimelineSelectionScreen.ReturnToNewTimeline();
            ChangeScene("res://TimelineSelection.tscn", "无法返回时间线选择，请稍后重试。");
        }

        private SetupSnapshot Capture() => new SetupSnapshot(_saveName.Text, _difficulty.Selected);

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
            theme.SetColor("font_color", "Button", new Color("d8d8dc"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            return theme;
        }

        private static PanelContainer CreatePanel(string title, out VBoxContainer body)
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", CreateBox(new Color("101013"), new Color("77777e"), 1));
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 12);
            panel.AddChild(column);
            var heading = new Label { Text = title };
            heading.AddThemeFontSizeOverride("font_size", 20);
            column.AddChild(heading);
            column.AddChild(new HSeparator());
            body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            column.AddChild(body);
            return panel;
        }

        private static LineEdit CreateTextInput(VBoxContainer parent, string label, string placeholder)
        {
            var fieldLabel = new Label { Text = label };
            fieldLabel.AddThemeFontSizeOverride("font_size", 19);
            parent.AddChild(fieldLabel);
            var input = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 44) };
            input.AddThemeFontSizeOverride("font_size", 20);
            parent.AddChild(input);
            return input;
        }

        private static OptionButton CreateOption(VBoxContainer parent, string label)
        {
            var fieldLabel = new Label { Text = label };
            fieldLabel.AddThemeFontSizeOverride("font_size", 19);
            parent.AddChild(fieldLabel);
            var option = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
            option.AddThemeFontSizeOverride("font_size", 20);
            parent.AddChild(option);
            return option;
        }

        private static Label Muted(string text)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size", 18);
            label.AddThemeConstantOverride("line_spacing", 4);
            label.AddThemeColorOverride("font_color", new Color("aaa8ae"));
            return label;
        }

        private static Button CreateButton(string text)
        {
            var button = new Button { Text = text, Flat = true, CustomMinimumSize = new Vector2(230, 52) };
            button.AddThemeFontSizeOverride("font_size", 20);
            return button;
        }
        private static Label CreateHeading(string text, int size, Color color) { var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label; }
        private static StyleBoxFlat CreateBox(Color fill, Color border, int width) { var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 18, ContentMarginBottom = 18 }; box.SetBorderWidthAll(width); return box; }
    }
}
