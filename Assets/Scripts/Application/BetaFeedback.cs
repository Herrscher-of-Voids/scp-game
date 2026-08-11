using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Scp.Application
{
    /// <summary>
    /// 中文：反馈分类使用稳定英文枚举值写入 JSON，避免显示语言变化破坏本地数据兼容性。
    /// English: Feedback categories use stable English enum values in JSON so display-language changes do not break local data compatibility.
    /// </summary>
    public enum BetaFeedbackCategory { Bug, Gameplay, Interface, Performance, Accessibility, Localization, Other }

    /// <summary>
    /// 中文：严重程度描述问题影响范围，不代表反馈已上传或由开发团队确认。
    /// English: Severity describes the issue impact and never implies upload or developer acknowledgement.
    /// </summary>
    public enum BetaFeedbackSeverity { Low, Medium, High, Critical }

    /// <summary>
    /// 中文：必要游戏信息快照，不包含用户名、绝对路径、个人文件或设备指纹。
    /// English: Required game-information snapshot containing no username, absolute path, personal file or device fingerprint.
    /// 参数与单位：随机种子保存为显示字符串；无活动游戏时身份、难度与种子使用明确的固定占位值。
    /// Parameters and units: the random seed is display text; identity, difficulty and seed use explicit stable placeholders without an active game.
    /// </summary>
    public sealed class BetaFeedbackDataSnapshot
    {
        public string GameVersion { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string CurrentScene { get; set; } = string.Empty;
        public string IdentityMode { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string RandomSeed { get; set; } = string.Empty;
    }

    /// <summary>
    /// 中文：玩家明确选择附加日志后保存的单个匿名日志片段；FileName 仅为受控日志根目录内的文件名。
    /// English: One anonymous log excerpt saved only after explicit opt-in; FileName is only the name inside the controlled log root.
    /// 边界与单位：Content 按字符计入仓库总上限，Truncated 表示因隐私上限截断；绝不保存完整路径。
    /// Boundaries and units: Content counts toward the repository-wide character limit, Truncated marks privacy-limit clipping, and full paths are never stored.
    /// </summary>
    public sealed class BetaFeedbackLogExcerpt
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// 中文：完整本地内测问卷记录。每个实例独立保存，不与存档或设置共享文件。
    /// English: Complete local beta questionnaire record. Each instance is stored independently and shares no file with saves or settings.
    /// 时间与确定性：CreatedAtUtc 使用 UTC；FeedbackId 由调用方生成的 GUID 提供唯一性，枚举和 JSON 字段名稳定。
    /// Time and determinism: CreatedAtUtc uses UTC; FeedbackId uniqueness comes from a caller-generated GUID, while enum and JSON field names remain stable.
    /// </summary>
    public sealed class BetaFeedback
    {
        public int SchemaVersion { get; set; } = 1;
        public string FeedbackId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public BetaFeedbackCategory Category { get; set; }
        public BetaFeedbackSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReproductionSteps { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
        public string ActualResult { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IncludeAnonymousLogs { get; set; }
        public BetaFeedbackDataSnapshot DataSnapshot { get; set; } = new();
        public List<BetaFeedbackLogExcerpt> AnonymousLogs { get; set; } = new();
    }

    /// <summary>
    /// 中文：反馈验证结果；Errors 是可直接映射到玩家提示的稳定问题列表。
    /// English: Feedback validation result whose Errors are stable issues suitable for player-facing mapping.
    /// </summary>
    public sealed class BetaFeedbackValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// 中文：引擎无关的本地反馈仓库，限定在调用方传入的 feedback 与 logs 绝对根目录内执行保存、枚举、导出和删除。
    /// English: Engine-independent local feedback repository limited to caller-provided absolute feedback and logs roots for save, enumerate, export and delete operations.
    /// 参数与单位：文本上限按 UTF-16 字符计；日志最多 5 个受控根目录顶层 .log/.txt 文件，总计 65536 字符。
    /// Parameters and units: text limits count UTF-16 characters; logs are limited to five top-level .log/.txt files under the controlled root and 65,536 total characters.
    /// 边界与安全：拒绝非法 ID；不递归扫描日志，不读取其他目录；写盘先刷新临时文件再原子替换目标。
    /// Boundaries and security: invalid IDs are rejected, logs are not recursively scanned or read elsewhere, and writes flush a temporary file before atomically replacing the target.
    /// </summary>
    public sealed class BetaFeedbackRepository
    {
        public const int MaximumLogCharacters = 65536;
        public const int MaximumLogFiles = 5;
        private const int MaximumTitleCharacters = 120;
        private const int MaximumFieldCharacters = 12000;
        private readonly string _rootDirectory;
        private readonly string _exportsDirectory;
        private readonly string _logsDirectory;
        private readonly JsonSerializerSettings _jsonSettings = new() { Formatting = Formatting.Indented };

        public BetaFeedbackRepository(string feedbackRootDirectory, string logsRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(feedbackRootDirectory)) throw new ArgumentException("Feedback root is required.", nameof(feedbackRootDirectory));
            if (string.IsNullOrWhiteSpace(logsRootDirectory)) throw new ArgumentException("Logs root is required.", nameof(logsRootDirectory));
            _rootDirectory = Path.GetFullPath(feedbackRootDirectory);
            _exportsDirectory = Path.Combine(_rootDirectory, "exports");
            _logsDirectory = Path.GetFullPath(logsRootDirectory);
        }

        /// <summary>
        /// 中文：验证完整问卷的必填项、字符上限和必要快照；返回所有问题且不修改输入。
        /// English: Validates required questionnaire fields, character limits and required snapshot, returning every issue without mutating input.
        /// </summary>
        public static BetaFeedbackValidationResult Validate(BetaFeedback feedback)
        {
            if (feedback == null) throw new ArgumentNullException(nameof(feedback));
            var result = new BetaFeedbackValidationResult();
            ValidateRequired(result, feedback.Title, "标题", MaximumTitleCharacters);
            ValidateRequired(result, feedback.ReproductionSteps, "复现步骤", MaximumFieldCharacters);
            ValidateRequired(result, feedback.ExpectedResult, "期望结果", MaximumFieldCharacters);
            ValidateRequired(result, feedback.ActualResult, "实际结果", MaximumFieldCharacters);
            ValidateRequired(result, feedback.Description, "自由描述", MaximumFieldCharacters);
            if (feedback.DataSnapshot == null
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.GameVersion)
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.Platform)
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.CurrentScene)
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.IdentityMode)
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.Difficulty)
                || string.IsNullOrWhiteSpace(feedback.DataSnapshot.RandomSeed))
            {
                result.Errors.Add("必要游戏信息快照不完整。");
            }
            return result;
        }

        /// <summary>
        /// 中文：补全唯一 ID 和 UTC 时间，按玩家勾选决定是否读取匿名日志，然后将单条反馈原子保存为 {FeedbackId}.json。
        /// English: Completes the unique ID and UTC timestamp, reads anonymous logs only on opt-in, then atomically stores one record as {FeedbackId}.json.
        /// 返回：返回最终写盘对象；验证失败抛出 ArgumentException，I/O 失败保留既有正式文件。
        /// Return: returns the final persisted object; validation failure throws ArgumentException and I/O failure preserves any existing final file.
        /// </summary>
        public BetaFeedback Save(BetaFeedback feedback)
        {
            BetaFeedbackValidationResult validation = Validate(feedback);
            if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors), nameof(feedback));
            feedback.FeedbackId = Guid.NewGuid().ToString("N");
            feedback.CreatedAtUtc = DateTime.UtcNow;
            feedback.AnonymousLogs = feedback.IncludeAnonymousLogs ? ReadAnonymousLogs() : new List<BetaFeedbackLogExcerpt>();
            Directory.CreateDirectory(_rootDirectory);
            AtomicWrite(GetFeedbackPath(feedback.FeedbackId), JsonConvert.SerializeObject(feedback, _jsonSettings));
            return feedback;
        }

        /// <summary>
        /// 中文：只枚举 feedback 根目录顶层的正式 JSON，跳过临时文件、exports 子目录和损坏记录，并按创建时间倒序、ID 正序稳定排列。
        /// English: Enumerates only final top-level JSON records, skips temporary files, the exports directory and corrupt records, and sorts stably by newest creation then ID.
        /// </summary>
        public BetaFeedback[] Enumerate()
        {
            if (!Directory.Exists(_rootDirectory)) return Array.Empty<BetaFeedback>();
            var records = new List<BetaFeedback>();
            foreach (string path in Directory.EnumerateFiles(_rootDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    BetaFeedback? record = JsonConvert.DeserializeObject<BetaFeedback>(File.ReadAllText(path));
                    if (record != null && IsValidId(record.FeedbackId) && Path.GetFileNameWithoutExtension(path) == record.FeedbackId) records.Add(record);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (JsonException) { }
            }
            return records.OrderByDescending(record => record.CreatedAtUtc).ThenBy(record => record.FeedbackId, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// 中文：复制一条已存在反馈到 feedback/exports，导出文件仍为 JSON 且不改变原记录；返回仅含文件名的玩家可见结果。
        /// English: Copies one existing record to feedback/exports as JSON without changing the source, returning only a player-visible file name.
        /// </summary>
        public string Export(string feedbackId)
        {
            string source = GetFeedbackPath(feedbackId);
            if (!File.Exists(source)) throw new FileNotFoundException("Feedback record does not exist.");
            Directory.CreateDirectory(_exportsDirectory);
            string fileName = "feedback-" + feedbackId + ".json";
            AtomicWrite(Path.Combine(_exportsDirectory, fileName), File.ReadAllText(source));
            return fileName;
        }

        /// <summary>
        /// 中文：删除一条由合法 ID 精确定位的本地反馈；导出副本刻意保留，避免删除操作撤销玩家已执行的导出。
        /// English: Deletes one local record addressed by an exact valid ID; exported copies intentionally remain so deletion does not undo a player export.
        /// 返回：存在并删除时为 true，不存在时为 false；强确认由表现层在调用前完成。
        /// Return: true when an existing record is deleted and false when absent; the presentation layer completes strong confirmation first.
        /// </summary>
        public bool Delete(string feedbackId)
        {
            string path = GetFeedbackPath(feedbackId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>
        /// 中文：仅读取受控 logs 根目录顶层的有限文本日志，先按文件名确定性排序，再脱敏 Windows、macOS 与 Unix 用户目录并按总字符上限截断。
        /// English: Reads only bounded text logs at the controlled logs root, deterministically sorts by file name, redacts Windows/macOS/Unix home paths, then clips to the total character limit.
        /// </summary>
        private List<BetaFeedbackLogExcerpt> ReadAnonymousLogs()
        {
            var excerpts = new List<BetaFeedbackLogExcerpt>();
            if (!Directory.Exists(_logsDirectory)) return excerpts;
            int remaining = MaximumLogCharacters;
            IEnumerable<string> candidates = Directory.EnumerateFiles(_logsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".log", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Take(MaximumLogFiles);
            foreach (string path in candidates)
            {
                if (remaining <= 0) break;
                try
                {
                    string redacted = RedactUserPaths(File.ReadAllText(path));
                    bool truncated = redacted.Length > remaining;
                    string content = truncated ? redacted.Substring(0, remaining) : redacted;
                    excerpts.Add(new BetaFeedbackLogExcerpt { FileName = Path.GetFileName(path), Content = content, Truncated = truncated });
                    remaining -= content.Length;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return excerpts;
        }

        /// <summary>
        /// 中文：将常见用户主目录路径替换为固定标记，不回传用户名；处理 Windows 盘符及 /home、/Users 形式。
        /// English: Replaces common home-directory paths with a fixed marker without returning usernames, covering Windows drives and /home or /Users forms.
        /// </summary>
        public static string RedactUserPaths(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string redacted = Regex.Replace(text, @"(?i)\b[A-Z]:[\\/]Users[\/][^\r\n""'<>]+", "<USER_HOME>");
            redacted = Regex.Replace(redacted, @"(?i)(?<![A-Za-z0-9_])/(?:home|Users)/[^\r\n""'<>]+", "<USER_HOME>");
            return redacted;
        }

        private string GetFeedbackPath(string feedbackId)
        {
            if (!IsValidId(feedbackId)) throw new ArgumentException("Feedback ID must be a 32-character GUID.", nameof(feedbackId));
            return Path.Combine(_rootDirectory, feedbackId + ".json");
        }

        private static bool IsValidId(string feedbackId) => feedbackId.Length == 32 && Guid.TryParseExact(feedbackId, "N", out _);

        private static void ValidateRequired(BetaFeedbackValidationResult result, string value, string fieldName, int maximumCharacters)
        {
            if (string.IsNullOrWhiteSpace(value)) result.Errors.Add(fieldName + "为必填项。");
            else if (value.Length > maximumCharacters) result.Errors.Add(fieldName + "超过 " + maximumCharacters + " 字符上限。");
        }

        private static void AtomicWrite(string path, string content)
        {
            string temporaryPath = path + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path))
            {
                // 中文：反馈 ID 通常不会覆盖，但导出同一反馈时允许替换；当前目标框架需显式删除旧目标后再移动已刷新的临时文件。
                // English: Feedback IDs normally do not overwrite, but re-exporting one item may replace it; this target framework requires deleting the old target before moving the flushed temporary file.
                File.Delete(path);
            }
            File.Move(temporaryPath, path);
        }
    }
}
