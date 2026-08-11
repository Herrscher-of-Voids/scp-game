using System;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：提供引擎无关的 JSON 编解码和版本迁移；文件布局与原子提交由 SaveRepository 负责。
    /// English: Provides engine-independent JSON encoding and migration; SaveRepository owns layout and atomic commits.
    /// </summary>
    public sealed class SaveService
    {
        /// <summary>中文：当前持久化格式版本；v7 增加独立受限应急储备并移除月度储备支出。English: Current persistence format; v7 adds an independent restricted emergency reserve and removes monthly reserve spending.</summary>
        public const int CurrentSchemaVersion = 7;
        public const int CurrentVersion = CurrentSchemaVersion;
        private readonly JsonSerializerSettings _settings;
        private readonly SaveMigrationPipeline _migrations;

        public SaveService()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            _settings.Converters.Add(new StringEnumConverter());
            _migrations = new SaveMigrationPipeline(new ISaveMigration[]
            {
                new SaveMigrationV1ToV2(),
                new SaveMigrationV2ToV3(),
                new SaveMigrationV3ToV4(),
                new SaveMigrationV4ToV5(),
                new SaveMigrationV5ToV6(),
                new SaveMigrationV6ToV7()
            });
        }

        public string Serialize(SaveFile save)
        {
            save.SchemaVersion = CurrentVersion;
            save.World.SchemaVersion = CurrentVersion;
            save.WorldFacts = save.World.Facts;
            return JsonConvert.SerializeObject(save, _settings);
        }

        public SaveFile Deserialize(string json)
        {
            var root = JObject.Parse(json);
            // 中文：迁移前只读取旧 JSON 的字段存在性。仅当存档没有第一版新增的财政字段时，才允许精确识别旧演示基线；这避免把新格式中恰好拥有 8,000,000 现金的玩家自定义世界误判为演示档。金额单位为整数货币，判定不依赖本地化文本或现实时间，因此结果确定。
            // English: Before schema migration, inspect only field presence in the original JSON. Exact legacy-demo recognition is allowed only when first-version finance fields are absent, preventing a new-format custom world that happens to hold 8,000,000 cash from being mistaken for a demo save. Amounts are integer currency and the test is independent of localized text or wall time, so it is deterministic.
            JToken? originalWorld = Child(root, "world");
            JToken? originalEconomy = Child(originalWorld, "economy");
            bool legacyFinanceShape = Child(originalEconomy, "totalAssets") == null && Child(originalEconomy, "fundingChannels") == null;
            long originalFunds = Child(originalWorld, "funds")?.Value<long>() ?? long.MinValue;
            long? originalBudgetTotal = TryReadLegacyPrimaryBudgetTotal(Child(originalEconomy, "budget"));
            root = _migrations.Migrate(root, CurrentVersion);
            var serializer = JsonSerializer.Create(_settings);
            var save = root.ToObject<SaveFile>(serializer) ??
                throw new JsonSerializationException("Save file is empty.");
            save.World.SchemaVersion = CurrentVersion;
            save.World.Facts = save.WorldFacts;
            ApplyLegacyFinanceBaseline(save.World, legacyFinanceShape, originalFunds, originalBudgetTotal);
            return save;
        }

        /// <summary>
        /// 中文：仅迁移可确认的旧演示财政状态。现金必须精确等于 8,000,000；预算若存在，也必须精确等于旧十项合计 900,000 才替换。其他现金和任何已修改预算原样保留；旧存档不会注入演示事故，防止污染玩家真实世界。
        /// English: Migrates only a confirmed legacy demo finance state. Cash must equal exactly 8,000,000, and an existing budget is replaced only when its ten-category total equals the legacy 900,000 signature. All other cash and any modified budget remain untouched; legacy saves never receive a demo incident, avoiding contamination of real player worlds.
        /// </summary>
        private static void ApplyLegacyFinanceBaseline(WorldState world, bool legacyFinanceShape, long originalFunds, long? originalBudgetTotal)
        {
            if (!legacyFinanceShape) return;
            world.Economy.EnsureFinanceDefaults();
            if (originalFunds == EconomyRules.LegacyDemoStartingAvailableCash)
                world.Funds = EconomyRules.TemporaryStartingAvailableCash;
            if (originalBudgetTotal == EconomyRules.LegacyDemoPrimaryBudgetTotal)
                world.Economy.Budget = EconomyRules.CreateTemporaryPrimaryBudget();
        }

        /// <summary>
        /// 中文：从原始旧 JSON 读取十个一级预算的整数货币合计；字段缺失返回 null，溢出或非整数由 JSON 转换异常明确拒绝，不猜测二级明细。English: Reads the ten primary integer-currency budget values from the original legacy JSON; missing data returns null, overflow or non-integers fail through JSON conversion, and secondary detail is never guessed.
        /// </summary>
        private static long? TryReadLegacyPrimaryBudgetTotal(JToken? budget)
        {
            if (budget == null || budget.Type == JTokenType.Null) return null;
            string[] names = { "siteOperations", "containmentMaintenance", "research", "security", "mobileTaskForces", "alphaOne", "veilAndCover", "administrationAndIntelligence", "personnelAndEthics", "emergencyReserve" };
            long total = 0;
            foreach (string name in names)
            {
                JToken? value = Child(budget, name);
                if (value == null) return null;
                total = checked(total + value.Value<long>());
            }
            return total;
        }

        /// <summary>中文：以不区分大小写方式读取 JObject 子字段，兼容 Newtonsoft 默认 PascalCase 与旧手写 camelCase 存档；非对象或缺失字段返回 null。English: Reads a JObject child case-insensitively to support Newtonsoft PascalCase and legacy hand-written camelCase saves; non-objects and missing fields return null.</summary>
        private static JToken? Child(JToken? token, string name)
        {
            return token is JObject item && item.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken? value) ? value : null;
        }

        public void Save(string path, SaveFile save)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize(save));
        }

        public SaveFile Load(string path)
        {
            return Deserialize(File.ReadAllText(path));
        }
    }
}
