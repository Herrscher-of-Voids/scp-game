namespace Scp.Godot
{
    using System;
    using System.Text;
    using global::Godot;
    using Scp.Application;
    using Scp.Domain;

    /// <summary>
    /// 中文：新生时间线开局档案页，控制存档名、固定 O5 身份、难度与起点方式，并通过互斥确认状态完成摘要确认、重复副本确认和放弃修改保护。
    /// English: New-timeline opening dossier controlling save name, fixed O5 identity, difficulty and start-point mode, with mutually exclusive confirmation and discard protection.
    /// 中文：种子文本仍用于确定性世界创建，但界面只呈现玩家可理解的起点语义；创建期间锁定全部输入，失败后恢复且保留玩家输入。
    /// English: Seed text still drives deterministic world creation, while the interface exposes only player-facing start-point semantics; inputs lock during creation and recover without losing edits after failure.
    /// </summary>
    public sealed partial class NewGameSetupScreen : Control
    {
        private const string BaselineSeed = "O5-BASELINE-001";
        private LineEdit _saveName = null!;
        private OptionButton _identity = null!;
        private OptionButton _saveMode = null!;
        private OptionButton _difficulty = null!;
        private OptionButton _seedMode = null!;
        private LineEdit _seed = null!;
        private Label _difficultyHelp = null!;
        private Label _seedHelp = null!;
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

        /// <summary>中文：单一确认框当前唯一动作，防止摘要、重复副本和放弃修改回调串线或累积。English: The single dialog's sole current action, preventing crossed or accumulated summary, duplicate-copy and discard callbacks.</summary>
        private enum DialogAction { None, FinalSummary, DuplicateCopy, DiscardChanges }
        private enum SeedMode { Baseline, Random, Custom }

        /// <summary>中文：用于比较页面是否被玩家修改的最小快照，覆盖全部已确认字段。English: Minimal snapshot used to detect player edits across every confirmed field.</summary>
        private sealed record SetupSnapshot(string Name = "", int Identity = 0, int SaveMode = 0, int Difficulty = 1, int SeedMode = 0, string Seed = BaselineSeed);

        public override void _Ready()
        {
            _initializing = true;
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            BuildUi();
            ApplySeedMode(false);
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
            var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 60, OffsetTop = 28, OffsetRight = -60, OffsetBottom = -28 };
            AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);
            root.AddChild(CreateHeading("新生时间线", 34, new Color("f4f0f0")));
            root.AddChild(CreateHeading("NEW TIMELINE / OPENING DOSSIER", 15, new Color("bdbdc2")));
            root.AddChild(CreateHeading("一份新的基金会历史即将建立。任命交接与前任遗产将在开局时自动生成。", 16, new Color("aaa8ae")));
            root.AddChild(new HSeparator());
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 20);
            scroll.AddChild(columns);
            columns.AddChild(BuildFormColumn());
            columns.AddChild(BuildSummaryColumn());
            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 48) };
            _back = CreateButton("返回时间线");
            _back.Pressed += OnBackPressed;
            actions.AddChild(_back);
            _create = CreateButton("创建游戏");
            _create.Pressed += OnCreatePressed;
            actions.AddChild(_create);
            root.AddChild(actions);
            _error = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _error.AddThemeColorOverride("font_color", new Color("e65a5a"));
            root.AddChild(_error);
            _dialog = new ConfirmationDialog { Title = "操作确认", OkButtonText = "确认", CancelButtonText = "返回" };
            _dialog.Confirmed += OnDialogConfirmed;
            _dialog.Canceled += () => _dialogAction = DialogAction.None;
            AddChild(_dialog);
        }

        private Control BuildFormColumn()
        {
            PanelContainer panel = CreatePanel("新生档案 / NEW TIMELINE", out VBoxContainer body);
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsStretchRatio = 0.52f;
            _saveName = CreateTextInput(body, "存档名", "输入这条历史的名称");
            _saveName.Text = "新的存档";
            _saveName.TextChanged += _ => OnChanged();

            _identity = CreateOption(body, "起始身份");
            string[] identities = { "O5 监督者" };
            foreach (string text in identities) _identity.AddItem(text);
            _identity.Disabled = true;
            body.AddChild(Muted("新生时间线从新的 O5 任命开始。其他身份将在后续时间线开放。"));

            _saveMode = new OptionButton();
            _saveMode.AddItem("独立存档（已开放）");

            _difficulty = CreateOption(body, "难度");
            foreach (string text in new[] { "简单", "普通", "困难", "真实" }) _difficulty.AddItem(text);
            _difficulty.Selected = 1;
            _difficulty.ItemSelected += _ => OnChanged();
            _difficultyHelp = Muted("");
            body.AddChild(_difficultyHelp);

            _seedMode = CreateOption(body, "世界起点");
            foreach (string text in new[] { "基准起点", "随机起点", "自定义起点" }) _seedMode.AddItem(text);
            _seedMode.ItemSelected += _ => ApplySeedMode(true);
            _seed = CreateTextInput(body, "起点代码", "输入自定义起点代码");
            _seed.Text = BaselineSeed;
            _seed.TextChanged += _ => OnChanged();
            _seedHelp = Muted("");
            body.AddChild(_seedHelp);
            return panel;
        }

        private Control BuildSummaryColumn()
        {
            PanelContainer panel = CreatePanel("完整摘要 / SUMMARY", out VBoxContainer body);
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsStretchRatio = 0.48f;
            _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ExpandFill };
            body.AddChild(_summary);
            return panel;
        }

        private void OnChanged()
        {
            if (!_initializing) RefreshSummary();
        }

        /// <summary>中文：切换种子模式并控制输入只读状态；随机模式每次被主动选择都生成新的可预览文本。English: Applies seed mode and editability; random mode generates a new preview text each time the player actively selects it.</summary>
        private void ApplySeedMode(bool generateRandom)
        {
            SeedMode mode = (SeedMode)_seedMode.Selected;
            if (mode == SeedMode.Baseline) _seed.Text = BaselineSeed;
            else if (mode == SeedMode.Random && generateRandom) _seed.Text = "RANDOM-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            else if (mode == SeedMode.Custom && _seed.Text == BaselineSeed) _seed.Text = string.Empty;
            _seed.Editable = mode == SeedMode.Custom && !_creating;
            _seedHelp.Text = mode == SeedMode.Baseline ? "使用基金会基准开局。" : mode == SeedMode.Random ? "为这条新历史生成一个独立起点。" : "使用你指定的起点代码建立这条历史。";
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            if (_summary == null) return;
            string difficulty = DifficultyDescription(_difficulty.Selected);
            _difficultyHelp.Text = difficulty + " 当前版本记录该选择；完整难度差异将在后续模拟切片接入。";
            _summary.Text = "历史名称：" + (string.IsNullOrWhiteSpace(_saveName.Text) ? "（未填写）" : _saveName.Text.Trim())
                + "\n任命身份：O5 监督者"
                + "\n时间线：新生时间线"
                + "\n难度：" + _difficulty.GetItemText(_difficulty.Selected) + " — " + difficulty
                + "\n世界起点：" + _seedMode.GetItemText(_seedMode.Selected)
                + "\n\n开局交接：自动生成"
                + "\n前任遗产：自动生成"
                + "\n正式设施：89 处"
                + "\n\n确认后将建立一条独立的基金会历史。";
        }

        private static string DifficultyDescription(int index) => index switch
        {
            0 => "适合首次游玩。",
            1 => "设计基准。",
            2 => "减少辅助并提高压力。",
            3 => "辅助最少，只有一个存档槽位。",
            _ => "未知难度。"
        };

        private void OnCreatePressed()
        {
            if (!TryBuildCandidate(out SaveFile? candidate)) return;
            _pendingSave = candidate;
            _dialogAction = DialogAction.FinalSummary;
            _dialog.Title = "确认创建游戏";
            _dialog.DialogText = _summary.Text + "\n\n确认后将检查同名与重复配置；此时尚未写入磁盘。";
            _dialog.OkButtonText = "确认配置";
            _dialog.PopupCentered(new Vector2I(760, 620));
        }

        private void OnBackPressed()
        {
            if (_creating) return;
            if (Capture() == _initial) { ReturnToTimeline(); return; }
            _dialogAction = DialogAction.DiscardChanges;
            _dialog.Title = "放弃修改";
            _dialog.DialogText = "配置已经修改。确定放弃并返回时间线选择吗？";
            _dialog.OkButtonText = "放弃修改";
            _dialog.PopupCentered();
        }

        private void OnDialogConfirmed()
        {
            DialogAction action = _dialogAction;
            _dialogAction = DialogAction.None;
            if (action == DialogAction.DiscardChanges) { ReturnToTimeline(); return; }
            if (action == DialogAction.FinalSummary) ProbeDuplicateThenCreate();
            else if (action == DialogAction.DuplicateCopy) CommitPendingSave();
        }

        private void ProbeDuplicateThenCreate()
        {
            if (_pendingSave == null) return;
            try
            {
                DuplicateSaveProbeResult result = GameLaunchContext.CreateRepository().ProbeDuplicates(_pendingSave);
                if (result.SkippedSaveCount > 0) GD.Print("Duplicate probe skipped " + result.SkippedSaveCount + " unreadable or incompatible save(s).");
                if (result.Match != DuplicateSaveMatch.None)
                {
                    _dialogAction = DialogAction.DuplicateCopy;
                    _dialog.Title = "创建独立副本";
                    _dialog.DialogText = result.Match.HasFlag(DuplicateSaveMatch.IdenticalConfiguration)
                        ? "已存在同名且配置完全相同的存档。不会覆盖旧档；是否仍然创建新的独立副本？"
                        : "已存在同名存档。不会覆盖旧档；是否仍然创建新的独立副本？";
                    _dialog.OkButtonText = "仍然创建副本";
                    _dialog.PopupCentered();
                    return;
                }
                CommitPendingSave();
            }
            catch (Exception exception)
            {
                GD.PrintErr("NewGameSetup duplicate probe failed: " + exception);
                _error.Text = "无法检查现有存档，请检查存储权限后重试。";
            }
        }

        /// <summary>
        /// 中文：最终确认后只把候选 SaveFile 作为一次性内存请求交给统一加载页；SCP/设施加载、世界创建、交接摘要和原子保存由加载页按固定顺序执行。
        /// English: After final approval, hands only the candidate SaveFile to the unified loader as a one-shot memory request; SCP/facility loading, world creation, briefing summary, and atomic save run there in fixed order.
        /// 返回/边界：方法无返回值；场景切换失败恢复输入且候选仍只属于当前页面，不写磁盘。
        /// Return/boundary: the method returns nothing; transition failure restores input and the candidate remains owned only by this page without disk writes.
        /// </summary>
        private void CommitPendingSave()
        {
            if (_pendingSave == null || _creating) return;
            SetCreating(true);
            GameLaunchContext.SetWork(new GameLaunchRequest { Kind = GameLaunchKind.NewGame, SaveId = _pendingSave.SaveId, Candidate = _pendingSave, ReturnScene = "res://NewGameSetup.tscn" });
            ChangeScene("res://TerminalLoading.tscn", "无法建立终端接入，请稍后重试。");
        }

        private bool TryBuildCandidate(out SaveFile? save)
        {
            save = null;
            string name = _saveName.Text.Trim();
            string seed = _seed.Text.Trim();
            if (name.Length is < 1 or > 32) { _error.Text = "存档名必须为 1–32 个字符。"; return false; }
            if (_identity.Selected != 0 || _saveMode.Selected != 0) { _error.Text = "当前只能使用 O5 监督者与独立存档。"; return false; }
            if (seed.Length == 0) { _error.Text = "种子文本不能为空。"; return false; }
            DateTime now = DateTime.UtcNow;
            save = new SaveFile { SaveId = Guid.NewGuid().ToString("N"), DisplayName = name, Identity = IdentityRole.Overseer, Difficulty = (GameDifficulty)(_difficulty.Selected + 1), Seed = seed, CreatedAtUtc = now, SavedAtUtc = now, SaveKind = SaveKind.Manual, GameVersion = "0.1.0-alpha", Mode = SaveMode.Standalone, BriefingAcknowledged = false };
            _error.Text = string.Empty;
            return true;
        }

        private void SetCreating(bool creating)
        {
            _creating = creating;
            _back.Disabled = creating;
            _create.Disabled = creating;
            _saveName.Editable = !creating;
            _identity.Disabled = creating;
            _saveMode.Disabled = creating;
            _difficulty.Disabled = creating;
            _seedMode.Disabled = creating;
            _seed.Editable = !creating && (SeedMode)_seedMode.Selected == SeedMode.Custom;
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

        private SetupSnapshot Capture() => new SetupSnapshot(_saveName.Text, _identity.Selected, _saveMode.Selected, _difficulty.Selected, _seedMode.Selected, _seed.Text);

        /// <summary>中文：稳定 UTF-8 FNV-1a 64 位映射，禁止使用进程随机化的 string.GetHashCode。English: Stable 64-bit UTF-8 FNV-1a mapping; randomized string.GetHashCode is forbidden.</summary>
        private static ulong StableSeed(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte value in Encoding.UTF8.GetBytes(text)) { hash ^= value; hash *= prime; }
            return hash;
        }

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
            column.AddThemeConstantOverride("separation", 8);
            panel.AddChild(column);
            column.AddChild(new Label { Text = title });
            column.AddChild(new HSeparator());
            body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            column.AddChild(body);
            return panel;
        }

        private static LineEdit CreateTextInput(VBoxContainer parent, string label, string placeholder)
        {
            parent.AddChild(new Label { Text = label });
            var input = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 36) };
            parent.AddChild(input);
            return input;
        }

        private static OptionButton CreateOption(VBoxContainer parent, string label)
        {
            parent.AddChild(new Label { Text = label });
            var option = new OptionButton { CustomMinimumSize = new Vector2(0, 36) };
            parent.AddChild(option);
            return option;
        }

        private static Label Muted(string text)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeColorOverride("font_color", new Color("aaa8ae"));
            return label;
        }

        private static Button CreateButton(string text) => new Button { Text = text, Flat = true, CustomMinimumSize = new Vector2(190, 42) };
        private static Label CreateHeading(string text, int size, Color color) { var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label; }
        private static StyleBoxFlat CreateBox(Color fill, Color border, int width) { var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 }; box.SetBorderWidthAll(width); return box; }
    }
}
