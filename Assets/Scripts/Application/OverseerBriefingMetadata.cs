using System;

using Newtonsoft.Json;

namespace Scp.Application
{
    /// <summary>
    /// 中文：O5 任命与交接页的持久化只读摘要。所有字段均为玩家可见文本，重载时直接读取以保证完全一致；内容描述当前新生时间线生成的世界状态。
    /// English: Persisted read-only summary for the O5 appointment and handover page. Every field is player-visible text and is read directly on reload for exact consistency; content describes the world state generated for the current new timeline.
    /// </summary>
    public sealed class OverseerBriefingMetadata
    {
        /// <summary>中文：玩家席位显示编号，例如 O5-4；创建后锁定。English: Player-facing seat designation such as O5-4; locked after creation.</summary>
        [JsonProperty("seatDesignation")]
        public string SeatDesignation { get; set; } = string.Empty;

        /// <summary>中文：不指向具体官方人物的前任离席类别。English: Predecessor departure category that does not identify a specific official character.</summary>
        [JsonProperty("predecessorDepartureCategory")]
        public string PredecessorDepartureCategory { get; set; } = string.Empty;

        /// <summary>中文：当前演示世界的基金会状态摘要。English: Foundation status summary for the current demo world.</summary>
        [JsonProperty("foundationStatusSummary")]
        public string FoundationStatusSummary { get; set; } = string.Empty;

        /// <summary>中文：恰好三份按顺序显示的优先简报；空数组只用于损坏或旧资料回退。English: Exactly three ordered priority briefs; an empty array is only a fallback for damaged or legacy metadata.</summary>
        [JsonProperty("priorityBriefs")]
        public string[] PriorityBriefs { get; set; } = Array.Empty<string>();

        /// <summary>中文：前任遗留政策、承诺和未结事项的合并只读说明。English: Combined read-only statement of predecessor policies, commitments and unresolved matters.</summary>
        [JsonProperty("predecessorLegacy")]
        public string PredecessorLegacy { get; set; } = string.Empty;
    }
}
