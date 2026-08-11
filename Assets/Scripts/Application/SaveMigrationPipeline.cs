using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    public sealed class SaveMigrationPipeline
    {
        private readonly Dictionary<int, ISaveMigration> _migrations;

        public SaveMigrationPipeline(IEnumerable<ISaveMigration> migrations)
        {
            _migrations = new Dictionary<int, ISaveMigration>();
            foreach (var migration in migrations)
            {
                _migrations.Add(migration.FromVersion, migration);
            }
        }

        public JObject Migrate(JObject save, int targetVersion)
        {
            var version = save.Value<int?>("schemaVersion") ?? 1;
            while (version < targetVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                {
                    throw new InvalidOperationException($"Missing save migration from version {version}.");
                }

                save = migration.Migrate(save);
                version = save.Value<int>("schemaVersion");
            }

            if (version != targetVersion)
            {
                throw new InvalidOperationException($"Unsupported save version {version}.");
            }

            return save;
        }
    }
}
