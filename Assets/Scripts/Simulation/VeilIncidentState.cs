using System;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：帷幕事件从未公开线索到多洲危机的五个有序阶段；数值顺序是确定性推进契约，存档后不得重排。
    /// English: Five ordered veil-incident stages from undisclosed clues to a multi-continent crisis; numeric order is a deterministic progression contract and must not be reordered after saves exist.
    /// </summary>
    public enum VeilIncidentStage { ClueBacklog, LocalPublicAwareness, CrossRegionMediaSpread, PublicInstitutionFailure, MultiContinentSystemicCrisis }

    /// <summary>中文：事件业务状态；暂停阻止自动传播，撤销结束事件但保留全部历史。English: Incident business status; paused blocks automatic propagation, while withdrawn closes an incident without deleting history.</summary>
    public enum VeilIncidentStatus { Active, Paused, Recovering, Resolved, Withdrawn }

    /// <summary>中文：O5 已确认的位置精度，不得由投影或 UI 自行提升。English: O5-confirmed location precision which projection and UI must never increase.</summary>
    public enum VeilLocationPrecision { ContinentOnly, Approximate, Confirmed }

    /// <summary>中文：事件级处置种类；值稳定用于命令日志与时间线。English: Stable incident-action values used by command logs and timelines.</summary>
    public enum VeilActionKind { Monitor, Investigate, SuppressPublicity, CoordinateInstitutions, AssessWitnessDisposition, EmergencyOperation, Pause, Withdraw }

    /// <summary>中文：单个传播节点记录已确认洲别、可选万分比地图坐标和首次出现 Tick；坐标为 0 表示没有可落图位置。English: One propagation node records confirmed continent, optional map coordinates in ten-thousandths, and first-seen tick; zero coordinates mean no point location is known.</summary>
    public sealed class VeilPropagationNode
    {
        public string StableId { get; set; } = string.Empty;
        public Continent Continent { get; set; }
        public long FirstObservedTick { get; set; }
        public VeilLocationPrecision LocationPrecision { get; set; } = VeilLocationPrecision.ContinentOnly;
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int Exposure { get; set; }
    }

    /// <summary>中文：只追加的处置/阶段时间线；Effect 为公开摘要，不保存隐藏真相。English: Append-only action/stage timeline; Effect is a public summary and stores no hidden truth.</summary>
    public sealed class VeilDispositionRecord
    {
        public string StableId { get; set; } = string.Empty;
        public long Tick { get; set; }
        public VeilActionKind Action { get; set; }
        public string Effect { get; set; } = string.Empty;
    }

    /// <summary>
    /// 中文：独立帷幕事件持久化根，控制事件身份、发现信息、五阶段传播、累计损失/恢复和处置历史。严重度、损失、恢复与 Exposure 均为 0..10000 无量纲万分比；设施引用仅保存项目内部稳定 ID。数组顺序与稳定 ID 共同保证同种子、同命令日志得到相同结果。
    /// English: Independent persisted veil-incident root controlling identity, discovery data, five-stage spread, accumulated loss/recovery, and disposition history. Severity, loss, recovery and Exposure are dimensionless 0..10000 values; facility references store only project-internal stable IDs. Array order plus stable IDs ensure identical seeds and command logs yield identical outcomes.
    /// </summary>
    public sealed class VeilIncidentState
    {
        public string StableId { get; set; } = string.Empty;
        public string AnonymousTitle { get; set; } = string.Empty;
        public string SourceCategory { get; set; } = string.Empty;
        public long CreatedTick { get; set; }
        public long DiscoveredTick { get; set; }
        public Continent OriginContinent { get; set; }
        public string? FacilityStableId { get; set; }
        public VeilLocationPrecision LocationPrecision { get; set; } = VeilLocationPrecision.ContinentOnly;
        public int Severity { get; set; }
        public VeilIncidentStage CurrentStage { get; set; }
        public VeilPropagationNode[] PropagationNodes { get; set; } = Array.Empty<VeilPropagationNode>();
        public int Loss { get; set; }
        public int Recovery { get; set; }
        public VeilIncidentStatus Status { get; set; } = VeilIncidentStatus.Active;
        public VeilDispositionRecord[] Dispositions { get; set; } = Array.Empty<VeilDispositionRecord>();
        public long LastProgressTick { get; set; }
        public int NextRecordSequence { get; set; }
    }
}
