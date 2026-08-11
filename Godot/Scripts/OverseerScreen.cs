namespace Scp.Godot
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using global::Godot;
    using Scp.Application;
    using Scp.Domain;
    using Scp.Simulation;

    /// <summary>
    /// 中文：O5 正式操作终端；所有显示值来自 O5 投影，所有改变状态的操作进入真实 GameSession 命令队列。
    /// English: Official O5 operation terminal; every displayed value comes from the O5 projection and every state change enters the real GameSession command queue.
    /// 参数/返回：Godot 生命周期参数 delta 为现实秒；界面构建方法返回对应控件；业务命令不在 UI 内伪造结果。
    /// Parameters/returns: Godot lifecycle delta is real seconds; UI builders return controls; business results are never fabricated in the UI.
    /// 边界/确定性：终局和重大事件停止推进；命令按 GameSession 的确定性 Tick 顺序执行。
    /// Edge/determinism: ended sessions and critical events stop advancement; commands execute through GameSession's deterministic Tick order.
    /// </summary>
    public sealed partial class OverseerScreen : Control
    {
        private const double SecondsPerTickAt1x = 1.0;
        private GameSession _session = null!;
        private readonly OverseerNotificationLog _notifications = new OverseerNotificationLog();
        private int _speedMultiplier;
        private double _tickAccumulator;
        private bool _isReady;
        private Label _dateLabel = null!;
        private Label _cycleLabel = null!;
        private PanelContainer _workspace = null!;
        private VBoxContainer _workspaceBody = null!;
        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        private WorldMapView _mapView = null!;
        private VBoxContainer _summaryList = null!;
        private VBoxContainer _alertList = null!;
        private VBoxContainer _notificationList = null!;
        private VBoxContainer _siteList = null!;
        private AudioManager? _audio;
        private ApplicationSettings _settings = ApplicationSettings.CreateDefault();
        private string _currentPage = "总览";
        private readonly List<string> _councilTranscript = new List<string>();
        private DecisionOverlay? _feedbackOverlay;
        private string _selectedReportId = string.Empty;
        private string _selectedFinanceDetail = "研究与实验";
        private readonly Dictionary<string, LineEdit> _financeEdits = new Dictionary<string, LineEdit>();
        private Label? _financeDraftStatus;
        // 中文：dirty 只表示玩家修改了当前九项文本；回调开关与页面代数共同阻止页面拆除期间旧输入框延迟写入。English: dirty means the player changed one of nine texts; callback gating and page generation prevent delayed writes during teardown.
        private bool _financeDraftDirty;
        private bool _financeCallbacksEnabled;
        private int _financePageGeneration;
        private string _financeInputError=string.Empty;
        private string _selectedCompensationIncident = string.Empty;
        /// <summary>中文：当前帷幕页选择的匿名事件稳定 ID；仅属会话 UI 状态，不写存档。English: Stable ID of the anonymous incident selected on the veil page; session-only UI state, not persisted.</summary>
        private string _selectedVeilIncident = string.Empty;
        // 中文：底部明细的显式折叠状态只影响当前会话界面比例；普通科目默认收起，事故卡打开时默认展开，玩家可随时切换且不写入世界存档。
        // English: The explicit bottom-detail disclosure state affects only the current session layout; ordinary categories default collapsed, incident cards default expanded, and the player may toggle it without changing world saves.
        private bool _financeDetailExpanded;

        /// <summary>中文：创建终端并恢复会话。English: Creates the terminal and restores the session.</summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            Theme = CreateTheme();
            _audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
            _settings = new ApplicationSettingsStore(ProjectSettings.GlobalizePath("user://settings/settings.json")).Load();
            BuildUi();
            try
            {
                // 中文：正常启动优先消费加载页已完成真实工作的 SaveFile，避免再次完整读盘；直接运行 Overseer.tscn 没有交接时才构建确定性演示世界。
                // English: Normal launch first consumes the SaveFile produced by completed loader work to avoid another full disk read; only direct Overseer.tscn runs without handoff build the deterministic demo world.
                SaveFile? loaded = GameLaunchContext.ConsumeLoaded();
                if (loaded != null)
                {
                    _session = GameSession.Restore(loaded, new OverseerPerspective());
                }
                else
                {
                    string dataDirectory = ProjectSettings.GlobalizePath("res://Assets/Data/Scps");
                    ScpDefinition[] definitions = new ScpContentLoader().LoadDirectory(dataDirectory);
                    // 中文：正式设施目录与 SCP 内容同为演示世界输入；加载器强制校验数量、唯一 ID、排除项与来源字段。
                    // English: The official facility catalogue joins SCP content as demo-world input; loaders enforce count, unique IDs, exclusions, and source fields.
                    string facilityFile = ProjectSettings.GlobalizePath("res://Assets/Data/Facilities/o5-facilities.json");
                    FacilityDefinition[] facilities = new FacilityDataLoader().LoadFile(facilityFile);
                    _session = new GameSession(OverseerScenarioFactory.CreateDemoWorld(definitions, facilities), new OverseerPerspective());
                }
                _isReady = true;
                Refresh();
            }
            catch (Exception exception)
            {
                GD.PrintErr("OverseerScreen: session creation failed: " + exception);
                ShowLaunchFailure("无法载入游戏。请返回主标题后检查存档状态或存储权限。");
            }
        }

        /// <summary>中文：按现实时间推进游戏小时；0 倍率、终局或重大事件均禁止推进。English: Advances game hours from real time; pause, endings and critical events forbid advancement.</summary>
        public override void _Process(double delta)
        {
            if (!_isReady || _speedMultiplier <= 0 || _session.World.Failure.IsEnded) return;
            _tickAccumulator += delta * _speedMultiplier;
            if (_tickAccumulator < SecondsPerTickAt1x) return;
            int ticks = (int)(_tickAccumulator / SecondsPerTickAt1x);
            _tickAccumulator -= ticks * SecondsPerTickAt1x;
            TickResult result = _session.Advance(ticks);
            _notifications.Append(result.Events);
            if (_notifications.HasCriticalSinceLastAppend) SetSpeed(0);
            Refresh();
        }

        private void BuildUi()
        {
            var background = new PanelContainer();
            background.SetAnchorsPreset(LayoutPreset.FullRect);
            background.AddThemeStyleboxOverride("panel", CreateBox(GodotArt.OverseerBackground, GodotArt.OverseerRule, 0));
            AddChild(background);
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 0);
            background.AddChild(root);
            root.AddChild(BuildTitleBar());
            root.AddChild(BuildControlBar());
            _workspace = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            _workspace.AddThemeStyleboxOverride("panel", CreateBox(GodotArt.OverseerPanel, GodotArt.OverseerRule, 1));
            _workspaceBody = new VBoxContainer();
            _workspace.AddChild(_workspaceBody);
            root.AddChild(_workspace);
            ShowPage("总览");
        }

        private Control BuildTitleBar()
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 44) };
            panel.AddThemeStyleboxOverride("panel", CreateBox(GodotArt.OverseerPanel, GodotArt.OverseerRule, 1));
            var row = new HBoxContainer();
            panel.AddChild(row);
            row.AddChild(new Label { Text = "O5 监督者终端 · 最高机密", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
            row.AddChild(new Label { Text = "LEVEL 5", VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        private Control BuildControlBar()
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 48) };
            panel.AddThemeStyleboxOverride("panel", CreateBox(GodotArt.OverseerPanel, GodotArt.OverseerRule, 1));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            panel.AddChild(row);
            var overview = new Button { Text = "⌂", TooltipText = "返回世界总览", CustomMinimumSize = new Vector2(40, 0) };
            overview.Pressed += () => ShowPage("总览"); _audio?.BindButton(overview); row.AddChild(overview);
            row.AddChild(new VSeparator());
            foreach (string title in new[] { "财政", "帷幕", "报告", "O5会议" })
            {
                var button = new Button { Text = title };
                button.Pressed += () => ShowPage(title);
                _tabButtons[title] = button;
                _audio?.BindButton(button);
                row.AddChild(button);
            }
            row.AddChild(new VSeparator());
            AddSpeedButton(row, "暂停", 0);
            AddSpeedButton(row, "1×", 1);
            AddSpeedButton(row, "2×", 2);
            AddSpeedButton(row, "4×", 4);
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            _dateLabel = AddReadout(row);
            row.AddChild(new VSeparator());
            _cycleLabel = AddReadout(row);
            // 中文：右上角只保留日期、周期与天数；完整现金金额已在财政页六指标展示，删除重复读数可释放表格横向空间。
            // English: The top-right area retains only date, cycle, and day; full cash already appears in the six finance metrics, so removing the duplicate readout frees horizontal table space.
            return panel;
        }

        private void ShowPage(string page)
        {
            if (!_isReady && page != "总览") return;
            // 中文：先关闭财政回调再移除控件；离页时若存在无效脏文本，仅恢复投影中的上一个有效草案，不提交命令，保证导航永远不会制造零草案。
            // English: Finance callbacks are disabled before controls are removed; dirty invalid text is restored from the last valid projection without submitting a command, ensuring navigation can never manufacture a zero draft.
            _financeCallbacksEnabled=false;
            if(_currentPage=="财政"&&page!="财政")RestoreInvalidFinanceInputs();
            // 中文：总览控件只归总览页所有；离页前清空字段引用，防止全局 Refresh 访问已 RemoveChild/QueueFree 的旧节点并中断当前页面刷新。
            // English: Overview controls belong only to the overview page; clear field references before leaving so global Refresh cannot touch removed/queued-for-free nodes and abort the active page update.
            if (_currentPage == "总览" && page != "总览")
            {
                _summaryList = null!;
                _alertList = null!;
                _notificationList = null!;
                _siteList = null!;
            }
            _currentPage = page;
            Clear(_workspaceBody);
            foreach (KeyValuePair<string, Button> tab in _tabButtons) tab.Value.Disabled = tab.Key == page;
            switch (page)
            {
                case "财政": BuildFinancePage(); break;
                case "帷幕": BuildVeilPage(); break;
                case "报告": BuildReportPage(); break;
                case "O5会议": BuildCouncilPage(); break;
                default: BuildOverviewPage(); break;
            }
        }

        private void BuildOverviewPage()
        {
            var upper = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.65f };
            PanelContainer summary = CreatePanel("基金会全局信息", out _summaryList); summary.SizeFlagsStretchRatio = 0.4f;
            _alertList = new VBoxContainer(); _summaryList.AddChild(new HSeparator()); _summaryList.AddChild(new Label { Text = "全球警报" }); _summaryList.AddChild(_alertList);
            PanelContainer map = CreatePanel("世界地图 · 左键拖动 / 滚轮缩放", out VBoxContainer mapBody); map.SizeFlagsStretchRatio = 0.6f;
            _mapView = new WorldMapView { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; mapBody.AddChild(_mapView);
            upper.AddChild(summary); upper.AddChild(map); _workspaceBody.AddChild(upper);
            var lower = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.35f };
            PanelContainer notifications = CreatePanel("通知记录", out _notificationList); notifications.SizeFlagsStretchRatio = 0.4f;
            PanelContainer sites = CreatePanel("设施列表 · 单击预览 / 双击详情", out _siteList); sites.SizeFlagsStretchRatio = 0.6f;
            lower.AddChild(notifications); lower.AddChild(sites); _workspaceBody.AddChild(lower);
        }

        /// <summary>
        /// 中文：构建完整财政工作台：顶部指标、左四渠道、中九项预算与独立储备管理、右风险/待签、底部明细和真实历史。English: Builds the finance desk with top metrics, four channels, nine budgets, independent reserve management, risks, obligations, detail, and history.
        /// </summary>
        private void BuildFinancePage()
        {
            OverseerViewModel view=Project(); FinanceViewModel finance=view.Finance; _financeEdits.Clear();_financeDraftDirty=false;_financeInputError=string.Empty;int generation=++_financePageGeneration;
            var header=new HBoxContainer { CustomMinimumSize=new Vector2(0,58) }; header.AddThemeConstantOverride("separation",4);
            AddFinanceMetric(header,"可用现金",finance.AvailableCash,GodotArt.Information); AddFinanceMetric(header,"总资产",finance.TotalAssets,GodotArt.Information);
            AddFinanceMetric(header,"本月收入",finance.MonthlyIncome,GodotArt.Positive); AddFinanceMetric(header,"本月支出",finance.MonthlyExpenses,GodotArt.Critical);
            AddFinanceMetric(header,"净流量",finance.NetCashFlow,finance.NetCashFlow>=0?GodotArt.Positive:GodotArt.Critical);
            var reserve=MetricLabel("储备支撑",finance.ReserveMonths.ToString("F1",CultureInfo.InvariantCulture)+" 月",finance.ReserveMonths<1?GodotArt.Warning:GodotArt.Information); reserve.TooltipText="独立应急储备余额 / 必要月度支出"; header.AddChild(reserve);
            _workspaceBody.AddChild(header);

            // 中文：主体与底部按 80/20 或 65/35 分配可用高度；不设置固定像素最小高度，防止 1280×720 时预算首行被底栏挤出并裁切。
            // English: Main and detail areas divide available height as 80/20 or 65/35; no fixed pixel minimum is imposed, preventing the footer from pushing the first budget row outside the 1280x720 viewport.
            bool detailExpanded=FinanceDetailLayoutPolicy.IsExpanded(_selectedCompensationIncident,_financeDetailExpanded);var main=new HBoxContainer { SizeFlagsVertical=SizeFlags.ExpandFill, SizeFlagsStretchRatio=detailExpanded?.65f:.77f }; main.AddThemeConstantOverride("separation",5); _workspaceBody.AddChild(main);
            PanelContainer incomePanel=CreatePanel("四类并行资金来源",out VBoxContainer incomeBody,false); incomePanel.SizeFlagsStretchRatio=.23f;
            var income=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};income.AddThemeConstantOverride("separation",1);incomeBody.AddChild(income);
            foreach(FundingChannelViewModel channel in finance.Channels) AddFundingChannelCard(income,channel);main.AddChild(incomePanel);

            PanelContainer budgetPanel=CreatePanel("九项本月预算草案",out VBoxContainer budgetBody,false); budgetPanel.SizeFlagsStretchRatio=.54f;
            var reserveManagement=new HBoxContainer();AddMoneyKeyValue(reserveManagement,"独立储备余额",finance.ReserveBalance,GodotArt.Information);AddMoneyKeyValue(reserveManagement,"必要月度支出",finance.NecessaryMonthlyExpenses,GodotArt.Warning);AddKeyValue(reserveManagement,"支撑",finance.ReserveMonths.ToString("F1",CultureInfo.InvariantCulture)+" 月",GodotArt.Information);budgetBody.AddChild(reserveManagement);
            // 中文：表头与十行共用同一个六列 GridContainer；每个单元格均 ExpandFill 且不跨列、不设不同 StretchRatio，因此六列严格等宽并天然逐像素对齐。删除滑杆、刻线和加减按钮，而非隐藏，避免无效控件再次挤坏列宽。
            // English: Header and all ten rows share one six-column GridContainer; every cell uses ExpandFill with no spans or unequal stretch ratios, making all six columns strictly equal and pixel-aligned. Sliders, markers, and nudge buttons are deleted rather than hidden so dead controls cannot distort widths again.
            var budgetGrid=new GridContainer{Columns=6,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};budgetGrid.AddThemeConstantOverride("h_separation",2);budgetGrid.AddThemeConstantOverride("v_separation",1);budgetBody.AddChild(budgetGrid);
            foreach(string heading in new[]{"科目","上月","本月草案（亿元）","变化","最低线","比例"})AddBudgetHeader(budgetGrid,heading);
            for(int i=0;i<finance.BudgetLines.Length;i++)AddBudgetEditor(budgetGrid,finance.BudgetLines[i],i);main.AddChild(budgetPanel);

            PanelContainer riskPanel=CreatePanel("风险 / 义务 / 决定",out VBoxContainer riskBody,false); riskPanel.SizeFlagsStretchRatio=.23f;
            var riskScroll=new ScrollContainer { SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; var risks=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill}; risks.AddThemeConstantOverride("separation",3); riskScroll.AddChild(risks); riskBody.AddChild(riskScroll);
            BuildFinanceRisks(risks,finance); main.AddChild(riskPanel);

            BuildFinanceDetail(finance);
            var footer=new HBoxContainer { CustomMinimumSize=new Vector2(0,42) }; footer.AddThemeConstantOverride("separation",8);
            _financeDraftStatus=new Label { Text=BuildDraftStatus(finance),SizeFlagsHorizontal=SizeFlags.ExpandFill,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart }; _financeDraftStatus.AddThemeColorOverride("font_color",finance.IsDraftRecorded?GodotArt.Information:GodotArt.OverseerMuted); footer.AddChild(_financeDraftStatus);
            var discard=new Button { Text="撤销草案",CustomMinimumSize=new Vector2(112,34),TooltipText="放弃本月未签发草案并恢复正式预算" }; discard.Pressed+=DiscardFinanceDraft; _audio?.BindButton(discard); footer.AddChild(discard);
            var sign=new Button { Text="签发本月预算",CustomMinimumSize=new Vector2(156,34),TooltipText="正式生效九项预算并写入财政决定历史" }; sign.AddThemeColorOverride("font_color",GodotArt.Positive); sign.AddThemeStyleboxOverride("normal",CreateBox(new Color("10251a"),GodotArt.Positive,2)); sign.Pressed+=SignFinanceBudget; _audio?.BindButton(sign); footer.AddChild(sign); _workspaceBody.AddChild(footer);
            _financeCallbacksEnabled=generation==_financePageGeneration;
        }

        /// <summary>中文：向严格等宽六列网格追加亿元编辑行。文本变化只标脏；Enter/失焦通过十项原子组装统一保存，任一无效值均恢复该框的上一个有效值、以红框标记并把完整原因写到底部状态栏。页面代数和回调开关阻止拆除事件，成功保存清除 dirty，故 Enter 后 FocusExited 最多保存一次。English: Appends a yi editor row to the strictly equal six-column grid. Text changes only mark the page dirty; Enter/focus loss saves through one atomic ten-field assembly. Any invalid value restores that field's last valid text, marks it with a red border, and writes the full reason to the footer. Page generation and callback gating block teardown events, while successful save clears dirty so FocusExited after Enter saves at most once.</summary>
        private void AddBudgetEditor(Node parent,BudgetLineViewModel line,int rowIndex)
        {
            string key=line.Key;long validAmount=line.DraftAmount;long comparison=line.PreviousAmount??line.BaselineAmount;int generation=_financePageGeneration;
            var select=new Button{Text=key,CustomMinimumSize=new Vector2(0,27),SizeFlagsHorizontal=SizeFlags.ExpandFill,TooltipText="选择科目并在底部查看明细"};select.Pressed+=()=>{_selectedFinanceDetail=key;_selectedCompensationIncident=string.Empty;ShowPage("财政");};_audio?.BindButton(select);parent.AddChild(select);
            var actual=BudgetCell(line.PreviousAmount.HasValue?FinanceBudgetAmountParser.FormatYi(line.PreviousAmount.Value)+" 亿":"—",line.PreviousAmount.HasValue?FinanceAmountFormatter.FormatFull(line.PreviousAmount.Value):"尚无已结算周期");actual.AddThemeColorOverride("font_color",GodotArt.OverseerMuted);parent.AddChild(actual);
            var edit=new LineEdit{Text=FinanceBudgetAmountParser.FormatYi(validAmount),CustomMinimumSize=new Vector2(0,27),SizeFlagsHorizontal=SizeFlags.ExpandFill,TooltipText=FinanceAmountFormatter.FormatFull(validAmount)};_financeEdits[key]=edit;parent.AddChild(edit);
            var change=BudgetCell(DescribeBudgetChange(line.ChangeAmount,line.ChangePercent),line.ChangeBasis+"；变化金额 / 变化率");change.AddThemeColorOverride("font_color",ChangeColor(line.ChangeAmount));parent.AddChild(change);
            string risk=line.DraftAmount<line.MinimumLine?"不足":line.DraftAmount==line.MinimumLine?"警告":"安全";var minimum=BudgetCell(FinanceBudgetAmountParser.FormatYi(line.MinimumLine)+" 亿｜"+risk,"临时最低维持线为集中预算基准的 80%");minimum.AddThemeColorOverride("font_color",risk=="安全"?GodotArt.Positive:GodotArt.Warning);parent.AddChild(minimum);
            var ratio=BudgetCell(line.RatioPercent.ToString("F0",CultureInfo.InvariantCulture)+"%","相对预算基准");ratio.AddThemeColorOverride("font_color",GodotArt.OverseerMuted);parent.AddChild(ratio);
            void Commit(){if(!_financeCallbacksEnabled||generation!=_financePageGeneration||!_financeDraftDirty)return;if(!FinanceBudgetAmountParser.TryParseYi(edit.Text,out long parsed,out string message)){edit.Text=FinanceBudgetAmountParser.FormatYi(validAmount);edit.TooltipText=FinanceAmountFormatter.FormatFull(validAmount);MarkFinanceInputInvalid(edit,key+"："+message);_financeDraftDirty=false;_audio?.PlayUiWarning();return;}validAmount=parsed;edit.Text=FinanceBudgetAmountParser.FormatYi(parsed);edit.TooltipText=FinanceAmountFormatter.FormatFull(parsed);if(!SaveFinanceDraft())return;ClearFinanceInputError(edit);long delta=validAmount-comparison;decimal? percent=comparison>0?decimal.Round(delta*100m/comparison,1):null;change.Text=DescribeBudgetChange(delta,percent);change.AddThemeColorOverride("font_color",ChangeColor(delta));risk=validAmount<line.MinimumLine?"不足":validAmount==line.MinimumLine?"警告":"安全";minimum.Text=FinanceBudgetAmountParser.FormatYi(line.MinimumLine)+" 亿｜"+risk;ratio.Text=(line.BaselineAmount>0?decimal.Round(validAmount*100m/line.BaselineAmount,0):0).ToString("F0",CultureInfo.InvariantCulture)+"%";}
            edit.FocusEntered+=()=>edit.SelectAll();edit.TextChanged+=_=>{if(_financeCallbacksEnabled&&generation==_financePageGeneration){_financeDraftDirty=true;ClearFinanceInputError(edit);}};edit.TextSubmitted+=_=>Commit();edit.FocusExited+=Commit;
        }

        private static void AddBudgetHeader(Node parent,string text){var panel=new PanelContainer{CustomMinimumSize=new Vector2(0,24),SizeFlagsHorizontal=SizeFlags.ExpandFill};panel.AddThemeStyleboxOverride("panel",CreateCompactBox(new Color("15242b"),GodotArt.Information,1,1));var label=new Label{Text=text,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,SizeFlagsHorizontal=SizeFlags.ExpandFill};label.AddThemeColorOverride("font_color",GodotArt.Information);panel.AddChild(label);parent.AddChild(panel);}
        private static Label BudgetCell(string text,string tooltip){return new Label{Text=text,TooltipText=tooltip,CustomMinimumSize=new Vector2(0,25),SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};}
        private static Color ChangeColor(long amount)=>amount<0?GodotArt.Critical:amount>0?GodotArt.Positive:GodotArt.Information;
        private static string DescribeBudgetChange(long amount,decimal? percent)=>amount==0?"0.00 亿\n0.0%":FinanceBudgetAmountParser.FormatYi(amount)+" 亿\n"+(percent.HasValue?percent.Value.ToString("+0.0;-0.0;0.0",CultureInfo.InvariantCulture)+"%":amount>0?"新增":"—");

        /// <summary>中文：仅在玩家确实修改且十项亿元文本全部有效时原子保存草案；任何失败均不提交命令、不覆盖投影中的有效草案。返回值供 Enter、失焦和签发路径决定是否继续，成功后清除 dirty 以抑制重复 FocusExited。English: Atomically saves only when the player changed text and all ten yi fields are valid; any failure submits no command and cannot overwrite the valid projected draft. The return value lets Enter, focus loss, and signing decide whether to continue, and success clears dirty to suppress duplicate FocusExited.</summary>
        private bool SaveFinanceDraft(bool requireDirty=true)
        {
            if(requireDirty&&!_financeDraftDirty)return true;var texts=new Dictionary<string,string>(StringComparer.Ordinal);foreach(KeyValuePair<string,LineEdit> item in _financeEdits)texts[item.Key]=item.Value.Text;
            if(!FinanceBudgetDraftAssembler.TryAssemble(texts,Project().Finance.DraftBudget,out BudgetState? budget,out string error)||budget==null){_financeInputError=error;UpdateFinanceDraftStatus();_audio?.PlayUiWarning();return false;}
            ValidationResult result=_session.TryApplyFinanceDraft(new SaveBudgetDraftCommand { Budget=budget });if(!result.IsValid){_financeInputError="草案记录被阻止："+result.Error;UpdateFinanceDraftStatus();_audio?.PlayUiWarning();return false;}_financeDraftDirty=false;_financeInputError=string.Empty;UpdateFinanceDraftStatus();return true;
        }

        /// <summary>中文：将无效框标为红色并在底部状态栏展示完整原因；不增加行高，避免错误文字压住下一行。English: Marks an invalid editor with a red border and shows the complete reason in the footer without increasing row height or overlapping the next row.</summary>
        private void MarkFinanceInputInvalid(LineEdit edit,string error){var box=CreateCompactBox(new Color("211015"),GodotArt.Critical,2,3);edit.AddThemeStyleboxOverride("normal",box);edit.AddThemeStyleboxOverride("focus",box);edit.TooltipText=error;_financeInputError=error;UpdateFinanceDraftStatus();}
        private void ClearFinanceInputError(LineEdit edit){edit.RemoveThemeStyleboxOverride("normal");edit.RemoveThemeStyleboxOverride("focus");if(_financeInputError.Length>0){_financeInputError=string.Empty;UpdateFinanceDraftStatus();}}
        private void UpdateFinanceDraftStatus(){if(_financeDraftStatus==null)return;FinanceViewModel finance=Project().Finance;_financeDraftStatus.Text=_financeInputError.Length>0?"输入错误｜"+_financeInputError:BuildDraftStatus(finance);_financeDraftStatus.AddThemeColorOverride("font_color",_financeInputError.Length>0?GodotArt.Critical:finance.IsDraftRecorded?GodotArt.Information:GodotArt.OverseerMuted);}
        private void RestoreInvalidFinanceInputs(){if(!_financeDraftDirty)return;var texts=new Dictionary<string,string>(StringComparer.Ordinal);foreach(KeyValuePair<string,LineEdit> item in _financeEdits)texts[item.Key]=item.Value.Text;if(FinanceBudgetDraftAssembler.TryAssemble(texts,Project().Finance.DraftBudget,out _,out _))return;BudgetViewModel valid=Project().Finance.DraftBudget;foreach(KeyValuePair<string,LineEdit> item in _financeEdits){long amount=EditOrProjected(item.Key,valid);item.Value.Text=FinanceBudgetAmountParser.FormatYi(amount);item.Value.TooltipText=FinanceAmountFormatter.FormatFull(amount);} _financeDraftDirty=false;_financeInputError=string.Empty;}

        private void SignFinanceBudget(){if(!SaveFinanceDraft(false))return;ValidationResult result=_session.TrySubmit(new SignBudgetCommand());if(!result.IsValid){_audio?.PlayUiWarning();ShowMessage("预算签发被阻止："+result.Error);return;}ExecutePending();ShowPage("财政");PlayDecisionFeedback("本月预算已签发",false);}
        private void DiscardFinanceDraft(){ValidationResult result=_session.TryApplyFinanceDraft(new DiscardBudgetDraftCommand());if(!result.IsValid){ShowMessage(result.Error);return;}ShowPage("财政");}

        /// <summary>
        /// 中文：底部建立内容自适应明细区，普通状态占约 20%、显式展开占约 35%；标题栏始终保留展开/收起按钮，内容放入滚动区，空历史仅占一行。研究二级分配只展示且不重复结算。
        /// English: Builds a content-adaptive detail area using roughly 20% normally and 35% when explicitly expanded; the title always retains a disclosure button, content scrolls when needed, and empty history occupies one line. Research allocations are display-only and never double-settled.
        /// </summary>
        private void BuildFinanceDetail(FinanceViewModel finance)
        {
            bool expanded=FinanceDetailLayoutPolicy.IsExpanded(_selectedCompensationIncident,_financeDetailExpanded);var panel=new PanelContainer { SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsStretchRatio=expanded?.35f:.23f }; panel.AddThemeStyleboxOverride("panel",CreateBox(new Color("0b1115"),new Color("6f1720"),2));var shell=new VBoxContainer();shell.AddThemeConstantOverride("separation",1);panel.AddChild(shell);_workspaceBody.AddChild(panel);
            var heading=new HBoxContainer();heading.AddChild(new Label{Text="当前明细｜"+(_selectedCompensationIncident.Length>0?"事故抚恤":_selectedFinanceDetail),SizeFlagsHorizontal=SizeFlags.ExpandFill});var disclosure=new Button{Text=expanded?"收起明细":"展开明细",CustomMinimumSize=new Vector2(96,24)};disclosure.Pressed+=()=>{_financeDetailExpanded=!expanded;ShowPage("财政");};_audio?.BindButton(disclosure);heading.AddChild(disclosure);shell.AddChild(heading);
            var body=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",1);if(expanded){var detailScroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};detailScroll.AddChild(body);shell.AddChild(detailScroll);}else shell.AddChild(body);
            CompensationIncidentViewModel? incident=Array.Find(finance.CompensationIncidents,item=>item.IncidentId==_selectedCompensationIncident);if(incident!=null){BuildCompensationDetail(body,incident);return;}
            BudgetViewModel budget=finance.DraftBudget;
            if(_selectedFinanceDetail=="研究与实验")
            {
                var research=new HBoxContainer(); AddDetailAmount(research,"基础研究",budget.ResearchDetail.BasicResearch,GodotArt.Information);AddDetailAmount(research,"重点项目",budget.ResearchDetail.PriorityProjects,GodotArt.Information);AddDetailAmount(research,"收容技术",budget.ResearchDetail.ContainmentTechnology,GodotArt.Information);AddDetailAmount(research,"异常应用",budget.ResearchDetail.AnomalousApplications,GodotArt.Warning);body.AddChild(research);
                long detailTotal=budget.ResearchDetail.Total();var total=MoneyLabel("研究明细汇总："+FormatMoney(detailTotal)+(detailTotal==budget.Research?" · 与一级草案一致":" · 与一级草案不一致"),detailTotal,detailTotal==budget.Research?GodotArt.Positive:GodotArt.Warning);body.AddChild(total);
            }
            else if(_selectedFinanceDetail=="安保"||_selectedFinanceDetail=="普通 MTF"||_selectedFinanceDetail=="Alpha-1")
            {
                var security=new HBoxContainer();AddDetailAmount(security,"站点安保",budget.SecurityDetail.SiteSecurity,GodotArt.Information);AddDetailAmount(security,"MTF 总部",budget.SecurityDetail.MtfHeadquarters,GodotArt.Information);AddDetailAmount(security,"队伍维护与部署",budget.SecurityDetail.MtfTeamMaintenance+budget.SecurityDetail.MtfDeployment,GodotArt.Information);AddDetailAmount(security,"Alpha-1",budget.SecurityDetail.AlphaOne,GodotArt.Warning);body.AddChild(security);body.AddChild(CreateMutedLabel("普通 MTF 汇总："+budget.SecurityDetail.MtfTeamCount+" 队；二级明细不重复计费。"));
            }
            else AddKeyValue(body,"本月草案",FormatMoney(EditOrProjected(_selectedFinanceDetail,budget)),GodotArt.Information);
            body.AddChild(new HSeparator());
            if(finance.CycleHistory.Length==0)body.AddChild(CreateMutedLabel("历史趋势｜尚无已结算周期"));
            else{body.AddChild(new Label { Text="历史趋势｜最新已结算周期在左" });var row=new HBoxContainer();foreach(FiscalCycleViewModel cycle in finance.CycleHistory){var item=new Label{Text="周期 "+cycle.Cycle+"\n收入 "+FormatMoney(cycle.Income)+"\n支出 "+FormatMoney(cycle.Expenses)+"\n净额 "+SignedMoney(cycle.NetCashFlow),SizeFlagsHorizontal=SizeFlags.ExpandFill,TooltipText="期末现金 "+FinanceAmountFormatter.FormatFull(cycle.ClosingCash)};item.AddThemeColorOverride("font_color",cycle.NetCashFlow>=0?GodotArt.Positive:GodotArt.Critical);row.AddChild(item);}body.AddChild(row);}
        }

        /// <summary>中文：事故展开区左侧用紧凑逐人表格集中姓名、完整整数货币金额与操作，右侧保留设施、报告 Tick、人数及状态摘要；抚恤解析和命令规则保持原样，不使用亿元解析器。English: The expanded incident area keeps names, full-integer money amounts, and actions together in a compact left table while the right side retains facility, report tick, headcount, and status summary; compensation parsing and command rules remain unchanged and never use the yi parser.</summary>
        private void BuildCompensationDetail(Node body,CompensationIncidentViewModel incident)
        {
            var split=new HBoxContainer();split.AddThemeConstantOverride("separation",8);body.AddChild(split);var left=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=.68f};left.AddThemeConstantOverride("separation",1);split.AddChild(left);var edits=new Dictionary<string,LineEdit>();var bulkRow=new HBoxContainer();var bulk=new LineEdit{PlaceholderText="批量完整货币整数",CustomMinimumSize=new Vector2(170,26),SizeFlagsHorizontal=SizeFlags.ExpandFill};bulkRow.AddChild(bulk);var apply=new Button{Text="批量设置",CustomMinimumSize=new Vector2(92,26)};apply.Pressed+=()=>{long amount=ParseMoney(bulk.Text);foreach(LineEdit edit in edits.Values)edit.Text=amount.ToString(CultureInfo.InvariantCulture);};bulkRow.AddChild(apply);left.AddChild(bulkRow);
            var table=new GridContainer{Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};table.AddThemeConstantOverride("h_separation",3);table.AddThemeConstantOverride("v_separation",1);table.AddChild(new Label{Text="姓名",SizeFlagsHorizontal=SizeFlags.ExpandFill});table.AddChild(new Label{Text="金额（完整整数）",HorizontalAlignment=HorizontalAlignment.Center,SizeFlagsHorizontal=SizeFlags.ExpandFill});table.AddChild(new Label{Text="状态",HorizontalAlignment=HorizontalAlignment.Center,SizeFlagsHorizontal=SizeFlags.ExpandFill});foreach(CompensationPersonViewModel person in incident.Personnel){table.AddChild(new Label{Text=person.Name,SizeFlagsHorizontal=SizeFlags.ExpandFill});var edit=new LineEdit{Text=person.Amount.ToString(CultureInfo.InvariantCulture),CustomMinimumSize=new Vector2(150,26),SizeFlagsHorizontal=SizeFlags.ExpandFill};edits[person.PersonnelId]=edit;void Save(){ValidationResult result=_session.TryApplyFinanceDraft(new SetCompensationAmountCommand{IncidentId=incident.IncidentId,PersonnelId=person.PersonnelId,Amount=ParseMoney(edit.Text)});if(!result.IsValid)ShowMessage("金额记录被阻止："+result.Error);}edit.TextSubmitted+=_=>Save();edit.FocusExited+=Save;table.AddChild(edit);table.AddChild(new Label{Text=DescribeCompensation(person.Status),HorizontalAlignment=HorizontalAlignment.Center,SizeFlagsHorizontal=SizeFlags.ExpandFill});}left.AddChild(table);
            var actions=new HBoxContainer();actions.AddThemeConstantOverride("separation",3);var pay=new Button{Text="签发支付"};pay.Pressed+=()=>SubmitCompensation(new PayCompensationCommand{IncidentId=incident.IncidentId},false);actions.AddChild(pay);var delay=new Button{Text="记录拖延"};delay.Pressed+=()=>SubmitCompensation(new DecideCompensationCommand{IncidentId=incident.IncidentId,Decision=CompensationStatus.Delayed},true);actions.AddChild(delay);var refuse=new Button{Text="记录拒绝"};refuse.Pressed+=()=>SubmitCompensation(new DecideCompensationCommand{IncidentId=incident.IncidentId,Decision=CompensationStatus.Refused},true);actions.AddChild(refuse);left.AddChild(actions);var summary=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=.32f};summary.AddChild(new Label{Text="事故摘要"});AddKeyValue(summary,"设施",incident.Facility,GodotArt.Information);AddKeyValue(summary,"报告","T"+incident.ReportedTick,GodotArt.OverseerMuted);AddKeyValue(summary,"涉及人员",incident.Personnel.Length+" 人",GodotArt.Information);AddKeyValue(summary,"当前状态",DescribeCompensation(incident.Status),GodotArt.Warning);split.AddChild(summary);
        }

        private void SubmitCompensation(ICommand command,bool adverse){ValidationResult result=_session.TrySubmit(command);if(!result.IsValid){_audio?.PlayUiWarning();ShowMessage("抚恤决定被阻止："+result.Error);return;}ExecutePending();_selectedCompensationIncident=string.Empty;ShowPage("财政");PlayDecisionFeedback(adverse?"决定已记入责任链":"抚恤已签发",adverse);}
        /// <summary>中文：左栏渠道使用独立有边界卡片，所有金额自动单位化并通过 Tooltip 暴露完整整数货币值；卡片固定五行字段以保证四类来源在 1280x720 默认视口可完整扫描。English: Each left-column channel uses its own bordered card; all money is auto-unit formatted with full integer currency in tooltips, and a fixed five-row field layout keeps all four sources scannable at the default 1280x720 viewport.</summary>
        private static void AddFundingChannelCard(Node parent,FundingChannelViewModel channel)
        {
            var card=new PanelContainer();card.AddThemeStyleboxOverride("panel",CreateCompactBox(new Color("0c1411"),channel.Risk>=4000?GodotArt.Warning:new Color("315043"),1,2));var body=new VBoxContainer();body.AddThemeConstantOverride("separation",0);card.AddChild(body);
            var title=new Label { Text=channel.Name };title.AddThemeColorOverride("font_color",GodotArt.Positive);body.AddChild(title);AddMoneyKeyValue(body,"收入",channel.Income,GodotArt.Positive);AddMoneyKeyValue(body,"固定成本",channel.FixedCost,GodotArt.Critical);AddKeyValue(body,"风险 / 关系",FormatRatio(channel.Risk)+" / "+FormatRatio(channel.Relationship),channel.Risk>=4000?GodotArt.Warning:GodotArt.Information);AddMoneyKeyValue(body,"本周期变化",channel.CycleChange,channel.CycleChange>=0?GodotArt.Positive:GodotArt.Critical,true);parent.AddChild(card);
        }

        /// <summary>中文：右栏始终显示三项量化风险、未支付义务、待处理事故和最近三次真实财政决定；无数据时分别显示零值或“尚无”，不合并事故份数与义务金额。English: The right column always shows three quantitative risks, unpaid obligations, pending incidents, and the latest three real fiscal decisions; absent data is represented separately as zero or none, never combining incident counts with obligation amounts.</summary>
        private void BuildFinanceRisks(Node parent,FinanceViewModel finance)
        {
            AddSectionTitle(parent,"量化风险");FinanceRiskSummaryViewModel summary=finance.RiskSummary;
            AddRisk(parent,"现金流风险",(summary.CashFlow<0?"缺口 ":"结余 ")+FinanceAmountFormatter.FormatAbsolute(summary.CashFlow),summary.CashFlow<0);
            AddRisk(parent,"储备风险","支撑 "+summary.ReserveMonths.ToString("F1",CultureInfo.InvariantCulture)+" 月",summary.ReserveMonths<1);
            AddRisk(parent,"流动性风险",summary.LiquidityGap>0?"现金缺口 "+FormatMoney(summary.LiquidityGap):"现金覆盖 "+(finance.MonthlyExpenses>0?decimal.Round(finance.AvailableCash/(decimal)finance.MonthlyExpenses,1):0)+" 月",summary.LiquidityGap>0);
            AddSectionTitle(parent,"义务与事故");if(summary.UnpaidObligations==0)parent.AddChild(CreateMutedLabel("当前无未支付财政义务"));else AddMoneyKeyValue(parent,"未支付义务",summary.UnpaidObligations,GodotArt.Warning);AddKeyValue(parent,"待处理事故",summary.PendingIncidentCount>0?summary.PendingIncidentCount+" 份":"当前无待处理事故",summary.PendingIncidentCount>0?GodotArt.Warning:GodotArt.Information);
            foreach(CompensationIncidentViewModel incident in finance.CompensationIncidents)
            {
                if(incident.Status==CompensationStatus.Paid||incident.Status==CompensationStatus.Refused)continue;var open=new Button { Text="待签事故｜"+incident.Facility+"\n"+incident.Personnel.Length+" 人 · "+DescribeCompensation(incident.Status),TooltipText=incident.IncidentId+" · 报告 T"+incident.ReportedTick,AutowrapMode=TextServer.AutowrapMode.WordSmart };open.Pressed+=()=>{_selectedCompensationIncident=incident.IncidentId;_financeDetailExpanded=true;ShowPage("财政");};_audio?.BindButton(open);parent.AddChild(open);
            }
            AddSectionTitle(parent,"最近决定");if(finance.RecentDecisions.Length==0)parent.AddChild(CreateMutedLabel("尚无已签发财政决定"));else foreach(FiscalDecisionViewModel decision in finance.RecentDecisions){string amount=decision.Amount!=0?" · "+FormatMoney(decision.Amount):string.Empty;var label=new Label{Text="周期 "+decision.Cycle+" · "+DescribeFiscalDecision(decision)+amount,AutowrapMode=TextServer.AutowrapMode.WordSmart,TooltipText="T"+decision.Tick+" · "+decision.SubjectId+(decision.Amount!=0?" · "+FinanceAmountFormatter.FormatFull(decision.Amount):string.Empty)};label.AddThemeColorOverride("font_color",decision.Decision=="Refused"||decision.Decision=="Delayed"?GodotArt.Warning:GodotArt.OverseerMuted);parent.AddChild(label);}
        }

        private static void AddDetailAmount(Node parent,string name,long value,Color color){var panel=new PanelContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};panel.AddThemeStyleboxOverride("panel",CreateBox(new Color("10171b"),new Color("2e414a"),1));var label=MoneyLabel(name+"\n"+FormatMoney(value),value,color);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;panel.AddChild(label);parent.AddChild(panel);}
        private static void AddFinanceMetric(Node parent,string name,long value,Color color){Label metric=MetricLabel(name,FormatMoney(value),color);metric.TooltipText=FinanceAmountFormatter.FormatFull(value);parent.AddChild(metric);}
        private static Label MetricLabel(string name,string value,Color color){var label=new Label{Text=name+"\n"+value,SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};label.AddThemeColorOverride("font_color",color);return label;}
        private static Label MoneyLabel(string text,long value,Color color){var label=new Label{Text=text,TooltipText=FinanceAmountFormatter.FormatFull(value)};label.AddThemeColorOverride("font_color",color);return label;}
        private static void AddMoneyKeyValue(Node parent,string key,long value,Color color,bool signed=false){var row=new HBoxContainer();row.AddChild(new Label{Text=key,SizeFlagsHorizontal=SizeFlags.ExpandFill});var label=MoneyLabel(signed?SignedMoney(value):FormatMoney(value),value,color);row.AddChild(label);parent.AddChild(row);}
        private static void AddKeyValue(Node parent,string key,string value,Color color){var row=new HBoxContainer();row.AddChild(new Label{Text=key,SizeFlagsHorizontal=SizeFlags.ExpandFill});var label=new Label{Text=value};label.AddThemeColorOverride("font_color",color);row.AddChild(label);parent.AddChild(row);}
        private static void AddRisk(Node parent,string name,string detail,bool warning){var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",CreateCompactBox(warning?new Color("21150d"):new Color("0d1519"),warning?GodotArt.Warning:new Color("31505b"),warning?2:1,3));var label=new Label{Text=name+"｜"+detail,AutowrapMode=TextServer.AutowrapMode.WordSmart};label.AddThemeColorOverride("font_color",warning?GodotArt.Warning:GodotArt.Information);panel.AddChild(label);parent.AddChild(panel);}
        /// <summary>中文：右栏分节标题使用独立底色与青蓝边线，保证空列表时各节仍具可扫描层级。English: Right-column section headings use a distinct fill and cyan rule so every section remains scannable even when its list is empty.</summary>
        private static void AddSectionTitle(Node parent,string text){var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",CreateCompactBox(new Color("15242b"),GodotArt.Information,1,2));var label=new Label{Text=text};label.AddThemeColorOverride("font_color",GodotArt.Information);panel.AddChild(label);parent.AddChild(panel);}
        private static string BuildDraftStatus(FinanceViewModel finance){if(finance.IsDraftRecorded)return "草案已记录：九项合计 "+FormatMoney(TotalBudget(finance.DraftBudget))+"（研究 "+FormatMoney(finance.DraftBudget.Research)+"）· 周期 "+finance.DraftRecordedCycle+" · Tick "+finance.DraftRecordedTick;if(finance.IsBudgetSignedThisCycle)return "本月预算已签发："+FormatMoney(TotalBudget(finance.EnactedBudget));return "尚无已保存草案｜修改金额后按 Enter 或移出焦点记录；签发后才正式生效";}
        private static long TotalBudget(BudgetViewModel b)=>checked(b.SiteOperations+b.ContainmentMaintenance+b.Research+b.Security+b.MobileTaskForces+b.AlphaOne+b.VeilAndCover+b.AdministrationAndIntelligence+b.PersonnelAndEthics);
        private static string RelativeScale(long amount,long baseline)=>(baseline>0?decimal.Round(amount*100m/baseline,0):0).ToString("F0",CultureInfo.InvariantCulture)+"%";
        // 中文：抚恤金额继续解析为完整非负整数货币单位；此函数绝不用于九项亿元预算。English: Compensation remains a full non-negative integer currency amount and is never parsed as one of nine yi budgets.
        private static long ParseMoney(string text)=>long.TryParse(text,NumberStyles.AllowThousands|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,CultureInfo.InvariantCulture,out long value)&&value>=0?value:0;
        private static string SignedMoney(long value)=>FinanceAmountFormatter.FormatSigned(value);
        private static string DescribeCompensation(CompensationStatus status)=>status switch{CompensationStatus.Paid=>"已支付",CompensationStatus.Delayed=>"已拖延",CompensationStatus.Refused=>"已拒绝",_=>"待处理"};
        private static string DescribeFiscalDecision(FiscalDecisionViewModel decision)=>decision.Kind switch{"BudgetSigned"=>"预算签发","EmergencyReserveDraw"=>"应急储备覆盖现金缺口","CompensationPaid"=>"抚恤支付","CompensationAmount"=>"抚恤金额记录","CompensationDisposition"=>decision.Decision=="Refused"?"拒绝抚恤":"拖延抚恤",_=>decision.Decision};
        private static long EditOrProjected(string key,BudgetViewModel b)=>key switch{"设施运营"=>b.SiteOperations,"收容维护"=>b.ContainmentMaintenance,"研究与实验"=>b.Research,"安保"=>b.Security,"普通 MTF"=>b.MobileTaskForces,"Alpha-1"=>b.AlphaOne,"帷幕与掩盖"=>b.VeilAndCover,"行政与情报"=>b.AdministrationAndIntelligence,"人员与伦理保障"=>b.PersonnelAndEthics,_=>0};

        /// <summary>
        /// 中文：按负责人手绘结构构建帷幕页：上部约三分之二为 2:1 地图与单事件纵向报告，下部约三分之一为十一项横向总览和四行两列处置。输入只来自 O5 投影；1024×576 与 1280×720 通过伸缩比和双向滚动保持可用，不产生业务状态。
        /// English: Builds the veil page from the owner's sketch: roughly two thirds for a 2:1 map and one vertically scrolling incident report, then one third for eleven horizontally scrolling metrics and a four-by-two action grid. Input comes only from the O5 projection; stretch ratios and directional scrolling preserve usability at 1024×576 and 1280×720 without creating business state.
        /// </summary>
        private void BuildVeilPage()
        {
            OverseerViewModel view = Project(); VeilViewModel veil = view.Veil;
            if (veil.Incidents.Length > 0 && Array.Find(veil.Incidents, item => item.StableId == _selectedVeilIncident) == null) _selectedVeilIncident = veil.Incidents[0].StableId;
            VeilIncidentViewModel? current = Array.Find(veil.Incidents, item => item.StableId == _selectedVeilIncident);
            var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; page.AddThemeConstantOverride("separation", 6);
            var upper = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .67f }; upper.AddThemeConstantOverride("separation", 6);
            var mapColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .64f }; mapColumn.AddThemeConstantOverride("separation", 3);
            mapColumn.AddChild(new Label { Text = "全球帷幕 " + FormatRatio(veil.GlobalIntegrity) + " · 左键拖动 / 滚轮缩放", CustomMinimumSize = new Vector2(0, 22), VerticalAlignment = VerticalAlignment.Center });
            var mapFrame = new AspectRatioContainer { Ratio = 2f, StretchMode = AspectRatioContainer.StretchModeEnum.Fit, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            _mapView = new WorldMapView { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            _mapView.VeilIncidentSelected += id => { _selectedVeilIncident = id; ShowPage("帷幕"); }; mapFrame.AddChild(_mapView); mapColumn.AddChild(mapFrame);
            _mapView.SetVeilIncidents(veil.Incidents);
            upper.AddChild(mapColumn);
            var reportPanel = CreatePanel("单事件报告", out VBoxContainer reportHost, false); reportPanel.CustomMinimumSize = new Vector2(330, 0); reportPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; reportPanel.SizeFlagsStretchRatio = .36f;
            var reportScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var reportBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; reportBody.AddThemeConstantOverride("separation", 4); reportScroll.AddChild(reportBody); reportHost.AddChild(reportScroll); BuildVeilReport(reportBody, current);
            upper.AddChild(reportPanel); page.AddChild(upper);

            var lower = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .33f }; lower.AddThemeConstantOverride("separation", 6);
            var overviewPanel = CreatePanel("全球总览 · 横向滚动 / 悬停查看七洲", out VBoxContainer overviewHost, false); overviewPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; overviewPanel.SizeFlagsStretchRatio = .72f;
            var overviewScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var cards = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; cards.AddThemeConstantOverride("separation", 5); overviewScroll.AddChild(cards); overviewHost.AddChild(overviewScroll);
            foreach (VeilOverviewMetricViewModel metric in veil.OverviewMetrics) AddVeilMetricCard(cards, metric);
            lower.AddChild(overviewPanel);
            var actionPanel = CreatePanel("事件处置", out VBoxContainer actionHost, false); actionPanel.CustomMinimumSize = new Vector2(300, 0); actionPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; actionPanel.SizeFlagsStretchRatio = .28f;
            var actions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; actions.AddThemeConstantOverride("h_separation", 4); actions.AddThemeConstantOverride("v_separation", 4); actionHost.AddChild(actions);
            foreach (VeilActionKind action in Enum.GetValues<VeilActionKind>()) { if (current != null) AddVeilAction(actions, current, action); else AddDisabledVeilAction(actions, action); }
            lower.AddChild(actionPanel); page.AddChild(lower); _workspaceBody.AddChild(page);
            if (veil.Alerts.Length > 0) _audio?.PlayDocumentCue(veil.Alerts[0].Severity == AlertSeverity.Critical);
        }

        /// <summary>中文：填充右侧唯一报告滚动区。报告按位置、严重度、涉及范围、估算人数、发现时间、状态、损失/恢复/暴露和同区时间线排序；Tick 只经 FoundationCalendar 格式化。English: Populates the sole right-report scroll area in location, severity, scope, estimated people, discovery time, status, loss/recovery/exposure, and same-area timeline order; ticks are formatted only through FoundationCalendar.</summary>
        private static void BuildVeilReport(VBoxContainer body, VeilIncidentViewModel? current)
        {
            if (current == null) { body.AddChild(new Label { Text = "未发现可报告的帷幕事件。", AutowrapMode = TextServer.AutowrapMode.WordSmart }); body.AddChild(CreateMutedLabel("全球监测持续运行；请在地图上选择事件标记。")); return; }
            body.AddChild(new Label { Text = current.Title, AutowrapMode = TextServer.AutowrapMode.WordSmart }); body.AddChild(CreateMutedLabel(current.StableId + " · " + current.SourceCategory));
            AddKeyValue(body, "位置", DescribeContinent(current.OriginContinent) + " · " + DescribeVeilPrecision(current.LocationPrecision), GodotArt.Information);
            AddKeyValue(body, "严重度", FormatRatio(current.Severity), current.Severity >= 7500 ? GodotArt.Critical : current.Severity >= 2500 ? GodotArt.Warning : GodotArt.Positive);
            AddKeyValue(body, "涉及范围", DescribeVeilScope(current), GodotArt.Information);
            AddKeyValue(body, "估算涉及人数", current.EstimatedAffectedPeople.ToString("N0", CultureInfo.InvariantCulture) + " 人（估算）", GodotArt.Warning);
            AddKeyValue(body, "发现准确时间", FoundationCalendar.FormatStandaloneDateTime(current.DiscoveredTick), GodotArt.OverseerMuted);
            AddKeyValue(body, "状态", DescribeVeilStatus(current.Status) + " · " + VeilIncidentService.DescribeStage(current.Stage), GodotArt.Information);
            AddKeyValue(body, "损失 / 恢复 / 暴露", FormatRatio(current.Loss) + " / " + FormatRatio(current.Recovery) + " / " + FormatRatioLong(current.Exposure), GodotArt.Warning);
            body.AddChild(new HSeparator()); body.AddChild(new Label { Text = "时间线" });
            if (current.Timeline.Length == 0) body.AddChild(CreateMutedLabel("当前没有事件记录。"));
            else foreach (VeilTimelineEntryViewModel entry in current.Timeline) body.AddChild(new Label { Text = FoundationCalendar.FormatStandaloneDateTime(entry.Tick) + " · " + DescribeVeilAction(entry.Action) + " · " + entry.Effect, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }

        /// <summary>中文：建立单张全局总览卡。卡面显示不重复计数的全球值，Tooltip 固定列出七洲，缺失项为 0；参数只读且无返回值。English: Creates one global metric card. The face shows a non-duplicated global value and the tooltip always lists seven continents with missing entries as zero; input is read-only and no value is returned.</summary>
        private static void AddVeilMetricCard(Node parent, VeilOverviewMetricViewModel metric)
        {
            string[] names = { "北美", "南美", "欧洲", "亚洲", "非洲", "大洋洲", "南极洲" };
            var tooltip = new System.Text.StringBuilder(metric.Title + " · 七洲明细");
            for (int index = 0; index < names.Length; index++) tooltip.Append('\n').Append(names[index]).Append("：").Append(FormatVeilMetric(metric, index < metric.ByContinent.Length ? metric.ByContinent[index] : 0));
            if (metric.TooltipNote.Length > 0) tooltip.Append('\n').Append(metric.TooltipNote);
            var card = new PanelContainer { CustomMinimumSize = new Vector2(136, 0), SizeFlagsVertical = SizeFlags.ExpandFill, TooltipText = tooltip.ToString() }; card.AddThemeStyleboxOverride("panel", CreateCompactBox(new Color("10191e"), new Color("31505b"), 1, 5));
            card.AddChild(new Label { Text = metric.Title + "\n" + FormatVeilMetric(metric, metric.Value), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore }); parent.AddChild(card);
        }

        /// <summary>中文：无事件时仍建立八枚禁用按钮以稳定四行两列布局；动作参数只决定标签，无返回值也不提交命令。English: Creates all eight disabled buttons when no incident exists to stabilise the four-by-two layout; the action only selects text, returns nothing, and submits no command.</summary>
        private static void AddDisabledVeilAction(Node parent, VeilActionKind action) => parent.AddChild(new Button { Text = DescribeVeilAction(action), Disabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill });
        /// <summary>中文：建立一个事件处置按钮；提交先严格验证，再在一个确定性 Tick 执行并刷新。English: Creates one incident-response button; submission is strictly validated, then executed in one deterministic tick before refresh.</summary>
        private void AddVeilAction(Node parent, VeilIncidentViewModel incident, VeilActionKind action)
        {
            var button = new Button { Text = DescribeVeilAction(action), Disabled = incident.Status is VeilIncidentStatus.Resolved or VeilIncidentStatus.Withdrawn, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            // 中文：提交期间立即锁定这一枚按钮并显示执行中；成功后页面重建会自然丢弃旧回调，失败则在原控件仍有效时恢复，避免 QueueFree 后回写旧节点。
            // English: Lock this button immediately and show an in-progress label while submitting; successful page rebuild discards the old callback naturally, while failures restore only a still-valid control before QueueFree.
            button.Pressed += () =>
            {
                if (button.IsQueuedForDeletion()) return;
                button.Disabled = true;
                button.Text = "执行中";
                ValidationResult result = _session.TrySubmit(new VeilIncidentActionCommand { IncidentId = incident.StableId, Action = action });
                if (!result.IsValid)
                {
                    button.Disabled = false;
                    button.Text = DescribeVeilAction(action);
                    _audio?.PlayUiWarning();
                    ShowMessage("帷幕处置被阻止：" + result.Error);
                    return;
                }
                ExecutePending();
                // 中文：ExecutePending 已立即推进一次命令 Tick并刷新投影；随后仅重建当前页，确保详情和时间线读取最新状态。
                // English: ExecutePending advances exactly one command Tick and refreshes the projection; rebuilding only the current page then reads the latest detail and timeline.
                ShowPage("帷幕");
            };
            _audio?.BindButton(button); parent.AddChild(button);
        }

        private static string DescribeVeilPrecision(VeilLocationPrecision value) => value switch { VeilLocationPrecision.Confirmed => "已确认位置", VeilLocationPrecision.Approximate => "近似区域", _ => "仅洲级" };
        private static string DescribeVeilAction(VeilActionKind value) => value switch { VeilActionKind.Monitor => "监测", VeilActionKind.Investigate => "调查", VeilActionKind.SuppressPublicity => "舆情压制", VeilActionKind.CoordinateInstitutions => "机构协调", VeilActionKind.AssessWitnessDisposition => "证人处置评估", VeilActionKind.EmergencyOperation => "紧急专项", VeilActionKind.Pause => "暂停", _ => "撤销" };
        /// <summary>中文：把事件状态翻译为玩家可读文本；参数是稳定业务枚举，返回值只用于显示且不改变状态。English: Translates a stable incident-status enum into player-facing text; the return is display-only and never changes state.</summary>
        private static string DescribeVeilStatus(VeilIncidentStatus value) => value switch { VeilIncidentStatus.Active => "活动", VeilIncidentStatus.Paused => "暂停", VeilIncidentStatus.Recovering => "恢复中", VeilIncidentStatus.Resolved => "已解决", _ => "已撤销" };
        /// <summary>中文：把七洲枚举翻译为固定中文名；未知越界值返回“未知洲”而不推测地点。English: Translates the seven-continent enum into fixed Chinese names; an out-of-range value returns “unknown continent” without inventing a location.</summary>
        private static string DescribeContinent(Continent value) => value switch { Continent.NorthAmerica => "北美洲", Continent.SouthAmerica => "南美洲", Continent.Europe => "欧洲", Continent.Asia => "亚洲", Continent.Africa => "非洲", Continent.Oceania => "大洋洲", Continent.Antarctica => "南极洲", _ => "未知洲" };
        /// <summary>中文：格式化涉及范围，依次给出去重洲名、节点数与事件位置精度；空洲数组明确显示 0 洲，不从坐标反推地点。English: Formats affected scope as deduplicated continents, node count, and incident location precision; an empty continent array explicitly shows zero and coordinates are never reverse-inferred.</summary>
        private static string DescribeVeilScope(VeilIncidentViewModel incident)
        {
            var names = new List<string>(); foreach (Continent continent in incident.InvolvedContinents) names.Add(DescribeContinent(continent));
            return (names.Count == 0 ? "0 洲" : string.Join("、", names)) + " · 节点 " + incident.Nodes.Length.ToString(CultureInfo.InvariantCulture) + " · " + DescribeVeilPrecision(incident.LocationPrecision);
        }
        /// <summary>中文：格式化可能跨节点累计而超过 10000 的万分比总量；参数单位为万分比点，负值夹到 0，返回百分比文本。English: Formats a node-accumulated ten-thousandth total that may exceed 10000; input is ten-thousandth points, negatives clamp to zero, and the return is percentage text.</summary>
        private static string FormatRatioLong(long value) => (Math.Max(0, value) / 100m).ToString("F1", CultureInfo.InvariantCulture) + "%";
        /// <summary>中文：按投影格式显示总览值；Ratio 单位万分比、People 单位人、Money 为 64 位货币，其余为整数。负数统一夹到 0，返回纯显示文本。English: Formats a projected metric: Ratio uses ten-thousandths, People uses persons, Money uses 64-bit currency, and other values are integers. Negatives clamp to zero and the return is display-only text.</summary>
        private static string FormatVeilMetric(VeilOverviewMetricViewModel metric, long value)
        {
            long safe = Math.Max(0, value);
            return metric.Format switch { VeilMetricFormat.Ratio => FormatRatioLong(safe), VeilMetricFormat.People => safe.ToString("N0", CultureInfo.InvariantCulture) + " 人", VeilMetricFormat.Money => FormatMoney(safe), _ => safe.ToString("N0", CultureInfo.InvariantCulture) };
        }

        /// <summary>
        /// 中文：构建真实报告工作台；复选框允许选择单条或同类低风险批次，四种决定均提交模拟命令，条件文本不在 UI 自行解释。
        /// English: Builds the real report workbench; checkboxes select one report or a same-category low-risk batch, all four decisions submit simulation commands, and condition text is never interpreted by UI.
        /// </summary>
        private void BuildReportPage()
        {
            OverseerViewModel view = Project();
            if (view.Reports.Length>0 && Array.Find(view.Reports,r=>r.Id==_selectedReportId)==null) _selectedReportId=view.Reports[0].Id;
            ReportViewModel? current=Array.Find(view.Reports,r=>r.Id==_selectedReportId);
            var selected = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
            var layout=new HBoxContainer { SizeFlagsVertical=SizeFlags.ExpandFill };
            var sidebar=new VBoxContainer { CustomMinimumSize=new Vector2(245,0) }; sidebar.AddChild(new Label { Text="报告目录" });
            var listScroll=new ScrollContainer { SizeFlagsVertical=SizeFlags.ExpandFill, HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; var list=new VBoxContainer(); listScroll.AddChild(list); sidebar.AddChild(listScroll);
            foreach(ReportViewModel report in view.Reports) { var row=new HBoxContainer(); var check=new CheckBox { Disabled=report.Status!=ReportStatus.Pending }; selected[report.Id]=check; row.AddChild(check); var open=new Button { Text=report.Id+"\n"+report.Category+" · "+report.Status, SizeFlagsHorizontal=SizeFlags.ExpandFill }; open.Pressed+=()=>{_selectedReportId=report.Id;ShowPage("报告");}; _audio?.BindButton(open); row.AddChild(open); list.AddChild(row); }
            if(view.Reports.Length==0) list.AddChild(CreateMutedLabel("当前没有报告。")); layout.AddChild(sidebar);
            var paper=new PanelContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill }; paper.AddThemeStyleboxOverride("panel",CreateBox(new Color(.86f,.85f,.8f),new Color(.18f,.18f,.18f),1));
            var paperScroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; var document=new VBoxContainer(); paperScroll.AddChild(document); paper.AddChild(paperScroll); layout.AddChild(paper);
            if(current!=null)
            {
                var title=new Label { Text=current.Title, HorizontalAlignment=HorizontalAlignment.Center, AutowrapMode=TextServer.AutowrapMode.WordSmart }; title.AddThemeFontSizeOverride("font_size",22); title.AddThemeColorOverride("font_color",new Color(.07f,.07f,.07f)); document.AddChild(title);
                document.AddChild(PaperLabel(current.Id+" · "+current.Category+" · "+current.Risk+" · T"+current.CreatedTick)); document.AddChild(new HSeparator()); document.AddChild(PaperLabel("摘要\n"+current.Summary)); document.AddChild(PaperLabel("正文\n原始正文未随报告下发。摘要不作为正文替代。"));
                document.AddChild(PaperLabel("固定结构总结\n目的：未提供  ｜  收益：未提供  ｜  风险：未提供\n成本：未提供  ｜  伦理影响：未提供  ｜  建议决定：未提供")); document.AddChild(PaperLabel("责任链\n来源："+current.Source+"\n创建：T"+current.CreatedTick+"\n其他责任节点：未提供"));
                document.AddChild(PaperLabel("模拟 / 档案附件（不构成现实证据）"));
                document.AddChild(CreateUnavailableImage("生成资源不可用；未接入服务占位图。",130,true));
                document.AddChild(PaperLabel("公开审批记录")); foreach(ReportApprovalViewModel approval in view.ReportApprovals) if(Array.IndexOf(approval.ReportIds,current.Id)>=0) document.AddChild(PaperLabel("T"+approval.DecidedTick+" · "+approval.Decision+(approval.Conditions.Length>0?" · "+approval.Conditions:"")));
            }
            _workspaceBody.AddChild(layout);
            var conditions = new LineEdit { PlaceholderText = "附条件格式：budget_cap=整数;deadline_cycles=正整数;audit_required=true|false" }; _workspaceBody.AddChild(conditions);
            var actions = new HBoxContainer(); AddReportAction(actions,"批准",ReportStatus.Approved,selected,conditions); AddReportAction(actions,"驳回",ReportStatus.Rejected,selected,conditions); AddReportAction(actions,"退回补充",ReportStatus.Returned,selected,conditions); AddReportAction(actions,"附条件批准",ReportStatus.ConditionallyApproved,selected,conditions); _workspaceBody.AddChild(actions);
        }

        /// <summary>中文：绑定报告决定按钮并统一提交，避免四条路径产生不同验证行为。English: Binds a report-decision button to one submission path so all four decisions share validation behavior.</summary>
        private void AddReportAction(Node parent, string label, ReportStatus decision, Dictionary<string, CheckBox> selected, LineEdit conditions)
        {
            var button = new Button { Text = label };
            button.Pressed += () => SubmitReportDecision(decision, selected, conditions.Text);
            _audio?.BindButton(button); parent.AddChild(button);
        }

        /// <summary>中文：收集所选稳定 ID，经 TrySubmit 在日志前严格验证；失败播放警示且不推进 Tick。English: Collects selected stable IDs and strictly validates via TrySubmit before logging; failure warns and does not advance the tick.</summary>
        private void SubmitReportDecision(ReportStatus decision, Dictionary<string, CheckBox> selected, string conditions)
        {
            var ids = new List<string>(); foreach (var item in selected) if (item.Value.ButtonPressed) ids.Add(item.Key);
            var command = new ReportApprovalCommand { ReportIds = ids.ToArray(), Decision = decision, Conditions = conditions };
            ValidationResult validation = _session.TrySubmit(command);
            if (!validation.IsValid) { _audio?.PlayUiWarning(); ShowMessage("报告提交被阻止：" + validation.Error); return; }
            ExecutePending(); ShowPage("报告");
            string feedback = decision == ReportStatus.Approved ? "批准" : decision == ReportStatus.ConditionallyApproved ? "附条件批准" : decision == ReportStatus.Returned ? "退回修改" : "驳回";
            PlayDecisionFeedback(feedback, decision == ReportStatus.Rejected || decision == ReportStatus.Returned);
        }

        /// <summary>
        /// 中文：构建第一视角会议演出；全景为首屏主画面，NPC 热点只使用公开席位编号，业务命令收纳在议程抽屉。
        /// English: Builds first-person council presentation with the panorama as the primary viewport, public seat-number hotspots, and business commands inside an agenda drawer.
        /// </summary>
        private void BuildCouncilPage()
        {
            OverseerViewModel view=Project(); var npc=new List<CouncilSeatViewModel>(); foreach(CouncilSeatViewModel seat in view.Seats) if(seat.IsOccupied&&!seat.IsPlayer) npc.Add(seat);
            var stage=new Control { CustomMinimumSize=new Vector2(0,455), SizeFlagsVertical=SizeFlags.ExpandFill }; _workspaceBody.AddChild(stage);
            // 中文：位图生成受阻期间使用匿名程序轮廓承载正式席位交互，不冒充预渲染资源。
            // English: While bitmap generation is blocked, anonymous procedural silhouettes carry the seat interaction without pretending to be pre-rendered assets.
            var panorama=new CouncilPanoramaView { SeatCount=view.Seats.Length, ReducedMotion=ReducedMotion }; panorama.SetAnchorsPreset(LayoutPreset.FullRect); stage.AddChild(panorama);
            for(int i=0;i<npc.Count;i++) { CouncilSeatViewModel seat=npc[i]; double angle=Math.PI*(1.08+i/(double)Math.Max(1,npc.Count-1)*.84); float x=.5f+(float)Math.Cos(angle)*.43f; float y=.68f+(float)Math.Sin(angle)*.42f; var hotspot=new Button { Text="O5-"+seat.SeatId.Number, TooltipText="查看 O5-"+seat.SeatId.Number+" 公开发言", CustomMinimumSize=new Vector2(66,32) }; hotspot.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); hotspot.AnchorLeft=x;hotspot.AnchorTop=y;hotspot.AnchorRight=x;hotspot.AnchorBottom=y;hotspot.OffsetLeft=-33;hotspot.OffsetTop=-16;hotspot.OffsetRight=33;hotspot.OffsetBottom=16; int number=seat.SeatId.Number; hotspot.Pressed+=()=>ShowCouncilSpeaker(stage,number,BuildProceduralLine(view,number)); _audio?.BindButton(hotspot); stage.AddChild(hotspot); }
            var caption=new Label { Text="固定全景 / 点击编号席位查看公开程序发言", HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Bottom, MouseFilter=MouseFilterEnum.Ignore }; caption.SetAnchorsPreset(LayoutPreset.FullRect); stage.AddChild(caption);
            var drawerToggle=new Button { Text="▸ 会议议程与操作", ToggleMode=true }; var drawer=new VBoxContainer { Visible=false }; drawerToggle.Toggled+=open=>{drawer.Visible=open;drawerToggle.Text=(open?"▾ ":"▸ ")+"会议议程与操作";}; _audio?.BindButton(drawerToggle); _workspaceBody.AddChild(drawerToggle); _workspaceBody.AddChild(drawer);
            var commandRow=new HBoxContainer(); var seatChoice=new OptionButton(); foreach(CouncilSeatViewModel seat in npc) seatChoice.AddItem("O5-"+seat.SeatId.Number,seat.SeatId.Number); commandRow.AddChild(seatChoice);
            var lobby=new Button { Text="游说" }; lobby.Pressed+=()=>SubmitSeatCommand(seatChoice,false); _audio?.BindButton(lobby); commandRow.AddChild(lobby); var pressure=new Button { Text="施压" }; pressure.Pressed+=()=>SubmitSeatCommand(seatChoice,true); _audio?.BindButton(pressure); commandRow.AddChild(pressure);
            var kind=new OptionButton(); foreach(ProposalKind item in Enum.GetValues(typeof(ProposalKind))) kind.AddItem(item.ToString()); commandRow.AddChild(kind); var propose=new Button { Text="提交提案" }; propose.Pressed+=()=>SubmitProposal(kind); _audio?.BindButton(propose); commandRow.AddChild(propose); drawer.AddChild(commandRow);
            foreach(ProposalViewModel proposal in view.Proposals) if(!proposal.IsResolved) { var vote=new Button { Text="提案 #"+proposal.ProposalId+" · "+proposal.Kind+" · 公开投票（支持）" }; int id=proposal.ProposalId; vote.Pressed+=()=>SubmitVote(id); _audio?.BindButton(vote); drawer.AddChild(vote); }
            drawer.AddChild(CreateMutedLabel("公开投票记录 "+view.VoteRecords.Length+" 条；不显示隐藏立场、关系、压力或投票概率。"));
        }

        private void SubmitSeatCommand(OptionButton choice, bool pressure)
        {
            if (choice.ItemCount == 0) { ShowMessage("没有可操作的匿名 NPC 席位。"); return; }
            SeatId id = new SeatId(choice.GetItemId(choice.Selected)); ICommand command=pressure ? new PressureSeatCommand { SeatId = id } : new LobbySeatCommand { SeatId = id };
            ValidationResult validation=_session.TrySubmit(command); if(!validation.IsValid){_audio?.PlayUiWarning();ShowMessage("会议操作被阻止："+validation.Error);return;} ExecutePending(); ShowPage("O5会议");
        }

        /// <summary>中文：严格验证并提交提案，门槛只由公开提案类型规则决定。English: Strictly validates and submits a proposal whose threshold follows only public proposal-kind rules.</summary>
        private void SubmitProposal(OptionButton kind)
        {
            ProposalKind selected=(ProposalKind)kind.Selected; ProposalThreshold threshold=selected==ProposalKind.Experiment||selected==ProposalKind.Diplomacy||selected==ProposalKind.Impeachment?ProposalThreshold.TwoThirds:selected==ProposalKind.WorldRestart?ProposalThreshold.Unanimous:ProposalThreshold.SimpleMajority;
            ValidationResult validation=_session.TrySubmit(new SubmitProposalCommand { Kind=selected,Threshold=threshold,Position=new AxisPosition(0,0,0) }); if(!validation.IsValid){_audio?.PlayUiWarning();ShowMessage("提案被阻止："+validation.Error);return;} ExecutePending(); ShowPage("O5会议");
        }

        /// <summary>中文：提交玩家对真实待决议案的公开支持票，成功后刷新会议。English: Casts the player's public support vote on a real pending proposal and refreshes the council after success.</summary>
        private void SubmitVote(int proposalId)
        {
            ValidationResult validation=_session.TrySubmit(new CastPlayerVoteCommand { ProposalId=proposalId,Choice=VoteChoice.Support }); if(!validation.IsValid){_audio?.PlayUiWarning();ShowMessage("投票被阻止："+validation.Error);return;} ExecutePending(); ShowPage("O5会议");
        }

        /// <summary>
        /// 中文：按 0.15 秒淡黑、切换、0.2 秒显现展示指定匿名席位；关闭动画时即时切换，记录与字幕不丢失。
        /// English: Shows an anonymous seat with a 0.15-second fade to black, cut, and 0.2-second reveal; reduced motion switches instantly without losing transcript or subtitles.
        /// </summary>
        private void ShowCouncilSpeaker(Control stage,int seatNumber,string line)
        {
            _councilTranscript.Add("O5-"+seatNumber+"："+line); if(_councilTranscript.Count>40)_councilTranscript.RemoveAt(0);
            var black=new ColorRect { Color=Colors.Black,MouseFilter=MouseFilterEnum.Stop }; black.SetAnchorsPreset(LayoutPreset.FullRect); black.Modulate=new Color(1,1,1,ReducedMotion?1:0); stage.AddChild(black);
            void Cut()
            {
                Clear(stage); var split=new HBoxContainer(); split.SetAnchorsPreset(LayoutPreset.FullRect); stage.AddChild(split);
                var left=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=.58f }; left.AddChild(new Label { Text="会议发言记录" }); var scroll=new ScrollContainer { SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; var transcript=new VBoxContainer(); foreach(string entry in _councilTranscript) transcript.AddChild(new Label { Text=entry,AutowrapMode=TextServer.AutowrapMode.WordSmart }); scroll.AddChild(transcript); left.AddChild(scroll); split.AddChild(left);
                var right=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsStretchRatio=.42f };
                var portrait=new AnonymousSpeakerView { SeatNumber=seatNumber, ReducedMotion=ReducedMotion, SizeFlagsVertical=SizeFlags.ExpandFill };
                right.AddChild(portrait); var back=new Button { Text="返回全景" }; back.Pressed+=()=>ShowPage("O5会议"); _audio?.BindButton(back); right.AddChild(back); split.AddChild(right);
                var subtitle=new Label { Text="O5-"+seatNumber+"\n"+line,AutowrapMode=TextServer.AutowrapMode.WordSmart,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,CustomMinimumSize=new Vector2(0,72) }; subtitle.SetAnchorsPreset(LayoutPreset.BottomWide); subtitle.OffsetTop=-78; stage.AddChild(subtitle); stage.Modulate=new Color(1,1,1,ReducedMotion?1:0); _audio?.PlayCouncilVoice(seatNumber,Math.Min(1.5,.35+line.Length*.025));
                if(!ReducedMotion) CreateTween().TweenProperty(stage,"modulate:a",1.0,.2);
            }
            if(ReducedMotion){Cut();return;} Tween tween=CreateTween(); tween.TweenProperty(black,"modulate:a",1.0,.15); tween.TweenCallback(Callable.From(Cut));
        }

        /// <summary>中文：优先从真实公开投票生成发言；没有记录时返回明确标记的程序性模板。English: Generates speech from real public votes first, otherwise returning an explicitly marked procedural template.</summary>
        private static string BuildProceduralLine(OverseerViewModel view,int seatNumber)
        {
            for(int record=view.VoteRecords.Length-1;record>=0;record--) foreach(SeatVoteViewModel vote in view.VoteRecords[record].Votes) if(vote.SeatId.Number==seatNumber) return "关于提案 #" + view.VoteRecords[record].ProposalId + "，我的公开表决记录为“" + vote.Choice + "”。";
            int pending=0; foreach(ProposalViewModel proposal in view.Proposals) if(!proposal.IsResolved) pending++; return "[程序性台词] 已确认当前议程包含 "+pending+" 项待决提案。";
        }

        private bool ReducedMotion => _settings.ReduceMotion || !_settings.InterfaceAnimations;

        /// <summary>中文：创建七洲预算编辑和真实权重条，权重只由当前七项预算占比计算。English: Creates seven-region budget editors and real weight bars calculated only from the current seven allocations.</summary>
        private void AddVeilWeightEditor(Node parent,OverseerViewModel view,LineEdit[] edits)
        {
            string[] names={"北美","南美","欧洲","亚洲","非洲","大洋洲","南极洲"}; long total=Sum(view.Budget.VeilOperations);
            for(int i=0;i<7;i++){long value=i<view.Budget.VeilOperations.Length?view.Budget.VeilOperations[i]:0; edits[6+i]=AddMoneyEdit(parent,names[i],value); var bar=new ProgressBar { MinValue=0,MaxValue=100,Value=total>0?value*100.0/total:0,ShowPercentage=true,CustomMinimumSize=new Vector2(0,8) }; parent.AddChild(bar);}
        }

        /// <summary>中文：显示业务成功后的印章或红笔反馈，并通过 Document 总线播放确认音。English: Shows stamp or red-pen feedback after business success and plays confirmation on the Document bus.</summary>
        private void PlayDecisionFeedback(string text,bool red)
        {
            _feedbackOverlay=new DecisionOverlay(); _feedbackOverlay.SetAnchorsPreset(LayoutPreset.Center); _feedbackOverlay.OffsetLeft=-145;_feedbackOverlay.OffsetRight=145;_feedbackOverlay.OffsetTop=-45;_feedbackOverlay.OffsetBottom=45; AddChild(_feedbackOverlay); _feedbackOverlay.Play(text,red,ReducedMotion); _audio?.PlayDocumentCue(red);
        }

        /// <summary>
        /// 中文：生成资源未完成时显示明确的不可用区域；height 单位为像素，paper 决定黑纸文字色，不加载或伪造任何图像。
        /// English: Displays an explicit unavailable region while generated assets are incomplete; height is in pixels and paper selects dark paper text, without loading or fabricating imagery.
        /// </summary>
        private static Label CreateUnavailableImage(string text,float height,bool paper=false)
        {
            var label=new Label { Text=text,CustomMinimumSize=new Vector2(height*4/3,height),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart };
            label.AddThemeColorOverride("font_color",paper?new Color(.18f,.18f,.18f):GodotArt.OverseerMuted); return label;
        }
        private static Label PaperLabel(string text){var label=new Label { Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart };label.AddThemeColorOverride("font_color",new Color(.08f,.08f,.08f));return label;}
        private static int FindLowest(int[] values){if(values==null||values.Length==0)return -1;int index=0;for(int i=1;i<Math.Min(7,values.Length);i++)if(values[i]<values[index])index=i;return index;}
        /// <summary>中文：财政及其他 O5 金额统一使用自动中文单位；完整整数货币值由相邻控件 Tooltip 提供。English: Finance and other O5 money displays share automatic Chinese units, while adjacent control tooltips provide the complete integer currency value.</summary>
        private static string FormatMoney(long value)=>FinanceAmountFormatter.Format(value);

        private LineEdit AddMoneyEdit(Node parent, string label, long value)
        {
            var row = new HBoxContainer(); row.AddChild(new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill }); var edit = new LineEdit { Text = value.ToString(CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(140, 0) }; row.AddChild(edit); parent.AddChild(row); return edit;
        }

        private void ExecutePending()
        {
            TickResult result = _session.Advance(1); _notifications.Append(result.Events); if (_notifications.HasCriticalSinceLastAppend) SetSpeed(0); Refresh();
        }

        private OverseerViewModel Project() => _session.Perspective.Project<OverseerViewModel>(_session.World);

        private void Refresh()
        {
            if (!_isReady) return; OverseerViewModel view = Project();
            _dateLabel.Text = FoundationCalendar.FormatYearMonth(view.CalendarYear, view.CalendarMonth); _cycleLabel.Text = "周期 " + view.CurrentCycle + " · 第 " + view.DayOfCycle + " 天";
            if (_workspaceBody.GetChildCount() == 0) ShowPage("总览");
            // 中文：总览刷新只允许操作当前仍在场景树中的总览节点；其他页面通过自身重建读取最新投影，禁止访问已释放的页面控件。
            // English: Overview refresh may only touch overview nodes that still belong to the scene tree; other pages read fresh projection through their own rebuild and must never access freed page controls.
            if (_currentPage == "总览" && _summaryList != null && _summaryList.IsInsideTree()) RefreshOverview(view);
            if (view.Failure.IsEnded) ShowEnd(view.Failure.EndReason);
        }

        private void RefreshOverview(OverseerViewModel view)
        {
            Clear(_summaryList); AddKeyValue(_summaryList, "可用资金", view.Funds.ToString("N0", CultureInfo.InvariantCulture)); AddKeyValue(_summaryList, "上周期净流量", view.LastCashFlow.ToString("N0", CultureInfo.InvariantCulture)); AddKeyValue(_summaryList, "全球帷幕", FormatRatio(view.GlobalVeil)); AddKeyValue(_summaryList, "设施数量", view.Sites.Length.ToString(CultureInfo.InvariantCulture)); AddKeyValue(_summaryList, "Alpha-1", view.AlphaOne.IsActive ? (view.AlphaOne.IsDeployed ? "已派出" : "待命") : "不可用");
            Clear(_alertList); foreach (OverseerAlertViewModel alert in view.Alerts) _alertList.AddChild(new Label { Text = "· " + alert.Title + (alert.Detail.Length > 0 ? "：" + alert.Detail : "") }); if (view.Alerts.Length == 0) _alertList.AddChild(CreateMutedLabel("尚无警报。"));
            Clear(_notificationList); for (int index = _notifications.Entries.Count - 1; index >= 0; index--) _notificationList.AddChild(new Label { Text = "[T" + _notifications.Entries[index].Tick + "] " + _notifications.Entries[index].Message, AutowrapMode = TextServer.AutowrapMode.WordSmart }); if (_notifications.Entries.Count == 0) _notificationList.AddChild(CreateMutedLabel("暂无通知。"));
            Clear(_siteList); foreach (SiteReportViewModel site in view.Sites) { var button = new Button { Text = (site.Code.Length == 0 ? "Site-" + site.SiteId.Value : site.Code) + " · " + DescribeLocation(site) + " · 稳定度 " + FormatRatio(site.Stability) }; button.Pressed += () => ShowMessage(site.DisplayLabel + "\n" + DescribeLocation(site) + "\n审计状态：" + (site.IsAudited ? "已审计" : "未审计")); button.GuiInput += input => { if (input is InputEventMouseButton mouse && mouse.DoubleClick) ShowMessage("设施详情\n" + site.DisplayLabel + "\n编号：" + site.SiteId.Value + "\n位置：" + DescribeLocation(site) + "\n位置精度：" + DescribePrecision(site.LocationPrecision) + "\n异常：" + site.AnomalyCount + "\n突破：" + site.BreachingAnomalyCount); }; _siteList.AddChild(button); }
            _mapView.SetSites(view.Sites);
        }

        private void ShowEnd(GameEndReason reason)
        {
            SetSpeed(0); var dialog = new AcceptDialog { Title = "终局结算", DialogText = "世界模拟已结束。\n结算原因：" + reason + "\n时间推进已停止。", OkButtonText = "返回主标题" }; dialog.Confirmed += () => GetTree().ChangeSceneToFile("res://Main.tscn"); AddChild(dialog); dialog.PopupCentered();
        }

        /// <summary>
        /// 中文：生成设施位置显示文本。优先使用来源表原文（可能是“未公开”“非地球”等），只有原文为空时才退回精度描述。
        /// 绝不显示 Continent 枚举值：位置不可确认的设施在数据中保留枚举默认值 NorthAmerica，直接显示等于凭空指定一个大洲。
        /// English: Builds the facility location text. The source-table wording is preferred (it may itself read "undisclosed", "non-terrestrial", and so on),
        /// falling back to the precision tier only when that wording is empty. The Continent enum is never shown: facilities with an unconfirmable
        /// location keep the enum default of NorthAmerica in data, so displaying it would assert a continent that no source states.
        /// </summary>
        /// <param name="site">当前设施投影。Current facility projection.</param>
        /// <returns>可直接显示的位置文本。Location text ready for display.</returns>
        private static string DescribeLocation(SiteReportViewModel site)
        {
            return site.LocationText.Length > 0 ? site.LocationText : DescribePrecision(site.LocationPrecision);
        }

        /// <summary>
        /// 中文：把位置精度枚举转成中文说明，使玩家能区分“城市级已知”“仅大区级”“官方保密”“非地球”，不把保密或未知误读为精确位置。
        /// English: Maps the precision enum to Chinese wording so players can tell city-level knowledge, region-only knowledge, official redaction
        /// and non-terrestrial placement apart, and never read redacted or unknown data as an exact position.
        /// </summary>
        /// <param name="precision">来源资料的位置精度。Source-material location precision.</param>
        /// <returns>该精度档的中文说明。Chinese wording for that precision tier.</returns>
        private static string DescribePrecision(SiteLocationPrecision precision) => precision switch
        {
            SiteLocationPrecision.City => "城市级",
            SiteLocationPrecision.Region => "大区级",
            SiteLocationPrecision.Country => "国家级",
            SiteLocationPrecision.Continent => "洲级",
            SiteLocationPrecision.Exact => "精确位置",
            SiteLocationPrecision.Deleted => "官方已隐去",
            SiteLocationPrecision.NonTerrestrial => "非地球设施",
            SiteLocationPrecision.NonReality => "非现实位置",
            _ => "位置未详"
        };

        private void ShowMessage(string message) { var dialog = new AcceptDialog { Title = "O5 终端", DialogText = message }; AddChild(dialog); dialog.PopupCentered(); }
        private void ShowLaunchFailure(string message) { var dialog = new AcceptDialog { Title = "载入失败", DialogText = message, OkButtonText = "返回主标题" }; dialog.Confirmed += () => GetTree().ChangeSceneToFile("res://Main.tscn"); AddChild(dialog); dialog.PopupCentered(); }
        private void SetSpeed(int multiplier) { _speedMultiplier = multiplier; _tickAccumulator = 0; }
        private void AddSpeedButton(Node parent, string text, int multiplier) { var button = new Button { Text = text }; button.Pressed += () => SetSpeed(multiplier); _audio?.BindButton(button); parent.AddChild(button); }
        private static Label AddReadout(Node parent) { var label = new Label { Text = "—" }; parent.AddChild(label); return label; }
        private static void AddKeyValue(Node parent, string key, string value) { var row = new HBoxContainer(); row.AddChild(new Label { Text = key, SizeFlagsHorizontal = SizeFlags.ExpandFill }); row.AddChild(new Label { Text = value }); parent.AddChild(row); }
        /// <summary>中文：创建带标题的通用面板；scrollBody=true 保留旧页面的单层滚动，财政页传 false 后自行放置唯一滚动容器，避免嵌套滚动吞掉最小高度。English: Creates a titled common panel; scrollBody=true preserves the existing single scroll for other pages, while finance passes false and installs exactly one scroll itself, avoiding nested-scroll minimum-size loss.</summary>
        private static PanelContainer CreatePanel(string title, out VBoxContainer body,bool scrollBody=true) { var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; panel.AddThemeStyleboxOverride("panel", CreateBox(GodotArt.OverseerPanel, GodotArt.OverseerRule, 1)); var column = new VBoxContainer(); panel.AddChild(column); column.AddChild(new Label { Text = title }); column.AddChild(new HSeparator()); body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill };if(scrollBody){var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; column.AddChild(scroll);scroll.AddChild(body);}else column.AddChild(body);return panel; }
        private static void Clear(Node parent) { for (int index = parent.GetChildCount() - 1; index >= 0; index--) { Node child = parent.GetChild(index); parent.RemoveChild(child); child.QueueFree(); } }
        // 中文：弱化说明统一按词智能换行；右栏固定 365 像素宽，文本必须增加高度而不能横向裁切，且不改变右栏比例或底部操作区布局。
        // English: Muted descriptions use WordSmart wrapping uniformly; the fixed 365-pixel right column must grow vertically instead of clipping horizontally, without changing its ratio or the bottom action layout.
        private static Label CreateMutedLabel(string text) { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.AddThemeColorOverride("font_color", GodotArt.OverseerMuted); return label; }
        private static long Sum(long[] values) { long total = 0; foreach (long value in values) total += value; return total; }
        private static string FormatRatio(int ratio) => (ratio / 100.0).ToString("F1", CultureInfo.InvariantCulture) + "%";
        private static Theme CreateTheme() { var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC" } }; return new Theme { DefaultFont = font, DefaultFontSize = 15 }; }
        private static StyleBoxFlat CreateBox(Color fill, Color border, int borderWidth) { var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, ContentMarginLeft = 7, ContentMarginRight = 7, ContentMarginTop = 5, ContentMarginBottom = 5 }; box.SetBorderWidthAll(borderWidth); return box; }
        /// <summary>中文：为高密度财政表格/卡片创建可预测的小内边距样式；margin 单位为 Godot 逻辑像素，零与正值均有效，边框宽度仍由调用者决定。English: Creates a predictable small-padding style for dense finance tables/cards; margin uses Godot logical pixels, accepts zero or positive values, and leaves border width to the caller.</summary>
        private static StyleBoxFlat CreateCompactBox(Color fill,Color border,int borderWidth,float margin){var box=new StyleBoxFlat{BgColor=fill,BorderColor=border,ContentMarginLeft=margin,ContentMarginRight=margin,ContentMarginTop=margin,ContentMarginBottom=margin};box.SetBorderWidthAll(borderWidth);return box;}
    }
}
