using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：从单个 JSON 文件加载并严格验证 O5 正式设施目录；该实现不依赖 Unity 或 Godot，可供测试、控制台和客户端共用。
    /// English: Loads and strictly validates the official O5 facility catalogue from one JSON file; the implementation is engine-neutral and shared by tests, console, and clients.
    /// </summary>
    public sealed class FacilityDataLoader
    {
        /// <summary>中文：当前确认的正式运行实体总数。English: Confirmed total number of official runtime entities.</summary>
        public const int ExpectedFacilityCount = 89;

        private static readonly HashSet<string> ExcludedCanonicalIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "SITE-0", "SITE-5", "SITE-418", "SITE-⌘"
        };

        private readonly JsonSerializerSettings _settings;

        /// <summary>中文：初始化仅允许字符串枚举且禁止类型元数据的确定性 JSON 设置。English: Initialises deterministic JSON settings with string enums and no type metadata.</summary>
        public FacilityDataLoader()
        {
            _settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Error
            };
            _settings.Converters.Add(new StringEnumConverter());
        }

        /// <summary>
        /// 中文：读取 UTF-8 JSON、验证全部跨记录约束，并按稳定 SiteId 返回新数组。文件不存在、JSON 无效或任何约束失败时抛出异常，不返回部分数据。
        /// English: Reads UTF-8 JSON, validates all cross-record constraints, and returns a new array ordered by stable SiteId. Missing files, invalid JSON, or any failed constraint throw instead of returning partial data.
        /// </summary>
        /// <param name="filePath">中文：设施目录 JSON 的绝对或调用方可解析路径。English: Absolute or caller-resolvable path to the facility catalogue JSON.</param>
        /// <returns>中文：恰好 89 项、已验证且按 SiteId 升序的定义。English: Exactly 89 validated definitions ordered by ascending SiteId.</returns>
        public FacilityDefinition[] LoadFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Facility catalogue was not found.", filePath);
            }

            FacilityCatalogue? catalogue = JsonConvert.DeserializeObject<FacilityCatalogue>(File.ReadAllText(filePath), _settings);
            if (catalogue == null)
            {
                throw new JsonSerializationException("Facility catalogue is empty.");
            }

            Validate(catalogue);
            return catalogue.Facilities.OrderBy(facility => facility.SiteId).ToArray();
        }

        /// <summary>
        /// 中文：验证格式版本、数量、稳定键、显示码、运行时编号、官方 URL、来源字段、坐标语义及 SITE-45 唯一重复例外。
        /// English: Validates schema, count, stable keys, display codes, runtime numbers, official URLs, source fields, coordinate semantics, and the sole SITE-45 duplicate exception.
        /// </summary>
        /// <param name="catalogue">中文：反序列化后的目录；不得为 null。English: Deserialised catalogue; must not be null.</param>
        public static void Validate(FacilityCatalogue catalogue)
        {
            if (catalogue.SchemaVersion != 1)
            {
                throw new InvalidDataException("Facility catalogue schemaVersion must be 1.");
            }

            FacilityDefinition[] facilities = catalogue.Facilities ?? Array.Empty<FacilityDefinition>();
            if (facilities.Length != ExpectedFacilityCount)
            {
                throw new InvalidDataException("Facility catalogue must contain exactly 89 entities.");
            }

            RequireUnique(facilities.Select(value => value.InternalStableId), "internalStableId");
            RequireUnique(facilities.Select(value => value.DisplayCode), "displayCode");
            RequireUnique(facilities.Select(value => value.SiteId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "siteId");

            foreach (FacilityDefinition facility in facilities)
            {
                RequireText(facility.InternalStableId, "internalStableId");
                RequireText(facility.CanonicalId, "canonicalId");
                RequireText(facility.DisplayCode, "displayCode");
                RequireText(facility.DisplayName, "displayName");
                RequireText(facility.FacilityType, "facilityType");
                RequireText(facility.Region, "region");
                RequireText(facility.SourceCanon, "sourceCanon");
                RequireText(facility.SourceSite, "sourceSite");
                RequireText(facility.License, "license");
                RequireText(facility.ProjectNotes, "projectNotes");
                if (facility.SiteId <= 0) throw new InvalidDataException("siteId must be positive: " + facility.InternalStableId);
                if (ExcludedCanonicalIds.Contains(facility.CanonicalId)) throw new InvalidDataException("Excluded facility is present: " + facility.CanonicalId);
                RequireOfficialUrl(facility.EnUrl, "scp-wiki.wikidot.com", false, facility.InternalStableId);
                RequireOfficialUrl(facility.CnUrl, "scp-wiki-cn.wikidot.com", true, facility.InternalStableId);
                ValidateCoordinates(facility);
            }

            var canonicalGroups = facilities.GroupBy(value => value.CanonicalId, StringComparer.Ordinal).ToArray();
            foreach (var group in canonicalGroups.Where(value => value.Count() > 1))
            {
                string[] ids = group.Select(value => value.InternalStableId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] codes = group.Select(value => value.DisplayCode).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (!string.Equals(group.Key, "SITE-45", StringComparison.Ordinal) ||
                    !ids.SequenceEqual(new[] { "SITE-45-AU", "SITE-45-US" }) ||
                    !codes.SequenceEqual(new[] { "SITE-45-AU", "SITE-45-US" }))
                {
                    throw new InvalidDataException("Only the confirmed SITE-45 AU/US pair may share a canonicalId.");
                }
            }

            if (canonicalGroups.SingleOrDefault(value => value.Key == "SITE-45")?.Count() != 2)
            {
                throw new InvalidDataException("The confirmed SITE-45 AU/US pair is required.");
            }
        }

        /// <summary>中文：要求字段非空白。English: Requires a field to contain non-whitespace text.</summary>
        private static void RequireText(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Facility field is required: " + field);
        }

        /// <summary>中文：按序数比较检查字段唯一，空值也会作为验证错误暴露。English: Checks uniqueness using ordinal comparison and exposes null values as validation failures.</summary>
        private static void RequireUnique(IEnumerable<string?> values, string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string? value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) throw new InvalidDataException("Facility field must be unique and non-empty: " + field);
            }
        }

        /// <summary>中文：只接受 HTTPS 官方 Wikidot 主机；CN URL 允许 null 以表达 SITE-512 缺项。English: Accepts only HTTPS official Wikidot hosts; CN URL may be null for the documented SITE-512 absence.</summary>
        private static void RequireOfficialUrl(string? value, string expectedHost, bool optional, string stableId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (optional) return;
                throw new InvalidDataException("Official URL is required: " + stableId);
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Facility URL must use the official domain: " + stableId);
            }
        }

        /// <summary>中文：保证纬经成对、范围有效、所有落图坐标均明确为近似，且非地球设施绝无地球坐标。English: Ensures coordinate pairs, valid ranges, explicit approximation for every mapped point, and no Earth coordinates for non-terrestrial facilities.</summary>
        private static void ValidateCoordinates(FacilityDefinition facility)
        {
            bool hasLatitude = facility.Latitude.HasValue;
            bool hasLongitude = facility.Longitude.HasValue;
            if (hasLatitude != hasLongitude) throw new InvalidDataException("Latitude and longitude must be supplied together: " + facility.InternalStableId);
            if (hasLatitude && (facility.Latitude < -90 || facility.Latitude > 90 || facility.Longitude < -180 || facility.Longitude > 180)) throw new InvalidDataException("Coordinate is outside decimal-degree bounds: " + facility.InternalStableId);
            if (hasLatitude && facility.CoordinateKind == FacilityCoordinateKind.Unknown) throw new InvalidDataException("Mapped coordinates must be marked approximate: " + facility.InternalStableId);
            if (!hasLatitude && facility.CoordinateKind != FacilityCoordinateKind.Unknown) throw new InvalidDataException("Coordinate kind requires a coordinate pair: " + facility.InternalStableId);
            if (facility.LocationPrecision == SiteLocationPrecision.NonTerrestrial && (hasLatitude || facility.Continent.HasValue)) throw new InvalidDataException("Non-terrestrial facilities cannot have Earth map coordinates: " + facility.InternalStableId);
        }
    }
}
