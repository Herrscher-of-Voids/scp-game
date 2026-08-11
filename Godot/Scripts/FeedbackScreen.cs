namespace Scp.Godot
{
    using System;
    using System.Globalization;
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 中文：控制主标题“内测反馈”本地页面，包括完整问卷、必要数据快照、本地列表、逐条导出及强确认删除。
    /// English: Controls the main-title local beta-feedback page, including the full questionnaire, required data snapshot, local list, per-record export and strongly confirmed deletion.
    /// 参数与单位：文本长度由 Application 仓库按字符验证，时间以 UTC 存储并按本地时间显示，日志总上限为 65536 字符。
    /// Parameters and units: the Application repository validates text in characters, time is stored in UTC and shown locally, and logs are capped at 65,536 characters total.
    /// 返回值：玩家操作只写入或复制 user://feedback 内的 JSON；页面不会联网，也不会产生“已提交”状态。
    /// Return value: player actions only write or copy JSON under user://feedback; the screen never uses the network or produces a "submitted" state.
    /// 边界与隐私：日志必须明确勾选，且仓库只读 user://logs 顶层受支持文件；界面不显示绝对路径或异常详情。
    /// Boundaries and privacy: logs require explicit opt-in and the repository reads only supported top-level files under user://logs; the UI never displays absolute paths or exception details.
    /// 确定性与原因：列表按 UTC 创建时间和反馈 ID 稳定排序；反馈数据独立于存档与设置，避免删除或损坏互相影响。
    /// Determinism and rationale: records sort stably by UTC creation time and feedback ID; feedback stays separate from saves and settings so deletion or corruption cannot cross domains.
    /// </summary>
    public sealed partial class FeedbackScreen : Control
    {
        private const string GameVersion = "0.1.0-alpha";
        private BetaFeedbackRepository _repository = null!;
        private AudioManager _audio = null!;
        private OptionButton _category = null!;
        private OptionButton _severity = null!;
        private LineEdit _title = null!;
        private TextEdit _reproduction = null!;
        private TextEdit _expected = null!;
        private TextEdit _actual = null!;
        private TextEdit _description = null!;
        private CheckButton _includeLogs = null!;
        private VBoxContainer _feedbackList = null!;
        private Label _details = null!;
        private Label _status = null!;
        private Button _export = null!;
        private Button _delete = null!;
        private ConfirmationDialog _deleteConfirmation = null!;
        private LineEdit _deleteInput = null!;
        private BetaFeedback? _selected;

        /// <summary>
        /// 中文：初始化独立反馈与日志根目录，创建全部控件并读取现有本地记录；不创建网络客户端。
        /// English: Initializes separate feedback and log roots, builds all controls and reads existing local records without creating a network client.
        /// </summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            _audio = GetNode<AudioManager>("/root/AudioManager");
            _repository = new BetaFeedbackRepository(
                ProjectSettings.GlobalizePath("user://feedback"),
                ProjectSettings.GlobalizePath("user://logs"));
            BuildUi();
            RefreshList();
        }

        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color("060607"));

        private void BuildUi()
        {
            var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 28, OffsetTop = 20, OffsetRight = -28, OffsetBottom = -20 };
            AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 8);
            margin.AddChild(root);
            root.AddChild(new Label { Text = "内测反馈 / BETA FEEDBACK", HorizontalAlignment = HorizontalAlignment.Center });
            var privacy = new Label
            {
                Text = "仅保存在本机，不会自动上传。反馈与存档、设置相互独立。保存后可逐条导出 JSON 文件。",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            privacy.AddThemeColorOverride("font_color", new Color("f0c674"));
            root.AddChild(privacy);
            root.AddChild(new HSeparator());

            var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            root.AddChild(body);
            body.AddChild(BuildQuestionnaire());
            body.AddChild(BuildLocalRecords());

            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            root.AddChild(actions);
            AddActionButton(actions, "返回主标题", ReturnToTitle);
            AddActionButton(actions, "清空问卷", ClearQuestionnaire);
            AddActionButton(actions, "保存到本机", SaveLocally);
            _export = AddActionButton(actions, "导出所选", ExportSelected);
            _delete = AddActionButton(actions, "删除所选", AskDeleteSelected);
            _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_status);

            _deleteConfirmation = new ConfirmationDialog
            {
                Title = "永久删除本地反馈",
                OkButtonText = "永久删除",
                CancelButtonText = "取消",
                DialogText = "删除后无法恢复。请输入完整反馈 ID 以确认："
            };
            _deleteConfirmation.Confirmed += ConfirmDelete;
            _deleteConfirmation.Canceled += () => _deleteInput.Text = string.Empty;
            _deleteInput = new LineEdit { PlaceholderText = "输入完整反馈 ID" };
            _deleteConfirmation.AddChild(_deleteInput);
            AddChild(_deleteConfirmation);
        }

        /// <summary>
        /// 中文：构建包含全部确认字段的滚动问卷；星号字段均由仓库再次验证，不能仅依赖界面状态。
        /// English: Builds the scrolling questionnaire with every confirmed field; the repository revalidates all starred fields rather than trusting UI state alone.
        /// </summary>
        private Control BuildQuestionnaire()
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.62f };
            panel.AddThemeStyleboxOverride("panel", Box(new Color("101013"), new Color("77777e"), 1));
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            panel.AddChild(scroll);
            var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            form.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(form);
            form.AddChild(new Label { Text = "完整问卷（* 为必填）" });
            _category = AddOption(form, "分类 *", new[] { "程序错误", "游戏体验", "界面", "性能", "无障碍", "本地化", "其他" });
            _severity = AddOption(form, "严重程度 *", new[] { "低", "中", "高", "致命" });
            _severity.Select(1);
            _title = AddLine(form, "标题 *", "简要概括问题（最多 120 字符）", 120);
            _reproduction = AddText(form, "复现步骤 *", "请按顺序列出如何触发问题");
            _expected = AddText(form, "期望结果 *", "你预期发生什么");
            _actual = AddText(form, "实际结果 *", "实际发生了什么");
            _description = AddText(form, "自由描述 *", "补充背景、频率或其他观察");
            _includeLogs = new CheckButton
            {
                Text = "我明确同意附加匿名日志（仅 user://logs；脱敏用户目录；总计最多 65536 字符）",
                ButtonPressed = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _audio.BindButton(_includeLogs);
            form.AddChild(_includeLogs);
            form.AddChild(new Label
            {
                Text = "不会采集：用户名、系统用户目录、完整路径、个人文件、设备指纹。不会扫描 user://logs 之外的文件。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            form.AddChild(new Label
            {
                Text = FormatSnapshot(CreateSnapshot()),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            return panel;
        }

        private Control BuildLocalRecords()
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.38f };
            panel.AddThemeStyleboxOverride("panel", Box(new Color("101013"), new Color("77777e"), 1));
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 6);
            panel.AddChild(root);
            root.AddChild(new Label { Text = "本地反馈列表" });
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
            root.AddChild(scroll);
            _feedbackList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(_feedbackList);
            _details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 180) };
            root.AddChild(_details);
            return panel;
        }

        /// <summary>
        /// 中文：捕获当前页面必要游戏信息。主标题反馈页没有活动存档，因此不读取最近存档，也不推测身份、难度或种子。
        /// English: Captures required game information for this page. With no active save on the title feedback screen, it neither reads the latest save nor guesses identity, difficulty or seed.
        /// </summary>
        private BetaFeedbackDataSnapshot CreateSnapshot() => new()
        {
            GameVersion = GameVersion,
            Platform = global::Godot.OS.GetName(),
            CurrentScene = GetTree().CurrentScene?.SceneFilePath ?? "res://Feedback.tscn",
            IdentityMode = "无活动游戏 / No active game",
            Difficulty = "无活动游戏 / No active game",
            RandomSeed = "无活动游戏 / No active game"
        };

        /// <summary>
        /// 中文：从控件创建候选反馈并交由仓库验证及原子写入；成功提示始终使用“已保存到本机”，不称为提交。
        /// English: Creates a feedback candidate from controls and delegates validation and atomic writing to the repository; success always says "saved locally", never submitted.
        /// </summary>
        private void SaveLocally()
        {
            var feedback = new BetaFeedback
            {
                Category = (BetaFeedbackCategory)_category.Selected,
                Severity = (BetaFeedbackSeverity)_severity.Selected,
                Title = _title.Text.Trim(),
                ReproductionSteps = _reproduction.Text.Trim(),
                ExpectedResult = _expected.Text.Trim(),
                ActualResult = _actual.Text.Trim(),
                Description = _description.Text.Trim(),
                IncludeAnonymousLogs = _includeLogs.ButtonPressed,
                DataSnapshot = CreateSnapshot()
            };
            BetaFeedbackValidationResult validation = BetaFeedbackRepository.Validate(feedback);
            if (!validation.IsValid)
            {
                _audio.PlayUiWarning();
                SetStatus(string.Join(" ", validation.Errors), true);
                return;
            }
            try
            {
                BetaFeedback saved = _repository.Save(feedback);
                ClearQuestionnaire();
                RefreshList(saved.FeedbackId);
                SetStatus("已保存到本机。反馈 ID：" + saved.FeedbackId + "。不会自动上传。", false);
            }
            catch (Exception exception)
            {
                GD.PrintErr("Feedback local save failed: " + exception);
                _audio.PlayUiWarning();
                SetStatus("本地保存失败，请检查存储权限。没有上传任何数据。", true);
            }
        }

        private void RefreshList(string selectedId = "")
        {
            foreach (Node child in _feedbackList.GetChildren()) child.QueueFree();
            BetaFeedback[] records = _repository.Enumerate();
            if (records.Length == 0) _feedbackList.AddChild(new Label { Text = "本机尚无反馈记录。" });
            _selected = null;
            foreach (BetaFeedback record in records)
            {
                var button = new Button
                {
                    Text = record.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) + " · " + record.Title + "\n" + record.FeedbackId,
                    Alignment = HorizontalAlignment.Left,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    FocusMode = FocusModeEnum.All
                };
                _audio.BindButton(button);
                button.Pressed += () => SelectRecord(record);
                _feedbackList.AddChild(button);
                if (record.FeedbackId == selectedId) _selected = record;
            }
            _selected ??= records.Length > 0 ? records[0] : null;
            UpdateSelection();
        }

        private void SelectRecord(BetaFeedback record)
        {
            _selected = record;
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            _export.Disabled = _selected == null;
            _delete.Disabled = _selected == null;
            if (_selected == null)
            {
                _details.Text = "选择一条反馈查看详情。";
                return;
            }
            _details.Text = "创建时间：" + _selected.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
                + "\n反馈 ID：" + _selected.FeedbackId
                + "\n分类 / 严重度：" + CategoryLabel(_selected.Category) + " / " + SeverityLabel(_selected.Severity)
                + "\n标题：" + _selected.Title
                + "\n匿名日志：" + (_selected.IncludeAnonymousLogs ? "玩家已同意，附加 " + _selected.AnonymousLogs.Count + " 个片段" : "未附加")
                + "\n\n此记录仅在本机，不会自动上传。";
        }

        private void ExportSelected()
        {
            if (_selected == null) return;
            try
            {
                string fileName = _repository.Export(_selected.FeedbackId);
                SetStatus("已导出到 user://feedback/exports/" + fileName + "。原本地记录仍保留。", false);
            }
            catch (Exception exception)
            {
                GD.PrintErr("Feedback export failed: " + exception);
                _audio.PlayUiWarning();
                SetStatus("导出失败，请检查存储权限。本地原记录未改变。", true);
            }
        }

        /// <summary>
        /// 中文：删除前显示不可恢复警告并要求逐字输入完整反馈 ID；仅弹窗确认按钮不足以执行删除。
        /// English: Before deletion, shows an irreversible warning and requires the exact full feedback ID; the dialog confirmation button alone cannot delete.
        /// </summary>
        private void AskDeleteSelected()
        {
            if (_selected == null) return;
            _deleteInput.Text = string.Empty;
            _deleteConfirmation.DialogText = "删除后无法恢复，且不会删除已导出的副本。\n请输入完整反馈 ID：\n" + _selected.FeedbackId;
            _deleteConfirmation.PopupCentered();
        }

        private void ConfirmDelete()
        {
            if (_selected == null) return;
            if (!string.Equals(_deleteInput.Text.Trim(), _selected.FeedbackId, StringComparison.Ordinal))
            {
                _audio.PlayUiWarning();
                SetStatus("确认内容不匹配，未删除任何反馈。", true);
                _deleteInput.Text = string.Empty;
                return;
            }
            try
            {
                string deletedId = _selected.FeedbackId;
                bool deleted = _repository.Delete(deletedId);
                RefreshList();
                SetStatus(deleted ? "已从本机永久删除反馈 " + deletedId + "。" : "反馈已不存在，未执行删除。", deleted);
            }
            catch (Exception exception)
            {
                GD.PrintErr("Feedback delete failed: " + exception);
                _audio.PlayUiWarning();
                SetStatus("删除失败，请检查存储权限。本地记录可能仍然存在。", true);
            }
            finally
            {
                _deleteInput.Text = string.Empty;
            }
        }

        private void ClearQuestionnaire()
        {
            _category.Select(0);
            _severity.Select(1);
            _title.Text = string.Empty;
            _reproduction.Text = string.Empty;
            _expected.Text = string.Empty;
            _actual.Text = string.Empty;
            _description.Text = string.Empty;
            _includeLogs.ButtonPressed = false;
        }

        private void ReturnToTitle()
        {
            Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
            if (error == Error.Ok) return;
            GD.PrintErr("Feedback title scene change failed: " + error);
            SetStatus("无法返回主标题，请稍后重试。", true);
        }

        private OptionButton AddOption(Control parent, string label, string[] values)
        {
            parent.AddChild(new Label { Text = label });
            var option = new OptionButton();
            foreach (string value in values) option.AddItem(value);
            _audio.BindButton(option);
            parent.AddChild(option);
            return option;
        }

        private static LineEdit AddLine(Control parent, string label, string placeholder, int maximumLength)
        {
            parent.AddChild(new Label { Text = label });
            var edit = new LineEdit { PlaceholderText = placeholder, MaxLength = maximumLength };
            parent.AddChild(edit);
            return edit;
        }

        private static TextEdit AddText(Control parent, string label, string placeholder)
        {
            parent.AddChild(new Label { Text = label });
            var edit = new TextEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 105), WrapMode = TextEdit.LineWrappingMode.Boundary };
            parent.AddChild(edit);
            return edit;
        }

        private Button AddActionButton(Control parent, string text, Action callback)
        {
            var button = new Button { Text = text, FocusMode = FocusModeEnum.All };
            _audio.BindButton(button);
            button.Pressed += callback;
            parent.AddChild(button);
            return button;
        }

        private void SetStatus(string message, bool warning)
        {
            _status.Text = message;
            _status.AddThemeColorOverride("font_color", warning ? new Color("e65a5a") : new Color("aaa8ae"));
        }

        private static string FormatSnapshot(BetaFeedbackDataSnapshot snapshot) => "默认附加的必要游戏信息：\n游戏版本：" + snapshot.GameVersion
            + " · 平台：" + snapshot.Platform + " · 当前场景：" + snapshot.CurrentScene
            + "\n身份模式：" + snapshot.IdentityMode + " · 难度：" + snapshot.Difficulty + " · 随机种子：" + snapshot.RandomSeed;

        private static string CategoryLabel(BetaFeedbackCategory category) => category switch
        {
            BetaFeedbackCategory.Bug => "程序错误", BetaFeedbackCategory.Gameplay => "游戏体验", BetaFeedbackCategory.Interface => "界面",
            BetaFeedbackCategory.Performance => "性能", BetaFeedbackCategory.Accessibility => "无障碍", BetaFeedbackCategory.Localization => "本地化", _ => "其他"
        };

        private static string SeverityLabel(BetaFeedbackSeverity severity) => severity switch
        {
            BetaFeedbackSeverity.Low => "低", BetaFeedbackSeverity.Medium => "中", BetaFeedbackSeverity.High => "高", _ => "致命"
        };

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("d8d8dc"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_focus_color", "Button", Colors.White);
            return theme;
        }

        private static StyleBoxFlat Box(Color fill, Color border, int width)
        {
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
            box.SetBorderWidthAll(width);
            return box;
        }
    }
}
