using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    /// <summary>
    /// 中文：把 v5 存档升级为 v6 会话闭环结构；缺失字段使用空集合和明确的 Unknown/None 默认值，保证迁移不伪造历史。
    /// English: Upgrades v5 saves to the v6 session-loop shape; missing fields use empty collections and explicit Unknown/None defaults so migration never fabricates history.
    /// </summary>
    public sealed class SaveMigrationV5ToV6 : ISaveMigration
    {
        public int FromVersion => 5;

        public int ToVersion => 6;

        /// <summary>
        /// 中文：原位补齐 v6 字段并同步世界 schema；输入为已迁移到 v5 的 JSON，已有值永不覆盖。
        /// English: Adds v6 fields in place and synchronizes world schema; input is v5 JSON and existing values are never overwritten.
        /// </summary>
        public JObject Migrate(JObject save)
        {
            save["pendingCommands"] ??= new JArray();
            save["checkpoint"] ??= new JObject
            {
                ["reason"] = "None",
                ["sequence"] = 0
            };
            save["epilogue"] ??= new JObject
            {
                ["isAvailable"] = false,
                ["sections"] = new JArray()
            };
            save["schemaVersion"] = ToVersion;
            var world = save["world"] as JObject ?? new JObject();
            save["world"] = world;
            world["schemaVersion"] = ToVersion;
            return save;
        }
    }
}
