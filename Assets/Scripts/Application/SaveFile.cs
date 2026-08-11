using System;

using Newtonsoft.Json;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：完整、可独立恢复的存档数据；元数据与世界快照同处一份文件，避免索引丢失后无法识别存档。
    /// English: Complete independently restorable save data; metadata stays with the world snapshot so a lost index does not erase save identity.
    /// </summary>
    public sealed class SaveFile
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = SaveService.CurrentSchemaVersion;

        [JsonProperty("saveId")]
        public string SaveId { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("identity")]
        public IdentityRole Identity { get; set; } = IdentityRole.Overseer;

        [JsonProperty("difficulty")]
        public GameDifficulty Difficulty { get; set; } = GameDifficulty.Unknown;

        [JsonProperty("seed")]
        public string Seed { get; set; } = string.Empty;

        [JsonProperty("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }

        [JsonProperty("savedAtUtc")]
        public DateTime SavedAtUtc { get; set; }

        [JsonProperty("saveKind")]
        public SaveKind SaveKind { get; set; } = SaveKind.Unknown;

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; } = string.Empty;

        [JsonProperty("mode")]
        public SaveMode Mode { get; set; }

        [JsonProperty("parentSaveId")]
        public string? ParentSaveId { get; set; }

        [JsonProperty("world")]
        public WorldState World { get; set; } = new WorldState();

        [JsonProperty("worldFacts")]
        public WorldFacts WorldFacts { get; set; } = new WorldFacts();

        /// <summary>
        /// 中文：是否已确认一次性 O5 任命交接；新档为 false，旧档迁移为 true，确认后通过仓库原子保存。
        /// English: Whether the one-time O5 appointment briefing was acknowledged; new saves use false, migrated legacy saves use true, and acknowledgement is atomically persisted through the repository.
        /// </summary>
        [JsonProperty("briefingAcknowledged")]
        public bool BriefingAcknowledged { get; set; }

        /// <summary>
        /// 中文：任命页重新载入所需的确定性只读元数据；文本属于本项目内部开发世界，不包含未经调查的官方人物或故事。
        /// English: Deterministic read-only metadata required to reload the appointment page; text belongs to this project's internal development world and contains no unresearched official character or story claims.
        /// </summary>
        [JsonProperty("briefing")]
        public OverseerBriefingMetadata Briefing { get; set; } = new OverseerBriefingMetadata();

        [JsonProperty("commandLog")]
        public CommandLogEntry[] CommandLog { get; set; } = Array.Empty<CommandLogEntry>();

        /// <summary>中文：保存尚未执行的本 Tick 命令，防止退出检查点丢失玩家已确认操作。English: Stores commands not yet executed in the current tick so an exit checkpoint cannot lose confirmed player actions.</summary>
        [JsonProperty("pendingCommands")]
        public CommandLogEntry[] PendingCommands { get; set; } = Array.Empty<CommandLogEntry>();

        /// <summary>中文：记录自动存档/退出/终局检查点原因与序号，序号只单调递增。English: Records the reason and monotonic sequence of auto, exit, and terminal checkpoints.</summary>
        [JsonProperty("checkpoint")]
        public SaveSessionMetadata Checkpoint { get; set; } = new SaveSessionMetadata();

        /// <summary>中文：终局时生成的三部分只读尾声；终局档仍可查看但禁止继续。English: Read-only three-section epilogue generated at termination; ended saves remain viewable but cannot continue.</summary>
        [JsonProperty("epilogue")]
        public EpilogueReport Epilogue { get; set; } = new EpilogueReport();
    }
}
