namespace Scp.Godot
{
    using System;
    using global::Godot;
    using Scp.Application;

    /// <summary>
    /// 中文：全局音频管理器，负责创建并维护 Master、Music、Ambience、UI 总线，以及跨场景播放主标题音乐和界面音效。
    /// English: Global audio manager that creates and maintains the Master, Music, Ambience and UI buses and plays title music and UI sounds across scenes.
    /// 参数与单位：设置音量为 0–100 的线性百分比，转换后写入 Godot 分贝总线；静音为独立布尔状态。
    /// Parameters and units: settings volumes are linear percentages from 0–100 and are converted to Godot bus decibels; mute is an independent boolean state.
    /// 返回值：播放与设置方法通过 Godot 音频服务器产生状态，不返回业务数据。
    /// Return value: playback and settings methods update Godot audio state and return no business data.
    /// 边界情况：总线或资源缺失时运行时补齐总线并安全跳过缺失流，不把主标题音乐发送到环境总线。
    /// Edge cases: missing buses are created at runtime and missing streams are safely skipped; title music is never routed to the ambience bus.
    /// 确定性：资源路径、总线路由和音量映射固定；仅玩家界面事件触发音效。
    /// Determinism: resource paths, bus routing and volume mapping are fixed; only player UI events trigger effects.
    /// 设计原因：Autoload 在场景切换期间持续存在，保证设置预览和按下音效不会被场景卸载截断。
    /// Design reason: the Autoload survives scene changes so settings previews and pressed sounds are not cut off by scene unloading.
    /// </summary>
    public sealed partial class AudioManager : Node
    {
        private const string MusicBus = "Music";
        private const string AmbienceBus = "Ambience";
        private const string UiBus = "UI";
        private const string DialogueBus = "Dialogue";
        private const string DocumentBus = "Document";
        private const string MainTitleMusicPath = "res://Assets/Resources/Audio/Runtime/main_title_music.ogg";
        private const string UiClickPath = "res://Assets/Resources/Audio/Runtime/ui_confirm.ogg";
        private const string UiHoverPath = "res://Assets/Resources/Audio/Runtime/ui_focus.ogg";
        private const string UiWarningPath = "res://Assets/Resources/Audio/Runtime/ui_warning.ogg";
        private const string TimelineInterferencePath = "res://Assets/Resources/Audio/Runtime/timeline_interference.wav";

        private AudioStreamPlayer _musicPlayer = null!;
        private AudioStreamPlayer _clickPlayer = null!;
        private AudioStreamPlayer _hoverPlayer = null!;
        private AudioStreamPlayer _warningPlayer = null!;
        private AudioStreamPlayer _dialoguePlayer = null!;
        private AudioStreamPlayer _documentPlayer = null!;
        private AudioStreamPlayer _timelinePlayer = null!;

        /// <summary>
        /// 中文：在首个场景前确保四总线可用、创建播放器，并应用磁盘中的已保存设置。
        /// English: Ensures all four buses exist before the first scene, creates players, and applies saved settings from disk.
        /// </summary>
        public override void _Ready()
        {
            EnsureBus(MusicBus);
            EnsureBus(AmbienceBus);
            EnsureBus(UiBus);
            EnsureBus(DialogueBus);
            EnsureBus(DocumentBus);

            _musicPlayer = CreatePlayer("MainTitleMusic", MusicBus, MainTitleMusicPath);
            if (_musicPlayer.Stream is AudioStreamOggVorbis musicStream)
            {
                // 中文：标题曲资源只在 Music 播放器中启用无缝循环，不改变原始文件，也不影响任何未来环境音播放器。
                // English: Enable seamless looping only for the title stream on the Music player without changing the source file or future ambience players.
                musicStream.Loop = true;
            }

            _clickPlayer = CreatePlayer("UiClick", UiBus, UiClickPath);
            _hoverPlayer = CreatePlayer("UiHover", UiBus, UiHoverPath);
            _warningPlayer = CreatePlayer("UiWarning", UiBus, UiWarningPath);
            _dialoguePlayer = CreateEmptyPlayer("CouncilDialogue", DialogueBus);
            _documentPlayer = CreateEmptyPlayer("DocumentFeedback", DocumentBus);
            _timelinePlayer = CreatePlayer("TimelineInterference", UiBus, TimelineInterferencePath);

            var store = new ApplicationSettingsStore(ProjectSettings.GlobalizePath("user://settings/settings.json"));
            ApplySettings(store.Load());
        }

        /// <summary>
        /// 中文：开始或继续主标题循环音乐；重复进入主标题不会叠加同一曲目。
        /// English: Starts or continues looping title music; repeated title entry never layers duplicate playback.
        /// </summary>
        public void PlayMainTitleMusic()
        {
            if (_musicPlayer.Stream != null && !_musicPlayer.Playing)
            {
                _musicPlayer.Play();
            }
        }

        /// <summary>
        /// 中文：停止仅属于主标题的音乐，防止场景切换后进入设置或 O5 总览时继续播放。
        /// English: Stops title-only music so it does not continue after switching to settings or the O5 overview.
        /// </summary>
        public void StopMainTitleMusic() => _musicPlayer.Stop();

        /// <summary>
        /// 中文：在 UI 总线播放确认/按下音效；音量和静音完全由 UI 与 Master 总线控制。
        /// English: Plays the confirmation/pressed sound on the UI bus; volume and mute are controlled entirely by UI and Master buses.
        /// </summary>
        public void PlayUiClick()
        {
            if (_clickPlayer.Stream != null)
            {
                _clickPlayer.Play();
            }
        }

        /// <summary>
        /// 中文：在 UI 总线播放悬停/焦点音效；音量和静音完全由 UI 与 Master 总线控制。
        /// English: Plays the hover/focus sound on the UI bus; volume and mute are controlled entirely by UI and Master buses.
        /// </summary>
        public void PlayUiHover()
        {
            if (_hoverPlayer.Stream != null)
            {
                _hoverPlayer.Play();
            }
        }

        /// <summary>
        /// 中文：播放警示提示音，供设置冲突、显示回退和危急提示使用；仍通过 UI 总线受音量与静音控制。
        /// English: Plays the warning cue for binding conflicts, display rollback and critical notices; it remains controlled by UI volume and mute.
        /// </summary>
        public void PlayUiWarning()
        {
            if (_warningPlayer.Stream != null)
            {
                _warningPlayer.Play();
            }
        }

        /// <summary>
        /// 中文：播放按席位编号确定性变化的不可辨识电子乱码语音；seatNumber 为 1–13，duration 单位为现实秒，字幕承担完整语义。
        /// English: Plays unintelligible electronic speech varied deterministically by seat number; seatNumber is 1–13, duration is in real seconds, and subtitles carry all meaning.
        /// 边界/返回：编号会钳制到有效范围，时长钳制到 0.15–1.8 秒；生成 22.05 kHz 单声道 PCM，不调用 TTS 且不返回业务状态。
        /// Edges/return: seat number and duration are clamped; 22.05 kHz mono PCM is generated without TTS and no business state is returned.
        /// </summary>
        public void PlayCouncilVoice(int seatNumber, double duration = 0.7)
        {
            int seat = Mathf.Clamp(seatNumber, 1, 13);
            _dialoguePlayer.Stream = BuildSynthStream(seat, Mathf.Clamp((float)duration, 0.15f, 1.8f), false);
            _dialoguePlayer.Play();
        }

        /// <summary>
        /// 中文：播放文档授权低频确认或终端扫描警报；alert=true 使用更高频率但仍不宣告业务成功。
        /// English: Plays a low document authorization cue or terminal scan alert; alert=true uses higher frequencies without asserting business success.
        /// </summary>
        public void PlayDocumentCue(bool alert = false)
        {
            _documentPlayer.Stream = BuildSynthStream(alert ? 29 : 17, alert ? 0.32f : 0.25f, alert);
            _documentPlayer.Play();
        }

        /// <summary>
        /// 中文：播放 O5 加载演出的确定性分阶段合成短音；stage 表示固定的 0–4 演出阶段，不是加载百分比，每次调用无业务返回值。
        /// English: Plays a deterministic synthesized cue for an O5 loading-presentation stage; stage identifies fixed presentation stage 0–4, never loading percentage, and the call returns no business value.
        /// 参数/单位/边界：stage 钳制到 0–4；每段持续 0.18–0.34 现实秒并使用固定种子和音色，调用方负责每阶段至多触发一次。
        /// Parameters/units/edges: stage is clamped to 0–4; each cue lasts 0.18–0.34 real seconds with fixed seeds and timbres, while the caller guarantees at most one trigger per stage.
        /// 确定性/设计原因：全部 PCM 均由固定映射生成并路由至 Document 总线，因此服从 Master/UI 静音与音量，同时不使用外部素材，也不声称真实工作成功。
        /// Determinism/design reason: all PCM uses a fixed mapping and routes through Document, respecting Master/UI mute and volume without external assets or implying that real work succeeded.
        /// </summary>
        public void PlayTerminalStageCue(int stage)
        {
            int index = Mathf.Clamp(stage, 0, 4);
            int[] seeds = { 41, 47, 53, 61, 71 };
            float[] durations = { 0.26f, 0.22f, 0.18f, 0.34f, 0.28f };
            _documentPlayer.Stream = BuildSynthStream(seeds[index], durations[index], index is 1 or 3);
            _documentPlayer.Play();
        }

        /// <summary>
        /// 中文：播放降动效加载页唯一一次低干扰确认音；无参数、无业务返回值，持续 0.20 现实秒且只表示静态检查开始。
        /// English: Plays the reduced-motion loader's single low-interference confirmation cue; it has no parameters or business return, lasts 0.20 real seconds, and signals only that static checks began.
        /// 边界/确定性/原因：固定种子与 Document 总线确保每次一致并服从静音；由加载页保证只调用一次，避免五项静态清单产生连续提示。
        /// Edge/determinism/reason: a fixed seed and Document routing keep playback repeatable and muted when requested; the loader calls it once to avoid repeated cues for the five static checks.
        /// </summary>
        public void PlayTerminalReducedMotionCue()
        {
            _documentPlayer.Stream = BuildSynthStream(37, 0.20f, false);
            _documentPlayer.Play();
        }

        public void PlayTimelineInterference()
        {
            // 中文：播放时间线切换专用的程序合成短音；停止上一段可重入播放，防止快速滚轮输入造成叠播和爆音。
            // English: Plays the dedicated synthesized timeline-switch cue; stopping the previous cue prevents overlap and clipping during rapid wheel input.
            if (_timelinePlayer.Stream == null) return;
            _timelinePlayer.Stop();
            _timelinePlayer.Play();
        }

        /// <summary>
        /// 中文：为按钮绑定一次统一反馈；鼠标悬停与键盘焦点重叠时只播放一次选择音，按下时播放确认音。
        /// English: Binds consistent feedback once; overlapping mouse hover and keyboard focus play one selection sound, while pressing plays confirmation.
        /// 参数：button 是当前场景拥有的 Godot 按钮；按钮销毁后信号连接随节点释放。
        /// Parameter: button is a Godot button owned by the current scene; its signal connections are released with the node.
        /// </summary>
        public void BindButton(Button button)
        {
            button.MouseEntered += () =>
            {
                if (!button.Disabled && !button.HasFocus())
                {
                    PlayUiHover();
                }
            };
            button.FocusEntered += () =>
            {
                if (!button.Disabled && !button.IsHovered())
                {
                    PlayUiHover();
                }
            };
            button.Pressed += PlayUiClick;
        }

        /// <summary>
        /// 中文：把应用设置中的四路百分比和静音状态真实写入 Godot 总线；0% 映射为静音分贝下限。
        /// English: Writes all four percentage volumes and mute states from application settings to Godot buses; 0% maps to the decibel silence floor.
        /// 参数与单位：settings 的音量范围为 0–100；超出范围时钳制，保证稳定且不放大到设计值之外。
        /// Parameters and units: settings volume range is 0–100; out-of-range values are clamped for stable playback without unintended gain.
        /// </summary>
        public void ApplySettings(ApplicationSettings settings)
        {
            ApplyBus("Master", settings.MasterVolume, settings.MasterMuted);
            ApplyBus(MusicBus, settings.MusicVolume, settings.MusicMuted);
            ApplyBus(AmbienceBus, settings.AmbienceVolume, settings.AmbienceMuted);
            ApplyBus(UiBus, settings.UiVolume, settings.UiMuted);
            // 中文：Dialogue 使用独立设置，字幕不受该总线静音影响；Document 仍继承 UI，避免为单一反馈增加无必要的设置项。
            // English: Dialogue uses dedicated settings while subtitles remain unaffected; Document continues to inherit UI to avoid a needless control for one feedback family.
            ApplyBus(DialogueBus, settings.DialogueVolume, settings.DialogueMuted);
            ApplyBus(DocumentBus, settings.UiVolume, settings.UiMuted);
        }

        /// <summary>
        /// 中文：确保命名总线存在并发送到 Master；Godot 内置 Master 总线始终保留在索引零。
        /// English: Ensures a named bus exists and sends it to Master; Godot's built-in Master bus remains at index zero.
        /// </summary>
        private static void EnsureBus(string busName)
        {
            if (AudioServer.GetBusIndex(busName) < 0)
            {
                AudioServer.AddBus();
                int index = AudioServer.BusCount - 1;
                AudioServer.SetBusName(index, busName);
                AudioServer.SetBusSend(index, "Master");
            }
        }

        /// <summary>
        /// 中文：创建一个非空间化播放器并固定到指定总线；资源加载失败时保留空流以允许界面继续运行。
        /// English: Creates a non-positional player routed to a fixed bus; a failed resource load leaves an empty stream so the UI remains usable.
        /// </summary>
        private AudioStreamPlayer CreatePlayer(string name, string bus, string resourcePath)
        {
            var player = new AudioStreamPlayer
            {
                Name = name,
                Bus = bus,
                Stream = ResourceLoader.Load<AudioStream>(resourcePath)
            };
            AddChild(player);
            return player;
        }

        /// <summary>中文：创建无预置资源的合成音播放器。English: Creates a player reserved for runtime synthesized audio.</summary>
        private AudioStreamPlayer CreateEmptyPlayer(string name, string bus)
        {
            var player = new AudioStreamPlayer { Name = name, Bus = bus };
            AddChild(player);
            return player;
        }

        /// <summary>
        /// 中文：生成确定性 16 位 PCM：两路正弦、方波门控和线性同余噪声构成不可辨识短音，攻击/释放包络防止爆音。
        /// English: Generates deterministic 16-bit PCM from two sines, square gating, and LCG noise, with attack/release envelopes preventing clicks.
        /// 参数/返回：seed 控制席位纹理，duration 为秒，alert 切换扫描节奏；返回可直接播放的单声道 AudioStreamWav。
        /// Parameters/return: seed controls voice texture, duration is seconds, and alert selects scan rhythm; returns a playable mono AudioStreamWav.
        /// </summary>
        private static AudioStreamWav BuildSynthStream(int seed, float duration, bool alert)
        {
            const int sampleRate = 22050;
            int sampleCount = Math.Max(1, (int)(sampleRate * duration));
            byte[] data = new byte[sampleCount * 2];
            uint noise = (uint)(seed * 2654435761u);
            float baseFrequency = (alert ? 310f : 105f) + seed * (alert ? 7f : 11f);
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float edge = Math.Min(1f, Math.Min(index / (sampleRate * 0.025f), (sampleCount - index) / (sampleRate * 0.04f)));
                float gate = Math.Sin(Math.Tau * (alert ? 11f : 17f + seed % 5) * time) >= 0 ? 1f : 0.28f;
                noise = noise * 1664525u + 1013904223u;
                float random = ((noise >> 16) / 32767.5f - 1f) * 0.16f;
                float signal = MathF.Sin(MathF.Tau * baseFrequency * time) * 0.45f + MathF.Sin(MathF.Tau * (baseFrequency * 1.71f) * time) * 0.22f + random;
                short sample = (short)Mathf.Clamp(signal * gate * edge * 17000f, short.MinValue, short.MaxValue);
                data[index * 2] = (byte)(sample & 0xff); data[index * 2 + 1] = (byte)((sample >> 8) & 0xff);
            }
            return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = sampleRate, Stereo = false, Data = data };
        }

        /// <summary>
        /// 中文：把单路线性百分比转换为分贝并同步静音；调用前总线已由初始化保证存在。
        /// English: Converts one linear percentage to decibels and synchronizes mute; initialization guarantees the bus exists before use.
        /// </summary>
        private static void ApplyBus(string busName, int volumePercent, bool muted)
        {
            int busIndex = AudioServer.GetBusIndex(busName);
            float linear = Mathf.Clamp(volumePercent / 100.0f, 0.0f, 1.0f);
            AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(linear));
            AudioServer.SetBusMute(busIndex, muted);
        }
    }
}
