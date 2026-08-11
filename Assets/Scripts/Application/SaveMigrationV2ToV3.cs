using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    public sealed class SaveMigrationV2ToV3 : ISaveMigration
    {
        public int FromVersion => 2;

        public int ToVersion => 3;

        public JObject Migrate(JObject save)
        {
            var world = save["world"] as JObject ?? new JObject();
            save["world"] = world;
            var worldVersion = world.Property("schemaVersion", System.StringComparison.OrdinalIgnoreCase);
            if (worldVersion == null)
            {
                world["SchemaVersion"] = 3;
            }
            else
            {
                worldVersion.Value = 3;
            }
            world["economy"] ??= JObject.FromObject(new { });
            world["failure"] ??= JObject.FromObject(new { });
            var facts = save["worldFacts"] as JObject ?? new JObject();
            save["worldFacts"] = facts;
            facts["worldWasRestarted"] ??= false;
            facts["councilLegacyKeys"] ??= new JArray();
            facts["personnelTerminated"] ??= 0;
            facts["privilegeUses"] ??= 0;
            facts["alphaOneDeployments"] ??= 0;
            facts["overseerCyclesServed"] ??= 0;
            world["facts"] = facts.DeepClone();
            save["schemaVersion"] = ToVersion;
            return save;
        }
    }
}
