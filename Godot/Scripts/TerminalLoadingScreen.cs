namespace Scp.Godot
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using global::Godot;
    using Scp.Application;
    using Scp.Domain;

    /// <summary>
    /// 中文：统一 O5 黑白登录终端，在 Godot 主线程执行一次性真实存档工作，并用六个现实秒的确定性世界内验证演出遮罩安全交接。
    /// English: Unified monochrome O5 login terminal that performs one-shot real save work on Godot's main thread and masks safe handoff with a deterministic six-real-second in-world verification presentation.
    /// 参数/单位：阶段边界、经过时间和最低门槛均为现实秒；演出状态不表示文件、存档或业务进度，也不返回业务结果。
    /// Parameters/units: stage boundaries, elapsed time, and the minimum gate are real seconds; presentation state never represents file, save, or business progress and returns no business result.
    /// 边界/确定性/原因：真实工作超过六秒时继续等待，失败进入本地化错误卡；降动效显示同风格静态验证摘要；节点 API 与真实工作均留在主线程，避免不安全跨线程访问。
    /// Edges/determinism/reason: real work beyond six seconds is awaited, failures enter a localized error card, reduced motion shows a matching static verification summary, and node API plus real work stay on the main thread to avoid unsafe cross-thread access.
    /// </summary>
    public sealed partial class TerminalLoadingScreen : Control
    {
        private const double MinimumPresentationSeconds = 6.0;
        private const float CardMaximumWidth = 760.0f;
        private const float CardMaximumHeight = 540.0f;
        private static readonly double[] StageStarts = { 0.0, 1.1, 2.3, 3.4, 4.8, 6.0 };

        private readonly Label[] _verificationRows = new Label[3];
        private Label _level = null!;
        private Label _title = null!;
        private Label _loginHeading = null!;
        private Label _usernameCaption = null!;
        private Label _usernameValue = null!;
        private Label _passwordCaption = null!;
        private Label _passwordValue = null!;
        private Label _screeningResult = null!;
        private Label _welcomeDetail = null!;
        private Label _status = null!;
        private Label _error = null!;
        private Button _return = null!;
        private ColorRect _separator = null!;
        private TextureRect _emblem = null!;
        private AudioManager? _audio;
        private string[] _presentationText = Array.Empty<string>();
        private string[] _verificationText = Array.Empty<string>();
        private string _usernameCaptionText = string.Empty;
        private string _passwordCaptionText = string.Empty;
        private string _welcomeDetailText = string.Empty;
        private string _failureTitle = string.Empty;
        private string _requestFailure = string.Empty;
        private string _workFailure = string.Empty;
        private string _returnFailure = string.Empty;
        private string _returnText = string.Empty;
        private double _elapsed;
        private ulong _presentationStartedMsec;
        private int _stage = -1;
        private int _lastPlayedStage = -1;
        private bool _reducedMotion;
        private bool _failed;
        private GameLaunchRequest? _request;

        /// <summary>
        /// 中文：读取 ApplicationSettings 的动画与语言值、构建加载卡、消费一次性请求并在首帧后开始真实工作；无参数和返回值。
        /// English: Reads motion and language from ApplicationSettings, builds the card, consumes one-shot work, and begins real work after the first frame; it has no parameters or return.
        /// 边界/确定性：语言只接受规范化的 zh_CN、zh_HK、en；单调毫秒时钟使主线程工作耗时计入六秒门槛，首阶段在控件和音频引用就绪后触发。
        /// Edges/determinism: language accepts normalized zh_CN, zh_HK, or en only; a monotonic millisecond clock counts main-thread work toward the six-second gate, and stage zero starts only after controls and audio are ready.
        /// </summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            ProcessMode = ProcessModeEnum.Always;
            Theme = CreateTheme();
            ApplicationSettings settings = new ApplicationSettingsStore(ProjectSettings.GlobalizePath("user://settings/settings.json")).Load();
            _reducedMotion = settings.ReduceMotion || !settings.InterfaceAnimations;
            ConfigureLanguage(settings.Language);
            BuildUi();
            _request = GameLaunchContext.ConsumeWork();
            _audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
            _presentationStartedMsec = Time.GetTicksMsec();
            EnterStage(0);
            ApplyPresentation();
            _ = RunWorkAfterFirstFrameAsync();
        }

        /// <summary>
        /// 中文：由单调时钟按固定边界推进登录、验证、筛查与欢迎状态并更新响应式布局；delta 参数由 Godot 提供、单位为现实秒，但时间状态不累加 delta 且无返回值。
        /// English: Advances login, verification, screening, and welcome states at fixed boundaries from a monotonic clock and updates responsive layout; Godot supplies delta in real seconds, but state does not accumulate delta and returns nothing.
        /// 边界/确定性：索引固定为 0–4；掉帧跨界时依序补触发状态但每个音效最多一次；失败后停止演出，降动效不进行逐字填充、闪烁或扫描移动。
        /// Edges/determinism: index remains 0–4; skipped boundaries enter in order while each cue plays at most once; failure stops presentation, and reduced motion has no incremental filling, flicker, or moving scan.
        /// </summary>
        public override void _Process(double delta)
        {
            if (_failed) return;
            _elapsed = (Time.GetTicksMsec() - _presentationStartedMsec) / 1000.0;
            int target = StageAt(_elapsed);
            while (_stage < target) EnterStage(_stage + 1);
            LayoutCard();
            ApplyPresentation();
            QueueRedraw();
        }

        /// <summary>
        /// 中文：绘制纯黑背景、白色响应式终端框与登录输入线；坐标和尺寸单位为视口像素，无业务返回值。
        /// English: Draws the black background, responsive white terminal frame, and login field lines in viewport pixels with no business return.
        /// 边界/原因：卡片最大 760×540 并保留视口边距；全部线框均为装饰且不接收输入，保持参考图的黑白高对比而不制造虚假凭证表单。
        /// Edge/reason: the card is capped at 760×540 with viewport clearance; all lines are decorative and accept no input, preserving the reference's high-contrast monochrome look without creating a false credential form.
        /// </summary>
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
            Rect2 card = CardRect();
            DrawStyleBox(Box(new Color("050505"), new Color("d8d8d8"), 1), card);
            DrawLine(new Vector2(card.Position.X + 30, card.Position.Y + 58), new Vector2(card.End.X - 30, card.Position.Y + 58), new Color("888888"), 1);
            // 中文：用户名与密码下划线只属于 LOGIN 构图；最终欢迎画面隐藏它们，避免已消失的表单线残留在大徽记旁。降动效静态摘要仍保留登录区，因此继续绘制。
            // English: Username and password rules belong only to the LOGIN composition; the final welcome view hides them so orphaned form lines do not remain beside the enlarged emblem. The reduced-motion static summary retains its login region and therefore retains the rules.
            if (_reducedMotion || _stage < 4)
            {
                DrawLine(new Vector2(card.Position.X + 218, card.Position.Y + 176), new Vector2(card.End.X - 48, card.Position.Y + 176), Colors.White, 1);
                DrawLine(new Vector2(card.Position.X + 218, card.Position.Y + 239), new Vector2(card.End.X - 48, card.Position.Y + 239), Colors.White, 1);
            }
            DrawLine(new Vector2(card.Position.X + 30, card.End.Y - 48), new Vector2(card.End.X - 30, card.End.Y - 48), new Color("888888"), 1);
        }

        /// <summary>
        /// 中文：建立仅使用既有白色基金会 SVG、标签与装饰线的黑白登录终端控件树；无参数、返回或外部素材创建。
        /// English: Builds the monochrome login-terminal control tree using only the existing white Foundation SVG, labels, and decorative lines; it has no parameters, return, or external asset creation.
        /// 边界/原因：用户名与密码是不可交互的匿名演出值，不读取存档身份或安全凭证；同一徽记用于登录与最终欢迎构图，避免新增资源。
        /// Edge/reason: username and password are non-interactive anonymous presentation values that read no save identity or credential; the same emblem serves login and final welcome compositions to avoid new assets.
        /// </summary>
        private void BuildUi()
        {
            _level = MakeLabel("SCP FOUNDATION / O5 / LEVEL 5", 15, HorizontalAlignment.Center, new Color("c8c8c8"));
            _title = MakeLabel(_presentationText[0], 31, HorizontalAlignment.Center, Colors.White);
            _loginHeading = MakeLabel("LOGIN", 25, HorizontalAlignment.Left, Colors.White);
            _emblem = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://Assets/Resources/UI/SCPFoundationEmblemWhite.svg"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_emblem);
            _usernameCaption = MakeLabel(_usernameCaptionText, 13, HorizontalAlignment.Left, new Color("a8a8a8"));
            _usernameValue = MakeLabel(string.Empty, 20, HorizontalAlignment.Left, Colors.White);
            _passwordCaption = MakeLabel(_passwordCaptionText, 13, HorizontalAlignment.Left, new Color("a8a8a8"));
            _passwordValue = MakeLabel(string.Empty, 22, HorizontalAlignment.Left, Colors.White);
            for (int i = 0; i < _verificationRows.Length; i++) _verificationRows[i] = MakeLabel(_verificationText[i], 17, HorizontalAlignment.Center, new Color("d8d8d8"));
            _screeningResult = MakeLabel("CLEAR", 32, HorizontalAlignment.Center, Colors.White);
            _welcomeDetail = MakeLabel(_welcomeDetailText, 17, HorizontalAlignment.Center, new Color("d0d0d0"));
            _separator = new ColorRect { Color = new Color("bcbcbc"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_separator);
            _status = MakeLabel(_presentationText[0], 14, HorizontalAlignment.Center, new Color("b8b8b8"));
            _error = MakeLabel(string.Empty, 16, HorizontalAlignment.Center, new Color("eeeeee"));
            _error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _error.Visible = false;
            _return = new Button { Text = _returnText, Visible = false, Size = new Vector2(180, 42) };
            _return.Pressed += ReturnFromFailure;
            AddChild(_return);
            LayoutCard();
        }

        /// <summary>
        /// 中文：严格按设置语言配置本加载终端的世界内登录、验证、欢迎、错误与返回文字；language 为 zh_CN、zh_HK 或 en，无返回值。
        /// English: Configures this loading terminal's in-world login, verification, welcome, error, and return text strictly from language zh_CN, zh_HK, or en; it returns nothing.
        /// 边界/确定性：未知值回退简体中文；固定终端标识保留英文，英文分支不混入中文；所有玩家可见文案仅描述世界内终端状态，不暴露内部演出结构。
        /// Edges/determinism: unknown values fall back to Simplified Chinese; fixed terminal marks remain English, the English branch contains no Chinese, and all player-visible copy describes only in-world terminal state rather than internal presentation structure.
        /// </summary>
        private void ConfigureLanguage(string language)
        {
            if (language == "en")
            {
                _presentationText = new[] { "SECURE TERMINAL", "IDENTITY VERIFICATION", "BIOMETRIC VERIFICATION", "COGNITIVE CONTAMINATION SCREENING", "WELCOME, OVERSEER" };
                _verificationText = new[] { "IDENTITY CONFIRMED — ANONYMOUS O5", "RETINAL PATTERN VERIFIED", "NEURAL SIGNATURE VERIFIED" };
                _usernameCaptionText = "USERNAME"; _passwordCaptionText = "PASSWORD"; _welcomeDetailText = "O5 COMMAND TERMINAL ACCESS GRANTED";
                _failureTitle = "AUTHORIZATION HANDSHAKE ABORTED"; _requestFailure = "No valid terminal access request was received. Return and try again."; _workFailure = "Terminal access failed. Check save status, storage permissions, or game data, then return and try again."; _returnFailure = "Unable to return. Please try again later."; _returnText = "RETURN";
            }
            else if (language == "zh_HK")
            {
                _presentationText = new[] { "安全終端", "身份驗證", "生物特徵驗證", "認知污染篩查", "Welcome／歡迎監督者" };
                _verificationText = new[] { "身份確認 — 匿名 O5", "視網膜特徵驗證通過", "神經簽名驗證通過" };
                _usernameCaptionText = "用戶名稱"; _passwordCaptionText = "密碼"; _welcomeDetailText = "O5 指揮終端接入許可已授予";
                _failureTitle = "權限握手中止"; _requestFailure = "未收到有效的終端接入請求。請返回後重試。"; _workFailure = "終端接入失敗。請檢查存檔狀態、儲存權限或遊戲資料後返回重試。"; _returnFailure = "無法返回，請稍後重試。"; _returnText = "返回";
            }
            else
            {
                _presentationText = new[] { "安全终端", "身份验证", "生物特征验证", "认知污染筛查", "Welcome／欢迎监督者" };
                _verificationText = new[] { "身份确认 — 匿名 O5", "视网膜特征验证通过", "神经签名验证通过" };
                _usernameCaptionText = "用户名"; _passwordCaptionText = "密码"; _welcomeDetailText = "O5 指挥终端接入许可已授予";
                _failureTitle = "权限握手中止"; _requestFailure = "未收到有效的终端接入请求。请返回后重试。"; _workFailure = "终端接入失败。请检查存档状态、存储权限或游戏数据后返回重试。"; _returnFailure = "无法返回，请稍后重试。"; _returnText = "返回";
            }
        }

        /// <summary>
        /// 中文：将子控件布置到当前响应式卡片；坐标与尺寸单位均为视口像素，无参数和返回值。
        /// English: Lays child controls into the responsive card in viewport pixels; it has no parameters or return.
        /// 边界/原因：卡片始终保留至少 18 像素边距；1024×576 下使用 540 像素高度，登录字段、验证摘要和错误按钮均不会越过卡片安全区。
        /// Edge/reason: the card always keeps at least 18 pixels clearance; at 1024×576 its 540-pixel height keeps login fields, verification summary, and error button inside the safe area.
        /// </summary>
        private void LayoutCard()
        {
            Rect2 card = CardRect(); float x = card.Position.X; float y = card.Position.Y; float w = card.Size.X;
            _level.Position = new Vector2(x + 32, y + 17); _level.Size = new Vector2(w - 64, 27);
            _title.Position = new Vector2(x + 30, y + 63); _title.Size = new Vector2(w - 60, 34);
            _loginHeading.Position = new Vector2(x + 218, y + 105); _loginHeading.Size = new Vector2(w - 266, 34);
            _emblem.Position = new Vector2(x + 54, y + 114); _emblem.Size = new Vector2(126, 126);
            _usernameCaption.Position = new Vector2(x + 218, y + 141); _usernameCaption.Size = new Vector2(w - 266, 23);
            _usernameValue.Position = new Vector2(x + 218, y + 174); _usernameValue.Size = new Vector2(w - 266, 32);
            _passwordCaption.Position = new Vector2(x + 218, y + 204); _passwordCaption.Size = new Vector2(w - 266, 23);
            _passwordValue.Position = new Vector2(x + 218, y + 235); _passwordValue.Size = new Vector2(w - 266, 32);
            _separator.Position = new Vector2(x + 48, y + 282); _separator.Size = new Vector2(w - 96, 1);
            for (int i = 0; i < _verificationRows.Length; i++) { _verificationRows[i].Position = new Vector2(x + 54, y + 302 + i * 34); _verificationRows[i].Size = new Vector2(w - 108, 28); }
            _screeningResult.Position = new Vector2(x + 54, y + 397); _screeningResult.Size = new Vector2(w - 108, 44);
            _welcomeDetail.Position = new Vector2(x + 54, y + 430); _welcomeDetail.Size = new Vector2(w - 108, 28);
            _status.Position = new Vector2(x + 42, y + 459); _status.Size = new Vector2(w - 84, 27);
            _error.Position = new Vector2(x + 55, y + 405); _error.Size = new Vector2(w - 110, 52);
            _return.Position = new Vector2(card.GetCenter().X - 90, card.End.Y - 67);
        }

        /// <summary>
        /// 中文：按当前固定状态显示登录自动填充、身份与生物验证、认知污染筛查及最终欢迎构图；无参数和返回值。
        /// English: Displays login auto-fill, identity and biometric verification, cognitive-contamination screening, and the final welcome composition for the current fixed state; it has no parameters or return.
        /// 边界/确定性：状态局部值钳制 0–1 并 SmoothStep；正常模式只进行淡入和确定性逐字符填充，降动效直接显示同风格静态验证摘要且不包含开发清单措辞。
        /// Edges/determinism: state-local values clamp to 0–1 with SmoothStep; normal motion uses only fades and deterministic character filling, while reduced motion directly shows a matching static verification summary without development-checklist wording.
        /// </summary>
        private void ApplyPresentation()
        {
            float local = StageLocal(_stage, _elapsed);
            if (_reducedMotion)
            {
                SetLoginVisible(true);
                _title.Text = _presentationText[4]; _title.Modulate = Colors.White;
                _usernameValue.Text = "O5-██"; _passwordValue.Text = "••••••••••••";
                for (int i = 0; i < _verificationRows.Length; i++) { _verificationRows[i].Visible = true; _verificationRows[i].Text = "✓  " + _verificationText[i]; }
                _screeningResult.Visible = true; _screeningResult.Text = "CLEAR";
                _welcomeDetail.Visible = true; _status.Text = _presentationText[4];
                return;
            }

            _title.Text = _presentationText[_stage]; _title.Modulate = new Color(1, 1, 1, .65f + .35f * local);
            SetLoginVisible(_stage < 4);
            _usernameValue.Text = Prefix("O5-██", _stage == 0 ? local : 1);
            _passwordValue.Text = Prefix("••••••••••••", _stage == 0 ? Mathf.Clamp(local * 1.35f - .2f, 0, 1) : 1);
            for (int i = 0; i < _verificationRows.Length; i++)
            {
                bool visible = _stage > i || (_stage == i + 1 && local > .3f);
                _verificationRows[i].Visible = visible && _stage < 4;
                _verificationRows[i].Text = "✓  " + _verificationText[i];
                _verificationRows[i].Modulate = new Color(1, 1, 1, visible ? Mathf.Clamp(local + .35f, 0, 1) : 0);
            }
            _screeningResult.Visible = _stage == 3 && local > .45f;
            _screeningResult.Modulate = new Color(1, 1, 1, Mathf.Clamp((local - .45f) * 2, 0, 1));
            _welcomeDetail.Visible = _stage == 4; _welcomeDetail.Modulate = new Color(1, 1, 1, local);
            if (_stage == 4) { _emblem.Visible = true; _emblem.Position = new Vector2(CardRect().GetCenter().X - 82, CardRect().Position.Y + 130); _emblem.Size = new Vector2(164, 164); _emblem.Modulate = new Color(1, 1, 1, local); }
            _status.Text = _presentationText[_stage];
        }

        /// <summary>
        /// 中文：等待首个 process_frame，在主线程同步完成真实存档工作，再等待六现实秒最低门槛并进入真实目标；无参数和返回值。
        /// English: Awaits the first process_frame, completes real save work synchronously on the main thread, then awaits the six-real-second floor and enters the real destination; it has no parameters or return.
        /// 边界/确定性：请求缺失或任何真实异常立即显示既有失败卡；工作超过六秒不再额外延迟但完成前绝不交接；认知污染演出不读写存档或人物属性。
        /// Edges/determinism: missing requests or real exceptions immediately show the existing failure card; work beyond six seconds adds no delay but never hands off early; cognitive-screening presentation never reads or writes save or character attributes.
        /// </summary>
        private async Task RunWorkAfterFirstFrameAsync()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_request == null) { ShowFailure(_requestFailure); return; }
            try
            {
                SaveRepository repository = GameLaunchContext.CreateRepository();
                SaveFile save;
                switch (_request.Kind)
                {
                    case GameLaunchKind.NewGame:
                        // 中文：完整演出不得改变新游戏业务语义；候选档仍需按固定顺序读取 SCP 与设施目录、创建确定性世界、生成任命摘要并原子保存。StableSeed 使用 UTF-8 FNV-1a，保证相同文本跨进程产生同一初始世界。
                        // English: The full presentation must not change new-game semantics; the candidate still loads SCP and facility catalogues in fixed order, creates a deterministic world, generates the appointment briefing, and saves atomically. StableSeed uses UTF-8 FNV-1a so equal text produces the same initial world across processes.
                        save = _request.Candidate ?? throw new InvalidOperationException("Missing new-game candidate save.");
                        ScpDefinition[] definitions = new ScpContentLoader().LoadDirectory(ProjectSettings.GlobalizePath("res://Assets/Data/Scps"));
                        FacilityDefinition[] facilities = new FacilityDataLoader().LoadFile(ProjectSettings.GlobalizePath("res://Assets/Data/Facilities/o5-facilities.json"));
                        save.World = OverseerScenarioFactory.CreateWorld(definitions, facilities, StableSeed(save.Seed));
                        save.WorldFacts = save.World.Facts;
                        save.BriefingAcknowledged = false;
                        save.Briefing = OverseerScenarioFactory.CreateBriefing(save.Seed, save.World);
                        repository.Save(save);
                        break;
                    case GameLaunchKind.PersistLoadedGame:
                        save = _request.Candidate ?? throw new InvalidOperationException("Missing loaded save handoff.");
                        repository.Save(save);
                        break;
                    case GameLaunchKind.BackupContinue:
                        save = repository.Load(_request.SaveId, true);
                        if (_request.UpdateLatest && repository.SetLatest(_request.SaveId, true).Status != SaveDirectoryOperationStatus.Succeeded) throw new InvalidOperationException("Unable to update latest save index.");
                        break;
                    default:
                        save = repository.Load(_request.SaveId, false);
                        if (_request.UpdateLatest && repository.SetLatest(_request.SaveId).Status != SaveDirectoryOperationStatus.Succeeded) throw new InvalidOperationException("Unable to update latest save index.");
                        break;
                }
                double remaining = MinimumPresentationSeconds - (Time.GetTicksMsec() - _presentationStartedMsec) / 1000.0;
                if (remaining > 0) await ToSignal(GetTree().CreateTimer(remaining, true, false, true), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GameLaunchContext.DeliverLoaded(save);
                string target = save.BriefingAcknowledged ? "res://Overseer.tscn" : "res://OverseerBriefing.tscn";
                Error transition = GetTree().ChangeSceneToFile(target);
                if (transition != Error.Ok) throw new InvalidOperationException("Target scene transition rejected: " + transition);
            }
            catch (Exception exception)
            {
                GD.PrintErr("Terminal real-work failure: " + exception);
                ShowFailure(_workFailure);
            }
        }

        /// <summary>
        /// 中文：进入指定 0–4 内部演出状态并按模式触发至多一次 Document 总线合成音；无返回值，状态时间仍是纯演出数据且不会显示给玩家。
        /// English: Enters internal presentation state 0–4 and triggers at most one Document-bus synthesized cue according to motion mode; it returns nothing, and state timing remains presentation-only data never shown to players.
        /// 边界/原因：降动效只在初始状态播放一次低干扰音；正常模式即使掉帧补状态也由 lastPlayedStage 防止重复播放。
        /// Edge/reason: reduced motion plays one low-interference cue only in the initial state; normal mode prevents duplicates with lastPlayedStage even when catching up skipped states.
        /// </summary>
        private void EnterStage(int stage)
        {
            _stage = Mathf.Clamp(stage, 0, 4);
            if (_audio == null || _stage <= _lastPlayedStage) return;
            if (_reducedMotion) { if (_lastPlayedStage < 0) _audio.PlayTerminalReducedMotionCue(); }
            else _audio.PlayTerminalStageCue(_stage);
            _lastPlayedStage = _stage;
        }

        /// <summary>
        /// 中文：切换到本地化真实错误卡，停止所有演出控件并保留返回操作；message 为玩家可见原因，无返回值。
        /// English: Switches to the localized real-error card, stops presentation controls, and preserves return action; message is player-visible and no value is returned.
        /// 边界/原因：错误不伪装成生物或认知失败；登录、验证、筛查与成功徽记全部隐藏，避免成功表现残留。
        /// Edge/reason: errors never masquerade as biometric or cognitive failure; login, verification, screening, and success-emblem controls are hidden to avoid residual success imagery.
        /// </summary>
        private void ShowFailure(string message)
        {
            _failed = true; _title.Text = _failureTitle; _status.Visible = false; _separator.Visible = false; _emblem.Visible = false; SetLoginVisible(false);
            foreach (Label item in _verificationRows) item.Visible = false; _screeningResult.Visible = false; _welcomeDetail.Visible = false;
            _error.Text = message; _error.Visible = true; _return.Visible = true; _return.GrabFocus(); QueueRedraw();
        }

        /// <summary>
        /// 中文：从错误卡返回请求指定的安全场景；无参数和返回值，切换失败时显示本地化返回错误。
        /// English: Returns from the error card to the request's safe scene; it has no parameters or return and shows a localized return error if transition fails.
        /// 边界：请求缺失时使用主标题；不恢复或重放已消费的真实工作。
        /// Edge: a missing request falls back to the main title and consumed real work is neither restored nor replayed.
        /// </summary>
        private void ReturnFromFailure()
        {
            Error result = GetTree().ChangeSceneToFile(_request?.ReturnScene ?? "res://Main.tscn");
            if (result != Error.Ok) _error.Text = _returnFailure;
        }

        /// <summary>
        /// 中文：把种子文本稳定映射为 64 位 UTF-8 FNV-1a 数值；参数 text 是玩家确认的种子文本，返回值供新世界工厂使用，无时间或显示单位。
        /// English: Maps seed text deterministically to a 64-bit UTF-8 FNV-1a value; text is the player-confirmed seed and the return value feeds the new-world factory with no time or display unit.
        /// 边界/确定性：空字符串仍产生固定 offset basis；禁止使用进程随机化的 string.GetHashCode，保证存档重建和跨进程一致性。
        /// Edge/determinism: an empty string still yields the fixed offset basis; randomized string.GetHashCode is forbidden so save construction remains stable across processes.
        /// </summary>
        private static ulong StableSeed(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte value in Encoding.UTF8.GetBytes(text)) { hash ^= value; hash *= prime; }
            return hash;
        }

        private int StageAt(double seconds) { for (int i = 4; i > 0; i--) if (seconds >= StageStarts[i]) return i; return 0; }
        private static float StageLocal(int stage, double elapsed) => Smooth((float)((elapsed - StageStarts[stage]) / (StageStarts[stage + 1] - StageStarts[stage])));
        private static float Smooth(float value) { value = Mathf.Clamp(value, 0, 1); return value * value * (3 - 2 * value); }
        private Rect2 CardRect() { float w = Math.Min(CardMaximumWidth, Math.Max(360, Size.X - 36)); float h = Math.Min(CardMaximumHeight, Math.Max(500, Size.Y - 36)); return new Rect2((Size.X - w) / 2, (Size.Y - h) / 2, w, h); }
        /// <summary>
        /// 中文：统一切换登录标题、匿名字段和分隔线可见性；visible 为是否显示登录区域，无返回值。
        /// English: Toggles visibility of the login heading, anonymous fields, and separator as one unit; visible selects whether the login region is shown and no value is returned.
        /// 边界/原因：仅控制表现节点，不创建输入焦点、不读取凭证；基金会徽记由调用方独立控制，以便最终欢迎页放大复用。
        /// Edge/reason: this controls presentation nodes only and creates no input focus or credential read; the caller controls the Foundation emblem separately so the final welcome page can reuse it at a larger size.
        /// </summary>
        private void SetLoginVisible(bool visible) { _loginHeading.Visible = visible; _usernameCaption.Visible = visible; _usernameValue.Visible = visible; _passwordCaption.Visible = visible; _passwordValue.Visible = visible; _separator.Visible = visible; if (visible) { _emblem.Visible = true; _emblem.Modulate = Colors.White; } }

        /// <summary>
        /// 中文：按 0–1 比例返回自动填充字符串的确定性前缀；text 是终端演出值，ratio 是无单位比例，返回值仅用于 Label 显示。
        /// English: Returns a deterministic prefix of an auto-filled string using a 0–1 ratio; text is a terminal presentation value, ratio is unitless, and the result is only for Label display.
        /// 边界/确定性：比例钳制后向下取整，空字符串返回空值；方法不处理真实用户名、密码或存档身份。
        /// Edge/determinism: the clamped ratio is floored, an empty string returns empty, and the method handles no real username, password, or save identity.
        /// </summary>
        private static string Prefix(string text, float ratio) => text[..Mathf.Clamp(Mathf.FloorToInt(text.Length * ratio), 0, text.Length)];
        private Label MakeLabel(string text, int size, HorizontalAlignment align, Color color) { var label = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, Modulate = color }; label.AddThemeFontSizeOverride("font_size", size); AddChild(label); return label; }
        private static StyleBoxFlat Box(Color background, Color border, int width) => new() { BgColor = background, BorderColor = border, BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
        private static Theme CreateTheme() { var theme = new Theme(); theme.SetColor("font_color", "Label", new Color("dedede")); theme.SetColor("font_color", "Button", Colors.White); theme.SetStylebox("normal", "Button", Box(new Color("080808"), new Color("bcbcbc"), 1)); theme.SetStylebox("hover", "Button", Box(new Color("242424"), Colors.White, 1)); theme.SetStylebox("focus", "Button", Box(new Color("080808"), Colors.White, 2)); return theme; }
    }
}
