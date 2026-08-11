namespace Scp.Godot
{
    using System;
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 中文：O5 任命文件与交接摘要独立全屏页。它只读显示存档中的确定性元数据；确认时原子持久化 BriefingAcknowledged，再以继续请求进入总览。
    /// English: Independent full-screen O5 appointment and handover screen. It displays persisted deterministic metadata read-only, then atomically persists BriefingAcknowledged before entering the overview through a continue request.
    /// 中文：从备份启动时，玩家已在主标题明确同意回退；确认任命会把所载备份作为同一 SaveId 的新主档原子保存，不静默选择或回退其他版本。
    /// English: When launched from backup, the player already explicitly accepted rollback on the title screen; acknowledging writes that loaded backup as the new primary under the same SaveId without silently selecting or rolling back any other version.
    /// </summary>
    public sealed partial class OverseerBriefingScreen : Control
    {
        private SaveRepository _repository = null!;
        private SaveFile? _save;
        private Label _content = null!;
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

        private void BuildUi()
        {
            var background = new PanelContainer();
            background.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddThemeStyleboxOverride("panel", CreateBox(new Color("060607"), new Color("060607"), 0));
            AddChild(background);
            var margin = new MarginContainer { OffsetLeft = 64, OffsetTop = 32, OffsetRight = -64, OffsetBottom = -32 };
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddChild(margin);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);
            var title = new Label { Text = "O5 任命文件与交接摘要", HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 30);
            root.AddChild(title);
            var classification = new Label { Text = "最高机密 / 仅限获任监督者阅览", HorizontalAlignment = HorizontalAlignment.Center };
            classification.AddThemeColorOverride("font_color", new Color("c43c3c"));
            root.AddChild(classification);
            root.AddChild(new HSeparator());
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            root.AddChild(scroll);
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", CreateBox(new Color("101013"), new Color("77777e"), 1));
            scroll.AddChild(panel);
            _content = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddChild(_content);
            _error = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _error.AddThemeColorOverride("font_color", new Color("e65a5a"));
            root.AddChild(_error);
            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _return = new Button { Text = "返回主标题", Flat = true, CustomMinimumSize = new Vector2(210, 44) };
            _return.Pressed += ReturnToTitle;
            actions.AddChild(_return);
            _acknowledge = new Button { Text = "确认接任并进入总览", Flat = true, CustomMinimumSize = new Vector2(270, 44) };
            _acknowledge.Pressed += Acknowledge;
            actions.AddChild(_acknowledge);
            root.AddChild(actions);
        }

        /// <summary>
        /// 中文：消费加载页交付的一次性已加载 SaveFile；页面不再次读盘，也不自行寻找主档、备份或其他存档。
        /// English: Consumes the one-shot loaded SaveFile delivered by the loader; this page never reads disk again or searches primary, backup, or another save.
        /// 返回/边界：方法无返回值；缺少交接即显示错误并禁用确认，不用演示数据掩盖正式启动失败。
        /// Return/boundary: the method returns nothing; missing handoff shows an error and disables acknowledgement rather than masking formal launch failure with demo data.
        /// </summary>
        private void LoadBriefing()
        {
            try
            {
                _save = GameLaunchContext.ConsumeLoaded() ?? throw new InvalidOperationException("Missing loaded briefing save.");
                _repository = GameLaunchContext.CreateRepository();
                OverseerBriefingMetadata briefing = _save.Briefing;
                string briefs = briefing.PriorityBriefs.Length == 0 ? "（未记录）" : string.Join("\n", briefing.PriorityBriefs);
                _content.Text = "任命令\n经内部授权程序确认，你即刻接任 " + briefing.SeatDesignation + " 席位，并承担监督职责。"
                    + "\n\n玩家席位编号\n" + briefing.SeatDesignation
                    + "\n\n前任离席说明\n" + briefing.PredecessorDepartureCategory
                    + "\n\n基金会状态摘要\n" + briefing.FoundationStatusSummary
                    + "\n\n三份优先简报\n" + briefs
                    + "\n\n前任遗留政策 / 承诺 / 未结事项\n" + briefing.PredecessorLegacy
                    + "\n\n构建说明\n本页面 O5 人物为通用占位，不代表具体官方角色；设施来自 SCP-EN/CN 主站全球设施资料，事件规模仍在扩充。";
            }
            catch (Exception exception)
            {
                GD.PrintErr("OverseerBriefing load failed: " + exception);
                _error.Text = "无法载入任命交接资料。请返回主标题后重试。";
                _acknowledge.Disabled = true;
            }
        }

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

        /// <summary>中文：返回主标题不删除新建档，也不更改未确认状态；下次继续仍会路由到本页。English: Returning to title neither deletes the save nor changes its unacknowledged state, so the next continue routes here again.</summary>
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

        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 17 };
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("d8d8dc"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            return theme;
        }

        private static StyleBoxFlat CreateBox(Color fill, Color border, int width)
        {
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18 };
            box.SetBorderWidthAll(width);
            return box;
        }
    }
}
