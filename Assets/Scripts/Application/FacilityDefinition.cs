using System;
using Newtonsoft.Json;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：引擎无关的正式设施数据定义。字符串保留官方来源文字，数值坐标仅允许带明确 CoordinateKind 的十进制度近似值。
    /// English: Engine-neutral official facility definition. Strings preserve source wording, while numeric coordinates are decimal-degree approximations allowed only with an explicit CoordinateKind.
    /// </summary>
    public sealed class FacilityDefinition
    {
        /// <summary>中文：项目内部稳定键；用于校验和存档映射，必须唯一。English: Project-internal stable key used for validation and save mapping; it must be unique.</summary>
        public string InternalStableId { get; set; } = string.Empty;

        /// <summary>中文：稳定正整数运行时编号；写入 SiteId，不能由数组位置临时推导。English: Stable positive runtime number written to SiteId; it must not be derived transiently from array position.</summary>
        public int SiteId { get; set; }

        /// <summary>中文：官方 canonical 编号；只有两个 SITE-45 实体允许重复。English: Official canonical identifier; only the two SITE-45 entities may duplicate it.</summary>
        public string CanonicalId { get; set; } = string.Empty;

        /// <summary>中文：界面和命令使用的内部显示码。English: Internal display code used by UI and commands.</summary>
        public string DisplayCode { get; set; } = string.Empty;

        /// <summary>中文：来源表中的官方显示名称。English: Official display name from the source table.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>中文：来源表中的设施类型。English: Facility type from the source table.</summary>
        public string FacilityType { get; set; } = string.Empty;

        /// <summary>中文：来源表中的地区原文；可能是保密、多地点或非地球描述。English: Source-table region text, which may describe redaction, multiple locations, or a non-terrestrial place.</summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>中文：可确认国家；不可确认时为 null。English: Confirmed country, or null when unavailable.</summary>
        public string? Country { get; set; }

        /// <summary>中文：可确认地球洲别；非地球、保密或未知位置为 null。English: Confirmed terrestrial continent; null for non-terrestrial, redacted, or unknown locations.</summary>
        public Continent? Continent { get; set; }

        /// <summary>中文：来源位置精度，不得由近似坐标提升为 Exact。English: Source location precision, which must never be promoted to Exact by an approximate coordinate.</summary>
        public SiteLocationPrecision LocationPrecision { get; set; }

        /// <summary>中文：十进制度纬度，范围 -90..90；无安全地图点时为 null。English: Latitude in decimal degrees, -90..90; null when no safe map point exists.</summary>
        public double? Latitude { get; set; }

        /// <summary>中文：十进制度经度，范围 -180..180；无安全地图点时为 null。English: Longitude in decimal degrees, -180..180; null when no safe map point exists.</summary>
        public double? Longitude { get; set; }

        /// <summary>中文：坐标来源类别；正式数据禁止使用未定义值。English: Coordinate provenance category; official data forbids undefined values.</summary>
        public FacilityCoordinateKind CoordinateKind { get; set; }

        /// <summary>中文：SCP-EN 官方域名来源 URL。English: Source URL on the official SCP-EN domain.</summary>
        public string EnUrl { get; set; } = string.Empty;

        /// <summary>中文：SCP-CN 官方域名来源 URL；无对应中文条目时为 null。English: Source URL on the official SCP-CN domain, or null when no corresponding Chinese entry exists.</summary>
        public string? CnUrl { get; set; }

        /// <summary>中文：来源作品或 Canon 说明，不自行统一世界线。English: Source work or Canon note without inventing a unified continuity.</summary>
        public string SourceCanon { get; set; } = string.Empty;

        /// <summary>中文：来源站点声明。English: Source-site declaration.</summary>
        public string SourceSite { get; set; } = string.Empty;

        /// <summary>中文：SCP 衍生内容许可证。English: License for SCP-derived content.</summary>
        public string License { get; set; } = string.Empty;

        /// <summary>中文：来源查询日期，ISO 8601 日期。English: Source retrieval date in ISO 8601 date form.</summary>
        public string RetrievedDate { get; set; } = string.Empty;

        /// <summary>中文：项目二次整理、坐标近似和冲突处理说明。English: Project notes covering adaptation, coordinate approximation, and conflict handling.</summary>
        public string ProjectNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// 中文：设施坐标来源强度；OfficialApproximation 仅表示官方给出的近似点，绝不表示精确坐标。
    /// English: Facility-coordinate provenance; OfficialApproximation means an officially supplied approximation and never an exact coordinate.
    /// </summary>
    public enum FacilityCoordinateKind
    {
        Unknown,
        OfficialApproximation,
        ProjectRegionalApproximation
    }

    /// <summary>
    /// 中文：设施目录根对象；SchemaVersion 控制兼容边界，Facilities 必须恰好包含当前确认的 89 个实体。
    /// English: Facility catalogue root; SchemaVersion controls compatibility and Facilities must contain exactly the 89 confirmed entities.
    /// </summary>
    public sealed class FacilityCatalogue
    {
        /// <summary>中文：设施数据格式版本；当前只接受 1。English: Facility data schema version; currently only version 1 is accepted.</summary>
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        /// <summary>中文：按稳定运行时编号排序的正式设施实体。English: Official facility entities ordered by stable runtime number.</summary>
        [JsonProperty("facilities")]
        public FacilityDefinition[] Facilities { get; set; } = Array.Empty<FacilityDefinition>();
    }
}