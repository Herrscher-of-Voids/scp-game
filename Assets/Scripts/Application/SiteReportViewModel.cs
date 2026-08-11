using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 单个设施在 O5 视角下的可见数据。
    /// 字段分两类：站点自报值（可能失真）与结构性事实（编号、洲、等级，不失真）。
    /// 失真规则见 10-O5监督者.md 第 5 节，投影逻辑在 OverseerPerspective。
    /// </summary>
    public sealed class SiteReportViewModel
    {
        /// <summary>设施编号。结构性事实，不受报告失真影响。</summary>
        public SiteId SiteId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string DisplayLabel { get; set; } = string.Empty;

        public string LocationText { get; set; } = string.Empty;

        public SiteLocationPrecision LocationPrecision { get; set; }

        public int MapX { get; set; }

        public int MapY { get; set; }

        /// <summary>所属大洲。用于世界地图定位与按洲筛选，属结构性事实。</summary>
        public Continent Continent { get; set; }

        /// <summary>安保等级。结构性事实，不由站点自报。</summary>
        public int SecurityLevel { get; set; }

        /// <summary>可用观察员数量。结构性事实，收容协议的硬约束之一。</summary>
        public int AvailableObservers { get; set; }

        /// <summary>该设施内的异常数量。由世界状态聚合得出，不经站点自报。</summary>
        public int AnomalyCount { get; set; }

        /// <summary>该设施内处于突破状态（BreachStage 高于 Latent）的异常数量。</summary>
        public int BreachingAnomalyCount { get; set; }

        /// <summary>设施是否仍在运作。结构性事实。</summary>
        public bool IsOperational { get; set; }

        /// <summary>稳定度，万分比定点数。未审计时为站点自报值，可能失真。</summary>
        public int Stability { get; set; }

        /// <summary>累计伤亡。未审计时为站点自报值，通常被低报。</summary>
        public int Casualties { get; set; }

        /// <summary>研究产出。未审计时为站点自报值，通常被高报。</summary>
        public int ResearchOutput { get; set; }

        /// <summary>
        /// 本周期该设施是否处于审计状态。
        /// 为 true 时上面三个自报字段等于真实值；为 false 时数值照常显示但不可信。
        /// </summary>
        public bool IsAudited { get; set; }
    }
}
