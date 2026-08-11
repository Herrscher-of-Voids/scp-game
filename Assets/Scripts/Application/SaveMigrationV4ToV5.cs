using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    /// <summary>
    /// 中文：把 v4 存档升级为 v5 任命交接结构。旧档默认已完成交接，避免既有继续游戏流程被一次性新手页面阻断；缺失的摘要元数据使用不冒充正式设定的内部开发文本。
    /// English: Upgrades v4 saves to the v5 appointment-briefing shape. Legacy saves default to acknowledged so existing continue flows are not blocked by a one-time onboarding page; missing metadata uses internal-development text that does not claim official lore.
    /// </summary>
    public sealed class SaveMigrationV4ToV5 : ISaveMigration
    {
        /// <summary>中文：本迁移接受的唯一输入版本。English: The only input version accepted by this migration.</summary>
        public int FromVersion => 4;

        /// <summary>中文：本迁移产出的唯一目标版本。English: The only target version produced by this migration.</summary>
        public int ToVersion => 5;

        /// <summary>
        /// 中文：原位补齐 v5 字段并返回同一 JSON 对象；输入必须已是 v4，字段已有值时不覆盖，以保持迁移确定性和玩家数据。
        /// English: Adds v5 fields in place and returns the same JSON object; the input must already be v4, and existing values are preserved for deterministic migration and player-data safety.
        /// </summary>
        /// <param name="save">中文：已迁移到 v4 的存档 JSON。English: Save JSON already migrated to v4.</param>
        /// <returns>中文：schemaVersion 为 5 的存档 JSON。English: Save JSON with schemaVersion 5.</returns>
        public JObject Migrate(JObject save)
        {
            save["briefingAcknowledged"] ??= true;
            save["briefing"] ??= new JObject
            {
                ["seatDesignation"] = "O5-UNRECORDED",
                ["predecessorDepartureCategory"] = "历史存档：交接记录未收录",
                ["foundationStatusSummary"] = "内部开发构建 / 三设施演示世界，非正式基准局完整规模。",
                ["priorityBriefs"] = new JArray(
                    "BRIEF-LEGACY-01｜核对三设施演示世界的运行状态。",
                    "BRIEF-LEGACY-02｜复核资源、收容与帷幕摘要。",
                    "BRIEF-LEGACY-03｜确认未结事项后进入总览。"),
                ["predecessorLegacy"] = "旧存档未记录任命交接元数据；继续使用当前持久化世界状态。"
            };
            save["schemaVersion"] = ToVersion;

            var world = save["world"] as JObject ?? new JObject();
            save["world"] = world;
            var worldVersion = world.Property("schemaVersion", System.StringComparison.OrdinalIgnoreCase);
            if (worldVersion == null)
            {
                world["SchemaVersion"] = ToVersion;
            }
            else
            {
                worldVersion.Value = ToVersion;
            }

            return save;
        }
    }
}
