using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    public sealed class SaveMigrationV1ToV2 : ISaveMigration
    {
        public int FromVersion => 1;

        public JObject Migrate(JObject save)
        {
            if (save["parentSaveId"] == null)
            {
                save["parentSaveId"] = JValue.CreateNull();
            }

            if (save["commandLog"] == null)
            {
                save["commandLog"] = new JArray();
            }

            save["schemaVersion"] = 2;
            return save;
        }
    }
}
