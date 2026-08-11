using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Scp.Application
{
    /// <summary>
    /// 中文：引擎无关的应用设置快照，保存显示、界面、效果、音频、控制和语言状态。
    /// English: Engine-independent application settings snapshot covering display, interface, effects, audio, controls and language.
    /// 参数与单位：窗口尺寸使用像素，UI 缩放和音量使用百分比，音量 0 表示静音；枚举字符串必须保持稳定以便跨版本迁移。
    /// Parameters and units: window sizes use pixels, UI scale and volume use percentages, volume 0 means silent; enum strings remain stable for migration.
    /// 边界情况：缺失或无法解析的字段使用默认值；集合始终归一化为非空字典。
    /// Edge cases: missing or unparseable fields use defaults; the binding collection is always normalized to a non-null dictionary.
    /// 确定性：默认值不依赖机器当前配置，便于测试和首次启动一致。
    /// Determinism: defaults do not depend on machine configuration, keeping tests and first launch consistent.
    /// </summary>
    public sealed class ApplicationSettings
    {
        public string WindowMode { get; set; } = "windowed";
        public int WindowWidth { get; set; } = 1280;
        public int WindowHeight { get; set; } = 720;
        public bool VSync { get; set; } = true;
        public int UiScalePercent { get; set; } = 100;
        public bool Borderless { get; set; }
        public bool DynamicBackground { get; set; } = true;
        public bool Scanlines { get; set; } = true;
        public bool InterfaceAnimations { get; set; } = true;
        public bool CrisisFlicker { get; set; } = true;
        public bool HighContrastFocus { get; set; }
        public bool ReduceMotion { get; set; }
        public int MasterVolume { get; set; } = 100;
        public int MusicVolume { get; set; } = 80;
        public int AmbienceVolume { get; set; } = 80;
        /// <summary>中文：匿名会议合成语音的独立音量百分比。English: Independent volume percentage for anonymous synthesized council dialogue.</summary>
        public int DialogueVolume { get; set; } = 80;
        public int UiVolume { get; set; } = 90;
        public bool MasterMuted { get; set; }
        public bool MusicMuted { get; set; }
        public bool AmbienceMuted { get; set; }
        /// <summary>中文：仅静音 Dialogue 总线，不影响环境音或字幕。English: Mutes only the Dialogue bus without affecting ambience or subtitles.</summary>
        public bool DialogueMuted { get; set; }
        public bool UiMuted { get; set; }
        public string Language { get; set; } = "zh_CN";
        public Dictionary<string, string> KeyBindings { get; set; } = CreateDefaultBindings();

        /// <summary>中文：创建稳定的首次启动默认设置。English: Creates stable first-launch defaults.</summary>
        public static ApplicationSettings CreateDefault() => new ApplicationSettings { KeyBindings = CreateDefaultBindings() };

        /// <summary>中文：复制快照，页面编辑永远不直接修改已应用状态。English: Clones a snapshot so page edits never mutate the applied state directly.</summary>
        public ApplicationSettings Clone() => JsonConvert.DeserializeObject<ApplicationSettings>(JsonConvert.SerializeObject(this)) ?? CreateDefault();

        /// <summary>中文：修正旧版本、手工编辑或损坏字段；不抛出玩家可预期的配置错误。English: Repairs fields from old versions, manual edits or partial corruption without throwing expected configuration errors.</summary>
        public void Normalize()
        {
            WindowMode = WindowMode is "windowed" or "maximized" or "fullscreen" ? WindowMode : "windowed";
            WindowWidth = WindowWidth is 1280 or 1600 or 1920 ? WindowWidth : 1280;
            WindowHeight = WindowHeight is 720 or 900 or 1080 ? WindowHeight : 720;
            UiScalePercent = UiScalePercent is 80 or 90 or 100 or 110 or 125 or 150 ? UiScalePercent : 100;
            Language = Language is "zh_CN" or "zh_HK" or "en" ? Language : "zh_CN";
            MasterVolume = Math.Clamp(MasterVolume, 0, 100);
            MusicVolume = Math.Clamp(MusicVolume, 0, 100);
            AmbienceVolume = Math.Clamp(AmbienceVolume, 0, 100);
            // 中文：旧存档缺少字段时 JSON 会保留属性默认值；手工越界值在进入 Godot Dialogue 总线前钳制。
            // English: Older files retain the property default when absent; manually out-of-range values are clamped before reaching the Godot Dialogue bus.
            DialogueVolume = Math.Clamp(DialogueVolume, 0, 100);
            UiVolume = Math.Clamp(UiVolume, 0, 100);
            KeyBindings ??= CreateDefaultBindings();
            foreach (var pair in CreateDefaultBindings()) if (!KeyBindings.ContainsKey(pair.Key) || string.IsNullOrWhiteSpace(KeyBindings[pair.Key])) KeyBindings[pair.Key] = pair.Value;
        }

        /// <summary>中文：返回确认过的核心动作默认键位；English: Returns default key bindings for the confirmed core actions.</summary>
        private static Dictionary<string, string> CreateDefaultBindings() => new(StringComparer.Ordinal)
        {
            ["confirm"] = "Enter", ["cancel"] = "Escape", ["pause"] = "P", ["move_up"] = "W", ["move_down"] = "S",
            ["move_left"] = "A", ["move_right"] = "D", ["map_zoom"] = "Mouse Wheel", ["map_pan"] = "Middle Mouse", ["time_speed"] = "Space"
        };
    }

    /// <summary>
    /// 中文：使用 settings.json、settings.json.tmp、settings.json.bak 的单文件原子设置存储。
    /// English: Atomic single-file settings store using settings.json, settings.json.tmp and settings.json.bak.
    /// 路径参数：主配置文件绝对路径；临时与备份路径由同一目录派生，禁止依赖引擎全局状态。
    /// Path parameter: absolute path to the primary file; temporary and backup paths derive from the same directory without engine-global dependencies.
    /// </summary>
    public sealed class ApplicationSettingsStore
    {
        private readonly string _path;
        private readonly string _tempPath;
        private readonly string _backupPath;

        public ApplicationSettingsStore(string settingsJsonPath)
        {
            _path = settingsJsonPath;
            _tempPath = settingsJsonPath + ".tmp";
            _backupPath = settingsJsonPath + ".bak";
        }

        /// <summary>中文：加载主配置，主配置损坏时回退备份，两个文件都不可读时返回默认值。English: Loads primary, falls back to backup on corruption, and returns defaults when neither is readable.</summary>
        public ApplicationSettings Load()
        {
            ApplicationSettings? settings = TryRead(_path) ?? TryRead(_backupPath) ?? ApplicationSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }

        /// <summary>中文：先写临时文件并刷新，再轮换备份和主文件；失败时不删除可用主文件。English: Writes and flushes a temporary file before rotating backup and primary; failures never delete a usable primary file.</summary>
        public void Save(ApplicationSettings settings)
        {
            settings.Normalize();
            string directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Settings path must have a directory.");
            Directory.CreateDirectory(directory);
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            using (FileStream stream = new(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream)) { writer.Write(json); writer.Flush(); stream.Flush(true); }
            if (File.Exists(_path))
            {
                File.Copy(_path, _backupPath, true);
                // 中文：当前目标框架没有覆盖式 File.Move；主档已复制到备份后先删除旧目标，再在同一目录移动已刷新的临时文件。
                // English: The current target framework lacks overwrite File.Move; after copying the primary to backup, remove the old target and move the flushed temporary file within the same directory.
                File.Delete(_path);
            }
            File.Move(_tempPath, _path);
        }

        private static ApplicationSettings? TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonConvert.DeserializeObject<ApplicationSettings>(File.ReadAllText(path));
            }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
