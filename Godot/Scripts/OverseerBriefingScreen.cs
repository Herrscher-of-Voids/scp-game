namespace Scp.Godot
{
    using System;
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 中文：O5 任命档案全屏页，只读展示新生时间线创建时持久化的席位、前任交接和优先事项；确认时原子持久化 BriefingAcknowledged，再以继续请求进入总览。
    /// English: Full-screen O5 appointment dossier that read-only displays the seat, predecessor handover, and priorities persisted when a new timeline is created; acknowledgement atomically persists BriefingAcknowledged before continuing to the overview.
    /// 中文：从备份启动时，玩家已在主标题明确同意回退；确认任命会把所载备份作为同一 SaveId 的新主档原子保存，不静默选择或回退其他版本。
    /// English: When launched from a backup, the player already explicitly accepted rollback on the title screen; acknowledgement atomically writes that loaded backup as the primary for the same SaveId without silently selecting or rolling back another version.
    /// </summary>
    public sealed partial class OverseerBriefingScreen : Control
    {
        private SaveRepository _repository = null!;
        private SaveFile? _save;
        private VBoxContainer _dossier = null!;
        private Label _error = null!;
        private Button _acknowledge = null!;
        private Button _return = null!;
        private bool _saving;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            BuildUi();
            LoadBriefing();
        }

        /// <summary>
        /// 中文：建立黑色终端背景、档案标题、可滚动分区正文和底部操作区；布局单位为 Godot 逻辑像素，不创建或修改任何存档数据。
        /// English: Builds the black terminal background, dossier heading, scrollable sections, and footer actions; layout uses Godot logical pixels and never creates or mutates save data.
        /// </summary>
        private void BuildUi()
        {
            var background = new PanelContainer();
            background.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddThemeStyleboxOverride("panel", CreateBox(new Color("050506"), new Color("050506"), 0, 0));
            AddChild(background);

            var margin = new MarginContainer
            {
                OffsetLeft = 56,
                OffsetTop = 26,
                OffsetRight = -56,
                OffsetBottom = -26
            };
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddChild(margin);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);

            root.AddChild(CreateHeading("O5 任命档案", 34, new Color("f2f2f4")));
            root.AddChild(CreateHeading("O5 APPOINTMENT DOSSIER / NEW TIMELINE", 15, new Color("a7a7ad")));
            root.AddChild(CreateHeading("最高机密 · 仅限获任监督者阅览", 15, new Color("c45b5b")));
            root.AddChild(new HSeparator());

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            root.AddChild(scroll);

            _dossier = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _dossier.AddThemeConstantOverride("separation", 12);
            scroll.AddChild(_dossier);

            _error = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _error.AddThemeColorOverride("font_color", new Color("e65a5a"));
            root.AddChild(_error);

            var actions = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                CustomMinimumSize = new Vector2(0, 48)
            };
            actions.AddThemeConstantOverride("separation", 16);
            _return = CreateButton("返回主标题", 210);
            _return.Pressed += ReturnToTitle;
            actions.AddChild(_return);
            _acknowledge = CreateButton("确认接任，进入总览", 280);
            _acknowledge.Pressed += Acknowledge;
            actions.AddChild(_acknowledge);
            root.AddChild(actions);
        }

        /// <summary>
        /// 中文：消费加载页交付的一次性已加载 SaveFile，并将确定性元数据组织为任命档案；页面不再次读盘，也不自行寻找主档、备份或其他存档。
        /// English: Consumes the one-shot loaded SaveFile delivered by the loader and organizes deterministic metadata into an appointment dossier; this page never reads disk again or searches primary, backup, or another save.
        /// 返回/边界：缺少交接即显示错误并禁用确认，不用演示数据掩盖正式启动失败；空优先事项显示明确缺失状态。
        /// Return/boundary: missing handoff shows an error and disables acknowledgement rather than masking a launch failure with demo data; absent priorities show an explicit missing state.
        /// </summary>
        private void LoadBriefing()
        {
            try
            {
                _save = GameLaunchContext.ConsumeLoaded() ?? throw new InvalidOperationException("Missing loaded briefing save.");
                _repository = GameLaunchContext.CreateRepository();
                OverseerBriefingMetadata briefing = _save.Briefing;

                AddSection("任命令 / APPOINTMENT ORDER",
                    "经内部授权程序确认，你即刻接任 " + ValueOrUnknown(briefing.SeatDesignation) + " 席位，并承担基金会战略监督职责。\n\n"
                    + "本档案不披露姓名、性别或具体官方人物身份。你的身份仅以本局确定性分配的匿名席位表示。");
                AddSection("获任席位 / ASSIGNED SEAT", ValueOrUnknown(briefing.SeatDesignation));
                AddSection("前任离席 / PREDECESSOR STATUS", ValueOrUnknown(briefing.PredecessorDepartureCategory));
                AddSection("基金会状态 / FOUNDATION STATUS", ValueOrUnknown(briefing.FoundationStatusSummary));
                AddSection("优先简报 / PRIORITY BRIEFS", BuildPriorityText(briefing.PriorityBriefs));
                AddSection("前任遗产 / PREDECESSOR LEGACY",
                    ValueOrUnknown(briefing.PredecessorLegacy)
                    + "\n\n本页仅进行交接记录。具体战略事项与报告将在进入总览后由相应系统呈现。");
                AddSection("接任确认 / ACKNOWLEDGEMENT",
                    "确认接任将原子保存本次任命确认，并进入 O5 总览。确认前返回主标题不会删除该存档；下次继续仍会回到本任命档案。");
            }
            catch (Exception exception)
            {
                GD.PrintErr("OverseerBriefing load failed: " + exception);
                _error.Text = "无法载入任命交接资料。请返回主标题后重试。";
                _acknowledge.Disabled = true;
            }
        }

        /// <summary>
        /// 中文：将标题和正文包装为只读档案分区；title 与 text 都是已持久化或固定的玩家可见文本，正文自动换行且不接收输入。
        /// English: Wraps a heading and body as a read-only dossier section; title and text are persisted or fixed player-visible copy, the body wraps automatically and receives no input.
        /// </summary>
        private void AddSection(string title, string text)
        {
            PanelContainer panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", CreateBox(new Color("0d0d10"), new Color("72727a"), 1, 12));
            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 7);
            panel.AddChild(body);

            var heading = new Label { Text = title };
            heading.AddThemeFontSizeOverride("font_size", 16);
            heading.AddThemeColorOverride("font_color", new Color("c9c9cf"));
            body.AddChild(heading);
            body.AddChild(new HSeparator());

            var content = new Label
            {
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.AddThemeFontSizeOverride("font_size", 18);
            body.AddChild(content);
            _dossier.AddChild(panel);
        }

        private static string BuildPriorityText(string[] priorities)
        {
            return priorities == null || priorities.Length == 0
                ? "（未记录优先简报）"
                : string.Join("\n", priorities);
        }

        private static string ValueOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "（未记录）" : value;

        private void Acknowledge()
        {
            if (_save == null || _saving) return;
            _saving = true;
            _acknowledge.Disabled = true;
            _return.Disabled = true;
            try
            {
                _save.BriefingAcknowledged = true;
                _save.SavedAtUtc = DateTime.UtcNow;
                // 中文：确认后的真实 SaveFile 交给加载页原子保存；保存成功前不允许进入总览，避免目标页看到未持久化状态。
                // English: Hand the real acknowledged SaveFile to the loader for atomic persistence; overview entry is forbidden until persistence succeeds, so it never sees an unsaved state.
                GameLaunchContext.SetWork(new GameLaunchRequest { Kind = GameLaunchKind.PersistLoadedGame, SaveId = _save.SaveId, Candidate = _save, ReturnScene = "res://Main.tscn" });
                Error transition = GetTree().ChangeSceneToFile("res://TerminalLoading.tscn");
                if (transition != Error.Ok) throw new InvalidOperationException("Loading scene transition rejected: " + transition);
            }
            catch (Exception exception)
            {
                GD.PrintErr("OverseerBriefing acknowledgement save failed: " + exception);
                _save.BriefingAcknowledged = false;
                _error.Text = "无法保存接任确认。任命页将保留，请检查存储权限后重试。";
                _saving = false;
                _acknowledge.Disabled = false;
                _return.Disabled = false;
            }
        }

        /// <summary>中文：返回主标题不删除新建档，也不更改未确认状态；下次继续仍会路由到本页。English: Returning to title neither deletes the new save nor changes its unacknowledged state, so the next continue routes here again.</summary>
        private void ReturnToTitle()
        {
            if (_saving) return;
            Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
            if (error != Error.Ok)
            {
                GD.PrintErr("OverseerBriefing title scene change failed: " + error);
                _error.Text = "无法返回主标题，请稍后重试。";
            }
        }

        private static Label CreateHeading(string text, int size, Color color)
        {
            var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        private static Button CreateButton(string text, float width) => new()
        {
            Text = text,
            Flat = true,
            CustomMinimumSize = new Vector2(width, 44)
        };

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 17 };
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("d8d8dc"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            return theme;
        }

        private static StyleBoxFlat CreateBox(Color fill, Color border, int width, int margin)
        {
            var box = new StyleBoxFlat
            {
                BgColor = fill,
                BorderColor = border,
                ContentMarginLeft = margin,
                ContentMarginRight = margin,
                ContentMarginTop = margin,
                ContentMarginBottom = margin
            };
            box.SetBorderWidthAll(width);
            return box;
        }
    }
}
