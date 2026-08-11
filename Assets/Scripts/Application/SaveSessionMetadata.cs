using System;
using Newtonsoft.Json;

namespace Scp.Application
{
    /// <summary>
    /// 中文：描述一次会话的创建配置与最后检查点，不从文件系统时间推导游戏事实。
    /// English: Describes session creation configuration and the last checkpoint without deriving game facts from filesystem time.
    /// </summary>
    public sealed class SaveSessionMetadata
    {
        [JsonProperty("checkpointReason")]
        public CheckpointReason CheckpointReason { get; set; }

        [JsonProperty("checkpointSequence")]
        public long CheckpointSequence { get; set; }

        [JsonProperty("checkpointTick")]
        public long CheckpointTick { get; set; }
    }

    public enum CheckpointReason
    {
        None,
        MonthBoundary,
        MajorCommand,
        Exit,
        Terminal
    }
}
