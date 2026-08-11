using System;
using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    /// <summary>中文：把 v6 月度储备科目确定性迁移为 v7 独立受限余额，并清除正式预算与草案中的旧科目。English: Deterministically migrates the v6 monthly reserve category into the v7 independent restricted balance and clears it from enacted and draft budgets.</summary>
    public sealed class SaveMigrationV6ToV7 : ISaveMigration
    {
        public int FromVersion => 6;
        public int ToVersion => 7;

        /// <summary>中文：已有独立字段（包括显式零）原样保留；缺失时，精确临时基线迁为三个月必要支出 3120 亿元，否则采用旧正式预算储备值（缺失按零），不增加总资产。English: Preserves an existing independent field including explicit zero; when absent, the exact provisional baseline becomes three necessary months (312 billion), otherwise the legacy enacted reserve value is used (missing means zero), without increasing total assets.</summary>
        public JObject Migrate(JObject save)
        {
            JObject world = ChildObject(save, "world") ?? new JObject();
            save["world"] = world;
            JObject economy = ChildObject(world, "economy") ?? new JObject();
            world["economy"] = economy;
            JObject? budget = ChildObject(economy, "budget");
            if (Property(economy, "emergencyReserveBalance") == null)
            {
                long legacyReserve = ChildValue(budget, "emergencyReserve") ?? 0;
                economy["emergencyReserveBalance"] = IsExactTemporaryBaseline(budget, legacyReserve) ? 312_000_000_000L : legacyReserve;
            }
            RemoveLegacyReserve(budget);
            RemoveLegacyReserve(ChildObject(economy, "budgetDraft"));
            save["schemaVersion"] = ToVersion;
            world["schemaVersion"] = ToVersion;
            return save;
        }

        private static bool IsExactTemporaryBaseline(JObject? budget, long reserve) => budget != null && reserve == 20_000_000_000L &&
            ChildValue(budget,"siteOperations")==18_000_000_000L && ChildValue(budget,"containmentMaintenance")==21_000_000_000L && ChildValue(budget,"research")==14_000_000_000L &&
            ChildValue(budget,"security")==12_000_000_000L && ChildValue(budget,"mobileTaskForces")==16_000_000_000L && ChildValue(budget,"alphaOne")==10_000_000_000L &&
            ChildValue(budget,"veilAndCover")==11_000_000_000L && ChildValue(budget,"administrationAndIntelligence")==7_000_000_000L && ChildValue(budget,"personnelAndEthics")==9_000_000_000L;

        private static void RemoveLegacyReserve(JObject? budget) { Property(budget,"emergencyReserve")?.Remove(); }
        private static long? ChildValue(JObject? item,string name) => Property(item,name)?.Value.Value<long>();
        private static JObject? ChildObject(JObject item,string name) => Property(item,name)?.Value as JObject;
        private static JProperty? Property(JObject? item,string name) => item?.Property(name,StringComparison.OrdinalIgnoreCase);
    }
}
