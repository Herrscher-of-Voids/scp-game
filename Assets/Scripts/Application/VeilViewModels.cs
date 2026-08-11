using System;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>中文：帷幕页根投影，包含全球态、匿名事件和仅帷幕警报；所有数组均与世界状态分离。English: Root veil-page projection containing global posture, anonymous incidents, and veil-only alerts; every array is detached from world state.</summary>
    public sealed class VeilViewModel
    {
        public int GlobalIntegrity { get; set; }
        public int[] IntegrityByContinent { get; set; } = Array.Empty<int>();
        public VeilIncidentViewModel[] Incidents { get; set; } = Array.Empty<VeilIncidentViewModel>();
        public OverseerAlertViewModel[] Alerts { get; set; } = Array.Empty<OverseerAlertViewModel>();
        /// <summary>中文：十一项帷幕总览的纯投影；每项包含不重复计数的全球值和固定七洲明细。English: Pure projection of the eleven veil overview metrics; each item contains a non-duplicated global value and fixed seven-continent detail.</summary>
        public VeilOverviewMetricViewModel[] OverviewMetrics { get; set; } = Array.Empty<VeilOverviewMetricViewModel>();
    }

    /// <summary>中文：一个匿名事件的 O5 可见投影；不包含隐藏真相或未经确认的设施/坐标。English: O5-visible projection of one anonymous incident; it contains no hidden truth or unconfirmed facility/coordinates.</summary>
    public sealed class VeilIncidentViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SourceCategory { get; set; } = string.Empty;
        public long CreatedTick { get; set; }
        public long DiscoveredTick { get; set; }
        public Continent OriginContinent { get; set; }
        public string FacilityStableId { get; set; } = string.Empty;
        public VeilLocationPrecision LocationPrecision { get; set; }
        public int Severity { get; set; }
        public VeilIncidentStage Stage { get; set; }
        public int Loss { get; set; }
        public int Recovery { get; set; }
        /// <summary>中文：基于严重度、阶段、节点暴露和真实涉及洲数得到的确定性人数估算，单位为人；它不是精确统计。English: Deterministic affected-person estimate based on severity, stage, node exposure, and actual involved-continent count, measured in people; it is not an exact census.</summary>
        public long EstimatedAffectedPeople { get; set; }
        /// <summary>中文：节点 Exposure 的饱和累计值，单位为万分比点；仅用于报告影响量，不改变事件状态。English: Saturating sum of node Exposure values in ten-thousandth points; used only for report impact and never mutates incident state.</summary>
        public long Exposure { get; set; }
        /// <summary>中文：事件起源洲与节点洲的去重有序集合；缺失节点时仍至少包含起源洲。English: Deduplicated ordered set of origin and node continents; it still contains the origin when no nodes exist.</summary>
        public Continent[] InvolvedContinents { get; set; } = Array.Empty<Continent>();
        public VeilIncidentStatus Status { get; set; }
        public VeilPropagationNodeViewModel[] Nodes { get; set; } = Array.Empty<VeilPropagationNodeViewModel>();
        public VeilTimelineEntryViewModel[] Timeline { get; set; } = Array.Empty<VeilTimelineEntryViewModel>();
    }

    /// <summary>中文：可见传播节点；坐标只有在模拟明确提供且精度不是洲级时才投影，否则保持 0。English: Visible spread node; coordinates are projected only when explicitly present and precision is above continent-only, otherwise they remain zero.</summary>
    public sealed class VeilPropagationNodeViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public Continent Continent { get; set; }
        public long FirstObservedTick { get; set; }
        public VeilLocationPrecision LocationPrecision { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public int Exposure { get; set; }
    }

    /// <summary>中文：帷幕事件公开时间线条目。English: Public veil-incident timeline entry.</summary>
    public sealed class VeilTimelineEntryViewModel
    {
        public string StableId { get; set; } = string.Empty;
        public long Tick { get; set; }
        public VeilActionKind Action { get; set; }
        public string Effect { get; set; } = string.Empty;
    }

    /// <summary>中文：单张底部总览卡的数据；Value 是全球口径，ByContinent 固定为七项且不得用全球值复制填充。English: Data for one bottom overview card; Value is global while ByContinent always has seven entries and must never be filled by repeating the global value.</summary>
    public sealed class VeilOverviewMetricViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long Value { get; set; }
        public long[] ByContinent { get; set; } = new long[7];
        public VeilMetricFormat Format { get; set; }
        public string TooltipNote { get; set; } = string.Empty;
    }

    /// <summary>中文：总览卡格式契约；Count 为整数、Ratio 为万分比、People 为人数、Money 为货币。English: Overview-card formatting contract: Count is integer, Ratio is ten-thousandths, People is persons, and Money is currency.</summary>
    public enum VeilMetricFormat { Count, Ratio, People, Money }
}
