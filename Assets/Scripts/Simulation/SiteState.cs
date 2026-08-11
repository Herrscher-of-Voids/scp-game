using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class SiteState
    {
        /// <summary>中文：模拟命令使用的稳定正整数引用；由设施 JSON 的 siteId 提供。English: Stable positive simulation-command reference supplied by facility JSON siteId.</summary>
        public SiteId Id { get; set; }

        /// <summary>中文：项目内部稳定字符串 ID；SITE-45 两版本必须不同。English: Project-internal stable string ID; the two SITE-45 versions must differ.</summary>
        public string InternalStableId { get; set; } = string.Empty;

        /// <summary>中文：官方 canonical 编号；SITE-45 两版本保留共同值。English: Official canonical identifier, shared by the two SITE-45 versions.</summary>
        public string CanonicalId { get; set; } = string.Empty;

        /// <summary>中文：界面显示码；SITE-45 的 AU/US 后缀属于项目区分而非官方编号。English: UI display code; SITE-45 AU/US suffixes are project distinctions, not official identifiers.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>中文：来源表中的官方设施名称。English: Official facility name from the source table.</summary>
        public string DisplayLabel { get; set; } = string.Empty;

        /// <summary>中文：官方设施类型文本。English: Official facility type text.</summary>
        public string FacilityType { get; set; } = string.Empty;

        /// <summary>中文：来源位置原文，可表达保密、多地点、从属或非地球。English: Source location text, including redacted, multi-location, subordinate, or non-terrestrial cases.</summary>
        public string LocationText { get; set; } = string.Empty;

        /// <summary>中文：资料本身的位置精度，绝不因项目近似点提升。English: Source location precision, never promoted by a project approximation.</summary>
        public SiteLocationPrecision LocationPrecision { get; set; }

        /// <summary>中文：可确认国家；未知或非地球时为空。English: Confirmed country, empty for unknown or non-terrestrial locations.</summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>中文：经纬度转换后的地图万分比 X；0 表示不落普通地球地图。English: Map X in ten-thousandths converted from coordinates; zero means no ordinary Earth-map placement.</summary>
        public int MapX { get; set; }

        /// <summary>中文：经纬度转换后的地图万分比 Y；0 表示不落普通地球地图。English: Map Y in ten-thousandths converted from coordinates; zero means no ordinary Earth-map placement.</summary>
        public int MapY { get; set; }

        /// <summary>中文：地图点是否仅为项目级近似；所有当前有坐标的设施均为 true。English: Whether the map point is only a project approximation; true for every currently mapped facility.</summary>
        public bool IsMapApproximate { get; set; }

        /// <summary>中文：非地球设施不在普通地图创建标记。English: Non-terrestrial facilities do not create ordinary world-map markers.</summary>
        public bool IsNonTerrestrial { get; set; }

        /// <summary>中文：SCP-EN 官方来源 URL。English: Official SCP-EN source URL.</summary>
        public string EnUrl { get; set; } = string.Empty;

        /// <summary>中文：SCP-CN 官方来源 URL；无对应条目时为空。English: Official SCP-CN source URL, empty when no counterpart exists.</summary>
        public string CnUrl { get; set; } = string.Empty;

        /// <summary>中文：来源作品或 Canon 说明，不声明统一正史。English: Source-work or Canon note without asserting one unified continuity.</summary>
        public string SourceCanon { get; set; } = string.Empty;

        /// <summary>中文：项目冲突、近似点或内部后缀说明。English: Project note for conflicts, approximations, or internal suffixes.</summary>
        public string ProjectDistinctionNote { get; set; } = string.Empty;

        /// <summary>中文：现实设施所属洲；未知和非地球条目使用默认值但由精度标记阻止落图。English: Terrestrial continent; unknown and non-terrestrial entries use a default value but precision flags prevent mapping.</summary>
        public Continent Continent { get; set; }

        public int SecurityLevel { get; set; }

        public int AvailableObservers { get; set; }

        public int TrueStability { get; set; } = 8000;

        public int ReportedStability { get; set; } = 8000;

        public int ReportCredibility { get; set; } = 7000;

        public int TrueCasualties { get; set; }

        public int ReportedCasualties { get; set; }

        public int TrueResearchOutput { get; set; }

        public int ReportedResearchOutput { get; set; }

        public int AuditCyclesRemaining { get; set; }

        public bool IsOperational { get; set; } = true;
    }
}
