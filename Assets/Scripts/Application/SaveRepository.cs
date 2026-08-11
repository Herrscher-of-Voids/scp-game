using System;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Scp.Domain;

namespace Scp.Application
{
    /// <summary>
    /// 中文：最近存档探测结果。结果对象不抛出可预期的文件、格式或兼容性错误，并明确区分主档与需确认的备份。
    /// English: Result of probing the latest save. Expected file, format and compatibility failures are returned rather than thrown, with primary and confirmation-required backup states kept distinct.
    /// </summary>
    public enum SaveProbeStatus
    {
        NoSave,
        PrimaryAvailable,
        BackupAvailable,
        IncompatibleVersion,
        InvalidOrCorrupt,
        IoFailure,
        Ended
    }

    /// <summary>
    /// 中文：携带探测分类、玩家可读详情和已验证路径；只有可用状态才包含路径。
    /// English: Carries the probe category, player-readable detail and validated path; a path is present only for usable states.
    /// </summary>
    public sealed class SaveProbeResult
    {
        public SaveProbeStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SaveId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    internal sealed class SaveIndex
    {
        [JsonProperty("latestSaveId")]
        public string LatestSaveId { get; set; } = string.Empty;

        /// <summary>中文：空值表示 main.json；0..2 表示对应自动存档槽。English: Null identifies main.json; 0..2 identifies the corresponding autosave slot.</summary>
        [JsonProperty("latestAutoSlot")]
        public int? LatestAutoSlot { get; set; }
    }

    /// <summary>
    /// 中文：主档或备份的独立文件状态；目录页必须同时展示两者，不能因主档成功而忽略备份。
    /// English: Independent primary or backup file state; the directory page must show both and must not hide the backup when the primary works.
    /// </summary>
    public enum SaveFileState
    {
        Missing,
        Available,
        InvalidOrCorrupt,
        IncompatibleVersion,
        IoFailure,
        Ended
    }

    /// <summary>
    /// 中文：不包含绝对路径、异常文本或堆栈的引擎无关存档目录项；可读元数据只来自文件内容和确定性日历换算。
    /// English: Engine-independent save-directory item without absolute paths, exception text or stack traces; readable metadata comes only from file contents and deterministic calendar conversion.
    /// </summary>
    public sealed class SaveDirectoryEntry
    {
        public string SaveId { get; set; } = string.Empty;
        public SaveFileState PrimaryState { get; set; }
        public SaveFileState BackupState { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public SaveFileMetadata Metadata { get; set; } = new SaveFileMetadata();
    }

    /// <summary>
    /// 中文：schema v5 的只读展示元数据；Tick 派生年月、周期内日期，绝不使用文件系统时间。
    /// English: Read-only schema-v5 display metadata; year, month and day are derived from Tick and never from filesystem timestamps.
    /// </summary>
    public sealed class SaveFileMetadata
    {
        public string SaveId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public IdentityRole Identity { get; set; } = IdentityRole.Unknown;
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Unknown;
        public string Seed { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime SavedAtUtc { get; set; }
        public SaveKind SaveKind { get; set; } = SaveKind.Unknown;
        public string GameVersion { get; set; } = string.Empty;
        public SaveMode Mode { get; set; }
        public bool BriefingAcknowledged { get; set; }
        public string O5Seat { get; set; } = string.Empty;
        public long WorldTick { get; set; }
        public int CurrentCycle { get; set; }
        public int CalendarYear { get; set; }
        public int CalendarMonth { get; set; }
        public int DayOfCycle { get; set; }
        public GameEndReason EndReason { get; set; }
        public bool IsEnded { get; set; }
    }

    public enum SaveDirectoryOperationStatus
    {
        Succeeded,
        NotFound,
        InvalidSaveId,
        IoFailure
    }

    public sealed class SaveDirectoryOperationResult
    {
        public SaveDirectoryOperationStatus Status { get; set; }
        public string SaveId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 中文：新建存档与现有主档的重复分类；标志可同时表示同名和完整配置相同。
    /// English: Duplicate categories between a proposed save and existing primary saves; flags may report both the same name and an identical configuration.
    /// </summary>
    [Flags]
    public enum DuplicateSaveMatch
    {
        None = 0,
        SameName = 1,
        IdenticalConfiguration = 2
    }

    /// <summary>
    /// 中文：重复探测的引擎无关结果。SkippedSaveCount 只供日志统计损坏、过新或不可读主档，不包含内部路径。
    /// English: Engine-independent duplicate-probe result. SkippedSaveCount is only for logging corrupt, future-version or unreadable primary saves and contains no internal paths.
    /// </summary>
    public sealed class DuplicateSaveProbeResult
    {
        public DuplicateSaveMatch Match { get; set; }
        public int SkippedSaveCount { get; set; }
    }

    /// <summary>
    /// 中文：管理绝对 saves 根目录下的单存档目录、主档、备份和最近索引。调用方必须先把 Godot user:// 路径全局化，本类不引用引擎。
    /// English: Manages per-save directories, primary files, backups and the latest index under an absolute saves root. Callers must globalize Godot user:// first; this type has no engine dependency.
    /// </summary>
    public sealed class SaveRepository
    {
        private readonly string _rootDirectory;
        private readonly SaveService _saveService;

        public SaveRepository(string rootDirectory, SaveService? saveService = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
            }

            _rootDirectory = Path.GetFullPath(rootDirectory);
            _saveService = saveService ?? new SaveService();
        }

        /// <summary>
        /// 中文：先提交 main.tmp，再以原子替换保留上一主档为 main.bak，最后原子更新索引；索引因此只指向成功提交的主档。
        /// English: Commits main.tmp first, atomically replaces the primary while retaining main.bak, then atomically updates the index, so the index only names a successfully committed primary.
        /// </summary>
        public void Save(SaveFile save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            ValidateSaveId(save.SaveId);
            ValidateSave(save);
            Directory.CreateDirectory(_rootDirectory);
            string saveDirectory = GetSaveDirectory(save.SaveId);
            Directory.CreateDirectory(saveDirectory);

            DateTime now = DateTime.UtcNow;
            if (save.CreatedAtUtc == default)
            {
                save.CreatedAtUtc = now;
            }
            save.SavedAtUtc = now;

            string mainPath = Path.Combine(saveDirectory, "main.json");
            string temporaryPath = Path.Combine(saveDirectory, "main.tmp");
            string backupPath = Path.Combine(saveDirectory, "main.bak");
            File.WriteAllText(temporaryPath, _saveService.Serialize(save));
            CommitTemporaryFile(temporaryPath, mainPath, backupPath);

            string indexPath = Path.Combine(_rootDirectory, "index.json");
            string indexTemporaryPath = Path.Combine(_rootDirectory, "index.tmp");
            File.WriteAllText(indexTemporaryPath, JsonConvert.SerializeObject(new SaveIndex { LatestSaveId = save.SaveId }, Formatting.Indented));
            CommitTemporaryFile(indexTemporaryPath, indexPath, null);
        }

        /// <summary>
        /// 中文：把检查点写入三个固定自动槽之一；序号模 3 决定槽位，每槽各自使用 .tmp/.bak 原子轮换，成功后索引精确指向该槽。
        /// English: Writes a checkpoint to one of three fixed autosave slots; sequence modulo three selects the slot, each slot independently rotates .tmp/.bak atomically, and the index points precisely to the committed slot.
        /// </summary>
        /// <returns>中文：本次提交使用的槽号 0..2。English: The committed slot number from 0 through 2.</returns>
        public int SaveAutoCheckpoint(SaveFile save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            ValidateSaveId(save.SaveId);
            ValidateSave(save);
            Directory.CreateDirectory(_rootDirectory);
            string saveDirectory = GetSaveDirectory(save.SaveId);
            Directory.CreateDirectory(saveDirectory);
            int slot = (int)(Math.Abs(save.Checkpoint.CheckpointSequence) % 3);
            save.SaveKind = SaveKind.Auto;
            DateTime now = DateTime.UtcNow;
            if (save.CreatedAtUtc == default) save.CreatedAtUtc = now;
            save.SavedAtUtc = now;
            string stem = "auto-" + slot;
            string destination = Path.Combine(saveDirectory, stem + ".json");
            string temporary = Path.Combine(saveDirectory, stem + ".tmp");
            string backup = Path.Combine(saveDirectory, stem + ".bak");
            File.WriteAllText(temporary, _saveService.Serialize(save));
            CommitTemporaryFile(temporary, destination, backup);
            WriteIndex(save.SaveId, slot);
            return slot;
        }

        /// <summary>
        /// 中文：载入指定自动槽用于恢复或只读查看；槽号边界为 0..2，终局档允许读取但由 GameSession 拒绝推进。
        /// English: Loads a selected autosave slot for recovery or read-only viewing; slot bounds are 0..2, and ended saves may be read while GameSession rejects advancement.
        /// </summary>
        public SaveFile LoadAutoCheckpoint(string saveId, int slot, bool useBackup = false)
        {
            ValidateSaveId(saveId);
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
            string extension = useBackup ? ".bak" : ".json";
            SaveFile save = _saveService.Load(Path.Combine(GetSaveDirectory(saveId), "auto-" + slot + extension));
            ValidateSave(save);
            return save;
        }

        /// <summary>
        /// 中文：按当前索引载入主档或自动槽；该接口允许终局档进入查看流程，但调用方不得把它作为继续目标。
        /// English: Loads the primary or autosave slot named by the current index; ended saves may enter viewing flows but callers must not treat them as resumable.
        /// </summary>
        public SaveFile LoadLatestForViewing()
        {
            SaveIndex index = ReadIndex() ?? throw new InvalidOperationException("Save index is missing.");
            return index.LatestAutoSlot.HasValue ? LoadAutoCheckpoint(index.LatestSaveId, index.LatestAutoSlot.Value) : Load(index.LatestSaveId, false);
        }

        /// <summary>
        /// 中文：按合法目录名确定性枚举所有逻辑存档，并分别探测主档和备份；异常目录仍返回目录项。 
        /// English: Deterministically enumerates every logical save with a valid directory name and probes primary and backup independently; abnormal directories still produce entries.
        /// </summary>
        public SaveDirectoryEntry[] EnumerateDirectory()
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return Array.Empty<SaveDirectoryEntry>();
            }

            var entries = new System.Collections.Generic.List<SaveDirectoryEntry>();
            foreach (string directory in Directory.EnumerateDirectories(_rootDirectory))
            {
                string saveId = Path.GetFileName(directory);
                try
                {
                    ValidateSaveId(saveId);
                    if (saveId.EndsWith(".deleting", StringComparison.OrdinalIgnoreCase) || saveId.Contains(".deleting-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    SaveDirectoryEntry entry = new SaveDirectoryEntry { SaveId = saveId };
                    SaveFileMetadata? primaryMetadata;
                    entry.PrimaryState = ProbeDirectoryFile(Path.Combine(directory, "main.json"), saveId, out primaryMetadata);
                    SaveFileMetadata? backupMetadata;
                    entry.BackupState = ProbeDirectoryFile(Path.Combine(directory, "main.bak"), saveId, out backupMetadata);
                    entry.Metadata = primaryMetadata ?? backupMetadata ?? new SaveFileMetadata { SaveId = saveId };
                    entry.StatusMessage = DescribeDirectoryStatus(entry);
                    entries.Add(entry);
                }
                catch (ArgumentException)
                {
                    // 中文：非法目录名不是存档，直接跳过；English: An invalid directory name is not a save and is skipped.
                }
            }

            return entries.OrderBy(entry => entry.SaveId, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// 中文：默认仅把当前 schema、可读、非终局且支持 Overseer 身份的主档写为最近目标，不改写保存内容或时间。
        /// English: By default, writes only a current-schema, readable, non-ended Overseer primary as the latest target without rewriting save data or timestamps.
        /// </summary>
        /// <param name="saveId">中文：存档目录标识；English: Save directory identifier.</param>
        /// <param name="allowBackupRecovery">中文：仅显式备份恢复时为 true；English: True only for an explicit backup-recovery confirmation.</param>
        /// <returns>中文：只报告原子 index 更新结果；English: Reports only the atomic index update result.</returns>
        public SaveDirectoryOperationResult SetLatest(string saveId, bool allowBackupRecovery = false)
        {
            try
            {
                ValidateSaveId(saveId);
                SaveFile save;
                string targetPath;
                if (allowBackupRecovery)
                {
                    // 中文：备份恢复必须同时看到损坏主档和合规备份；English: Recovery requires both a corrupt primary and a compliant backup.
                    SaveFileState primaryState = ProbeDirectoryFile(Path.Combine(GetSaveDirectory(saveId), "main.json"), saveId, out _);
                    if (primaryState == SaveFileState.IncompatibleVersion || primaryState != SaveFileState.InvalidOrCorrupt)
                    {
                        return Operation(SaveDirectoryOperationStatus.IoFailure, saveId, "主档状态不允许恢复备份。");
                    }
                    targetPath = Path.Combine(GetSaveDirectory(saveId), "main.bak");
                    save = Load(saveId, true);
                }
                else
                {
                    targetPath = Path.Combine(GetSaveDirectory(saveId), "main.json");
                    save = Load(saveId, false);
                }

                // 中文：读取磁盘原始 schema，禁止迁移后的旧档伪装成当前格式；English: Read the on-disk schema so migrated legacy data cannot masquerade as current format.
                int storedSchemaVersion = JObject.Parse(File.ReadAllText(targetPath)).Value<int?>("schemaVersion") ?? 1;
                if (storedSchemaVersion != SaveService.CurrentSchemaVersion || save.SchemaVersion != SaveService.CurrentSchemaVersion || save.World.Failure.IsEnded || save.Identity != IdentityRole.Overseer)
                {
                    return Operation(SaveDirectoryOperationStatus.IoFailure, saveId, "该存档不能作为继续目标。");
                }

                Directory.CreateDirectory(_rootDirectory);
                string temporaryPath = Path.Combine(_rootDirectory, "index.tmp");
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(new SaveIndex { LatestSaveId = saveId }, Formatting.Indented));
                CommitTemporaryFile(temporaryPath, Path.Combine(_rootDirectory, "index.json"), null);
                return Operation(SaveDirectoryOperationStatus.Succeeded, saveId, "最近存档已更新。");
            }
            catch (ArgumentException)
            {
                return Operation(SaveDirectoryOperationStatus.InvalidSaveId, saveId, "存档标识无效。");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException || exception is InvalidOperationException)
            {
                return Operation(SaveDirectoryOperationStatus.IoFailure, saveId, "无法更新最近存档。");
            }
        }

        /// <summary>
        /// 中文：先把合法存档目录原子移动到隔离目录，再修复索引，最后删除隔离目录；操作期间由仓库锁串行化。
        /// English: Atomically moves a valid save directory into an isolated directory, repairs the index, then deletes the isolated directory; the repository lock serializes the operation.
        /// </summary>
        public SaveDirectoryOperationResult DeleteSave(string saveId)
        {
            lock (_rootDirectory)
            {
                try
                {
                    ValidateSaveId(saveId);
                    string source = GetSaveDirectory(saveId);
                    if (!Directory.Exists(source)) return Operation(SaveDirectoryOperationStatus.NotFound, saveId, "存档不存在。");
                    string isolated = source + ".deleting-" + Guid.NewGuid().ToString("N");
                    Directory.Move(source, isolated);
                    try
                    {
                        SaveIndex? index = ReadIndex();
                        if (index != null && string.Equals(index.LatestSaveId, saveId, StringComparison.Ordinal))
                        {
                            RepairLatestIndex(saveId);
                        }
                        Directory.Delete(isolated, true);
                    }
                    catch
                    {
                        if (!Directory.Exists(source) && Directory.Exists(isolated)) Directory.Move(isolated, source);
                        throw;
                    }
                    return Operation(SaveDirectoryOperationStatus.Succeeded, saveId, "存档已删除。");
                }
                catch (ArgumentException)
                {
                    return Operation(SaveDirectoryOperationStatus.InvalidSaveId, saveId, "存档标识无效。");
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    return Operation(SaveDirectoryOperationStatus.IoFailure, saveId, "无法删除存档。");
                }
            }
        }

        /// <summary>
        /// 中文：探测索引指定的最近存档。主档损坏时仅检查备份是否可用，不会静默把备份当主档载入。
        /// English: Probes the save named by the index. If the primary is corrupt, the backup is checked only for availability and is never silently loaded as the primary.
        /// English: Probes the save named by the index. If the primary is corrupt, the backup is checked only for availability and is never silently loaded as the primary.
        /// </summary>
        public SaveProbeResult ProbeLatest()
        {
            string indexPath = Path.Combine(_rootDirectory, "index.json");
            if (!File.Exists(indexPath))
            {
                return Result(SaveProbeStatus.NoSave, "没有可继续的存档。");
            }

            try
            {
                var index = JsonConvert.DeserializeObject<SaveIndex>(File.ReadAllText(indexPath));
                if (index == null || string.IsNullOrWhiteSpace(index.LatestSaveId))
                {
                    return Result(SaveProbeStatus.InvalidOrCorrupt, "存档索引无效或已损坏。");
                }

                ValidateSaveId(index.LatestSaveId);
                string directory = GetSaveDirectory(index.LatestSaveId);
                if (index.LatestAutoSlot.HasValue)
                {
                    int slot = index.LatestAutoSlot.Value;
                    if (slot < 0 || slot > 2) return Result(SaveProbeStatus.InvalidOrCorrupt, "自动存档索引无效。", index.LatestSaveId);
                    string autoPath = Path.Combine(directory, "auto-" + slot + ".json");
                    SaveProbeResult auto = ProbeFile(autoPath, index.LatestSaveId, SaveProbeStatus.PrimaryAvailable);
                    if (auto.Status == SaveProbeStatus.PrimaryAvailable || auto.Status == SaveProbeStatus.IncompatibleVersion || auto.Status == SaveProbeStatus.Ended || auto.Status == SaveProbeStatus.IoFailure) return auto;
                    return ProbeFile(Path.Combine(directory, "auto-" + slot + ".bak"), index.LatestSaveId, SaveProbeStatus.BackupAvailable);
                }
                string mainPath = Path.Combine(directory, "main.json");
                SaveProbeResult primary = ProbeFile(mainPath, index.LatestSaveId, SaveProbeStatus.PrimaryAvailable);
                if (primary.Status == SaveProbeStatus.PrimaryAvailable || primary.Status == SaveProbeStatus.IncompatibleVersion || primary.Status == SaveProbeStatus.Ended || primary.Status == SaveProbeStatus.IoFailure)
                {
                    return primary;
                }

                string backupPath = Path.Combine(directory, "main.bak");
                SaveProbeResult backup = ProbeFile(backupPath, index.LatestSaveId, SaveProbeStatus.BackupAvailable);
                if (backup.Status == SaveProbeStatus.BackupAvailable || backup.Status == SaveProbeStatus.IncompatibleVersion || backup.Status == SaveProbeStatus.Ended || backup.Status == SaveProbeStatus.IoFailure)
                {
                    return backup;
                }

                return primary.Status == SaveProbeStatus.NoSave && backup.Status == SaveProbeStatus.NoSave
                    ? Result(SaveProbeStatus.InvalidOrCorrupt, "索引指向的存档文件不存在。", index.LatestSaveId)
                    : primary;
            }
            catch (UnauthorizedAccessException exception)
            {
                return Result(SaveProbeStatus.IoFailure, "没有权限读取存档：" + exception.Message);
            }
            catch (IOException exception)
            {
                return Result(SaveProbeStatus.IoFailure, "读取存档时发生 I/O 错误：" + exception.Message);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                return Result(SaveProbeStatus.InvalidOrCorrupt, "存档索引无效或已损坏。");
            }
        }

        /// <summary>
        /// 中文：载入已经由探测结果确认的主档或备份；useBackup 必须由玩家确认流程明确提供。
        /// English: Loads a primary or backup already approved by probing; useBackup must come explicitly from the player's confirmation flow.
        /// </summary>
        public SaveFile Load(string saveId, bool useBackup)
        {
            ValidateSaveId(saveId);
            string fileName = useBackup ? "main.bak" : "main.json";
            SaveFile save = _saveService.Load(Path.Combine(GetSaveDirectory(saveId), fileName));
            ValidateSave(save);
            return save;
        }

        /// <summary>
        /// 中文：只扫描 saves 根目录下一层、目录名为合法 save-id 的 main.json，并比较去除首尾空白后的名称与完整创建配置。损坏、未来版本或不可读存档会跳过，绝不阻止新建。
        /// English: Scans only valid save-id directories directly under the saves root and compares trimmed names plus the complete creation configuration in main.json. Corrupt, future-version or unreadable saves are skipped and never block creation.
        /// </summary>
        /// <param name="candidate">中文：尚未写盘的新建存档候选；比较字段为 DisplayName、Identity、Difficulty、Seed 和 Mode。English: Proposed save not yet written; compared fields are DisplayName, Identity, Difficulty, Seed and Mode.</param>
        /// <returns>中文：累计匹配标志与跳过数量；不返回路径或既有存档内容。English: Accumulated match flags and skipped count, without paths or existing save contents.</returns>
        public DuplicateSaveProbeResult ProbeDuplicates(SaveFile candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var result = new DuplicateSaveProbeResult();
            if (!Directory.Exists(_rootDirectory))
            {
                return result;
            }

            string candidateName = candidate.DisplayName.Trim();
            foreach (string directory in Directory.EnumerateDirectories(_rootDirectory))
            {
                string saveId = Path.GetFileName(directory);
                try
                {
                    ValidateSaveId(saveId);
                    string path = Path.Combine(directory, "main.json");
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var root = JObject.Parse(File.ReadAllText(path));
                    int version = root.Value<int?>("schemaVersion") ?? 1;
                    if (version > SaveService.CurrentSchemaVersion)
                    {
                        result.SkippedSaveCount++;
                        continue;
                    }

                    SaveFile existing = _saveService.Deserialize(root.ToString(Formatting.None));
                    ValidateSave(existing);
                    bool sameName = string.Equals(existing.DisplayName.Trim(), candidateName, StringComparison.OrdinalIgnoreCase);
                    bool identical = sameName
                        && existing.Identity == candidate.Identity
                        && existing.Difficulty == candidate.Difficulty
                        && string.Equals(existing.Seed, candidate.Seed, StringComparison.Ordinal)
                        && existing.Mode == candidate.Mode;
                    if (sameName)
                    {
                        result.Match |= DuplicateSaveMatch.SameName;
                    }
                    if (identical)
                    {
                        result.Match |= DuplicateSaveMatch.IdenticalConfiguration;
                    }
                }
                catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException || exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
                {
                    result.SkippedSaveCount++;
                }
            }

            return result;
        }

        private SaveFileState ProbeDirectoryFile(string path, string saveId, out SaveFileMetadata? metadata)
        {
            metadata = null;
            if (!File.Exists(path)) return SaveFileState.Missing;
            try
            {
                string json = File.ReadAllText(path);
                JObject root = JObject.Parse(json);
                int version = root.Value<int?>("schemaVersion") ?? 1;
                if (version > SaveService.CurrentSchemaVersion) return SaveFileState.IncompatibleVersion;
                SaveFile save = _saveService.Deserialize(json);
                ValidateSave(save);
                metadata = ToMetadata(save, saveId);
                return save.World.Failure.IsEnded ? SaveFileState.Ended : SaveFileState.Available;
            }
            catch (UnauthorizedAccessException) { return SaveFileState.IoFailure; }
            catch (IOException) { return SaveFileState.IoFailure; }
            catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException || exception is ArgumentException)
            {
                return SaveFileState.InvalidOrCorrupt;
            }
        }

        private static SaveFileMetadata ToMetadata(SaveFile save, string saveId)
        {
            FoundationCalendar.Resolve(FoundationCalendar.StandaloneStartYear, FoundationCalendar.StandaloneStartMonth, FoundationCalendar.ElapsedCycles(save.World.Tick), out int year, out int month);
            return new SaveFileMetadata
            {
                SaveId = saveId,
                DisplayName = save.DisplayName,
                Identity = save.Identity,
                Difficulty = save.Difficulty,
                Seed = save.Seed,
                CreatedAtUtc = save.CreatedAtUtc,
                SavedAtUtc = save.SavedAtUtc,
                SaveKind = save.SaveKind,
                GameVersion = save.GameVersion,
                Mode = save.Mode,
                BriefingAcknowledged = save.BriefingAcknowledged,
                O5Seat = save.Briefing?.SeatDesignation ?? string.Empty,
                WorldTick = save.World.Tick,
                CurrentCycle = save.World.Council.CurrentCycle,
                CalendarYear = year,
                CalendarMonth = month,
                DayOfCycle = FoundationCalendar.DayOfCycle(save.World.Tick),
                EndReason = save.World.Failure.EndReason,
                IsEnded = save.World.Failure.IsEnded
            };
        }

        private static string DescribeDirectoryStatus(SaveDirectoryEntry entry)
        {
            if (entry.Metadata.IsEnded) return "终局存档，终局报告页尚未开放。";
            if (entry.PrimaryState == SaveFileState.Available) return entry.Metadata.BriefingAcknowledged ? "可载入。" : "任命待确认。";
            if (entry.PrimaryState == SaveFileState.InvalidOrCorrupt && entry.BackupState == SaveFileState.Available) return "主档损坏，备份可恢复。";
            if (entry.PrimaryState == SaveFileState.IncompatibleVersion) return "主档版本过新，当前版本不可载入。";
            return entry.PrimaryState switch
            {
                SaveFileState.Missing => "主档不存在。",
                SaveFileState.InvalidOrCorrupt => "主档损坏或无效。",
                SaveFileState.IoFailure => "主档读取失败。",
                _ => "存档状态异常。"
            };
        }

        private SaveIndex? ReadIndex()
        {
            string path = Path.Combine(_rootDirectory, "index.json");
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<SaveIndex>(File.ReadAllText(path));
        }

        /// <summary>中文：使用同一原子提交规则更新最近索引；slot 为空时指向主档。English: Updates the latest index with the same atomic commit rule; a null slot points to the primary save.</summary>
        private void WriteIndex(string saveId, int? slot)
        {
            string temporary = Path.Combine(_rootDirectory, "index.tmp");
            File.WriteAllText(temporary, JsonConvert.SerializeObject(new SaveIndex { LatestSaveId = saveId, LatestAutoSlot = slot }, Formatting.Indented));
            CommitTemporaryFile(temporary, Path.Combine(_rootDirectory, "index.json"), null);
        }

        private void RepairLatestIndex(string deletedSaveId)
        {
            SaveDirectoryEntry? replacement = EnumerateDirectory()
                .Where(entry => !string.Equals(entry.SaveId, deletedSaveId, StringComparison.Ordinal)
                    && entry.PrimaryState == SaveFileState.Available
                    && !entry.Metadata.IsEnded
                    && entry.Metadata.Identity == IdentityRole.Overseer)
                .OrderByDescending(entry => entry.Metadata.SavedAtUtc)
                .ThenByDescending(entry => entry.Metadata.CreatedAtUtc)
                .ThenBy(entry => entry.SaveId, StringComparer.Ordinal)
                .FirstOrDefault();
            string indexPath = Path.Combine(_rootDirectory, "index.json");
            if (replacement == null)
            {
                if (File.Exists(indexPath)) File.Delete(indexPath);
                return;
            }

            string temporaryPath = Path.Combine(_rootDirectory, "index.tmp");
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(new SaveIndex { LatestSaveId = replacement.SaveId }, Formatting.Indented));
            CommitTemporaryFile(temporaryPath, indexPath, null);
        }

        private static SaveDirectoryOperationResult Operation(SaveDirectoryOperationStatus status, string saveId, string message)
        {
            return new SaveDirectoryOperationResult { Status = status, SaveId = saveId, Message = message };
        }

        private SaveProbeResult ProbeFile(string path, string saveId, SaveProbeStatus availableStatus)
        {
            if (!File.Exists(path))
            {
                return Result(SaveProbeStatus.NoSave, "存档文件不存在。", saveId);
            }

            try
            {
                string json = File.ReadAllText(path);
                var root = JObject.Parse(json);
                int version = root.Value<int?>("schemaVersion") ?? 1;
                if (version > SaveService.CurrentSchemaVersion)
                {
                    return Result(SaveProbeStatus.IncompatibleVersion, "存档由更高版本游戏创建，请更新游戏后继续。", saveId);
                }

                SaveFile save = _saveService.Deserialize(json);
                ValidateSave(save);
                if (save.World.Failure.IsEnded)
                {
                    return Result(SaveProbeStatus.Ended, "最近存档已经终局，无法继续。", saveId);
                }

                string message = availableStatus == SaveProbeStatus.BackupAvailable
                    ? "主存档已损坏，但上一保存版本可用。"
                    : "可继续最近存档。";
                return Result(availableStatus, message, saveId, path);
            }
            catch (UnauthorizedAccessException exception)
            {
                return Result(SaveProbeStatus.IoFailure, "没有权限读取存档：" + exception.Message, saveId);
            }
            catch (IOException exception)
            {
                return Result(SaveProbeStatus.IoFailure, "读取存档时发生 I/O 错误：" + exception.Message, saveId);
            }
            catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException || exception is ArgumentException)
            {
                return Result(SaveProbeStatus.InvalidOrCorrupt, "存档损坏或内容无效。", saveId);
            }
        }

        private static void ValidateSave(SaveFile save)
        {
            if (save.World == null || save.World.Failure == null)
            {
                throw new InvalidOperationException("The save does not contain a valid world.");
            }
            if (save.Identity != IdentityRole.Overseer)
            {
                throw new InvalidOperationException("Only Overseer saves are supported.");
            }
            if (string.IsNullOrWhiteSpace(save.SaveId))
            {
                throw new InvalidOperationException("The save id is missing.");
            }
        }

        private static void ValidateSaveId(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId) || saveId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveId.Contains("..") || saveId.Contains(Path.DirectorySeparatorChar.ToString()) || saveId.Contains(Path.AltDirectorySeparatorChar.ToString()))
            {
                throw new ArgumentException("The save id is invalid.", nameof(saveId));
            }
        }

        private string GetSaveDirectory(string saveId)
        {
            return Path.Combine(_rootDirectory, saveId);
        }

        private static void CommitTemporaryFile(string temporaryPath, string destinationPath, string? backupPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }

        private static SaveProbeResult Result(SaveProbeStatus status, string message, string saveId = "", string path = "")
        {
            return new SaveProbeResult { Status = status, Message = message, SaveId = saveId, Path = path };
        }
    }
}
