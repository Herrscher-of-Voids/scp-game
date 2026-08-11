using System;

using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    /// <summary>
    /// 中文：把 v3 存档补齐为 v4 元数据结构。旧格式未记录的玩家选择使用 Unknown、空文本和 Unix 纪元，明确表示资料缺失。
    /// English: Extends v3 saves to the v4 metadata shape. Player choices absent from the old format use Unknown, empty text and the Unix epoch to explicitly represent missing data.
    /// </summary>
    public sealed class SaveMigrationV3ToV4 : ISaveMigration
    {
        public int FromVersion => 3;

        public int ToVersion => 4;

        public JObject Migrate(JObject save)
        {
            save["saveId"] ??= string.Empty;
            save["displayName"] ??= string.Empty;
            save["identity"] ??= "Unknown";
            save["difficulty"] ??= "Unknown";
            save["seed"] ??= string.Empty;
            save["createdAtUtc"] ??= DateTime.UnixEpoch;
            save["savedAtUtc"] ??= DateTime.UnixEpoch;
            save["saveKind"] ??= "Unknown";
            save["gameVersion"] ??= string.Empty;
            save["schemaVersion"] = ToVersion;

            var world = save["world"] as JObject ?? new JObject();
            save["world"] = world;
            var worldVersion = world.Property("schemaVersion", StringComparison.OrdinalIgnoreCase);
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
