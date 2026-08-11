using System;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;
using NUnit.Framework;

using Scp.Application;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application.Tests
{
    public sealed class ApplicationTests
    {
        [Test]
        public void SaveService_JsonRoundTrip_PreservesIdsAndRandomState()
        {
            var service = new SaveService();
            var save = new SaveFile
            {
                SaveId = "round-trip",
                DisplayName = "测试存档",
                Identity = IdentityRole.Overseer,
                Difficulty = GameDifficulty.Hard,
                Seed = "FIXED-SEED",
                CreatedAtUtc = new DateTime(2026, 8, 8, 1, 2, 3, DateTimeKind.Utc),
                SavedAtUtc = new DateTime(2026, 8, 8, 2, 3, 4, DateTimeKind.Utc),
                SaveKind = SaveKind.Manual,
                GameVersion = "0.1.0-alpha",
                Mode = SaveMode.Chained,
                ParentSaveId = "parent",
                BriefingAcknowledged = true,
                Briefing = new OverseerBriefingMetadata
                {
                    SeatDesignation = "O5-4",
                    PredecessorDepartureCategory = "前任监督者因机密原因离席。",
                    FoundationStatusSummary = "内部开发构建。",
                    PriorityBriefs = new[] { "一", "二", "三" },
                    PredecessorLegacy = "遗留事项。"
                },
                World = CreateWorld(),
                WorldFacts = new WorldFacts { KnownFactKeys = new[] { "fact" } },
                CommandLog = new[]
                {
                    new CommandLogEntry
                    {
                        Kind = CommandKinds.AdjustFunds,
                        SubmittedAtTick = 3,
                        Amount = 25,
                        RequiredClearance = ClearanceLevel.Level4
                    }
                }
            };

            var json = service.Serialize(save);
            var loaded = service.Deserialize(json);

            Assert.That(json, Does.Not.Contain("$type"));
            Assert.That(loaded.World.Anomalies[0].Definition.Id, Is.EqualTo(new ScpId(1)));
            Assert.That(loaded.World.Sites[0].Id, Is.EqualTo(new SiteId(1)));
            Assert.That(loaded.World.Random.State0, Is.EqualTo(save.World.Random.State0));
            Assert.That(loaded.World.Random.State1, Is.EqualTo(save.World.Random.State1));
            Assert.That(loaded.CommandLog.Single().Kind, Is.EqualTo(CommandKinds.AdjustFunds));
            Assert.That(loaded.SaveId, Is.EqualTo(save.SaveId));
            Assert.That(loaded.DisplayName, Is.EqualTo(save.DisplayName));
            Assert.That(loaded.Difficulty, Is.EqualTo(GameDifficulty.Hard));
            Assert.That(loaded.SaveKind, Is.EqualTo(SaveKind.Manual));
            Assert.That(loaded.SchemaVersion, Is.EqualTo(7));
            Assert.That(loaded.BriefingAcknowledged, Is.True);
            Assert.That(loaded.Briefing.SeatDesignation, Is.EqualTo("O5-4"));
            Assert.That(loaded.Briefing.PriorityBriefs, Is.EqualTo(new[] { "一", "二", "三" }));
        }

        [Test]
        public void SaveMigrationV4ToV5_LegacySaveDefaultsBriefingToAcknowledged()
        {
            var source = JObject.Parse("{\"schemaVersion\":4,\"mode\":\"Standalone\",\"world\":{\"schemaVersion\":4},\"worldFacts\":{},\"commandLog\":[]}");
            var migrated = new SaveMigrationV4ToV5().Migrate(source);

            Assert.That(migrated.Value<int>("schemaVersion"), Is.EqualTo(5));
            Assert.That(migrated.Value<bool>("briefingAcknowledged"), Is.True);
            Assert.That(((JArray)migrated["briefing"]!["priorityBriefs"]!).Count, Is.EqualTo(3));
        }

        [Test]
        public void SaveMigrationV3ToV4_MissingMetadataUsesExplicitUnknownDefaults()
        {
            var source = JObject.Parse("{\"schemaVersion\":3,\"mode\":\"Standalone\",\"world\":{\"schemaVersion\":3},\"worldFacts\":{},\"commandLog\":[]}");
            var migrated = new SaveMigrationV3ToV4().Migrate(source);

            Assert.That(migrated.Value<int>("schemaVersion"), Is.EqualTo(4));
            Assert.That(migrated.Value<string>("identity"), Is.EqualTo("Unknown"));
            Assert.That(migrated.Value<string>("difficulty"), Is.EqualTo("Unknown"));
            Assert.That(migrated.Value<string>("saveKind"), Is.EqualTo("Unknown"));
            Assert.That(migrated.Value<string>("seed"), Is.Empty);
        }

        [Test]
        public void SaveRepository_AtomicCommitUpdatesIndexAndKeepsPreviousBackup()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile first = CreateRepositorySave("atomic");
                first.World.Funds = 100;
                repository.Save(first);
                first.World.Funds = 200;
                repository.Save(first);

                Assert.That(File.Exists(Path.Combine(directory, "atomic", "main.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "atomic", "main.bak")), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(directory, "index.json")), Does.Contain("atomic"));
                Assert.That(repository.Load("atomic", false).World.Funds, Is.EqualTo(200));
                Assert.That(repository.Load("atomic", true).World.Funds, Is.EqualTo(100));
            });
        }

        [Test]
        public void SaveRepository_CorruptPrimaryReportsBackupWithoutSilentLoad()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateRepositorySave("backup");
                repository.Save(save);
                save.World.Funds++;
                repository.Save(save);
                File.WriteAllText(Path.Combine(directory, "backup", "main.json"), "not json");

                SaveProbeResult result = repository.ProbeLatest();

                Assert.That(result.Status, Is.EqualTo(SaveProbeStatus.BackupAvailable));
                Assert.That(result.Path, Does.EndWith("main.bak"));
            });
        }

        [Test]
        public void SaveRepository_FutureVersionIsIncompatibleAndFileIsUntouched()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                repository.Save(CreateRepositorySave("future"));
                string path = Path.Combine(directory, "future", "main.json");
                string futureJson = File.ReadAllText(path).Replace("\"schemaVersion\": 7", "\"schemaVersion\": 99");
                File.WriteAllText(path, futureJson);

                SaveProbeResult result = repository.ProbeLatest();

                Assert.That(result.Status, Is.EqualTo(SaveProbeStatus.IncompatibleVersion));
                Assert.That(File.ReadAllText(path), Is.EqualTo(futureJson));
            });
        }

        [Test]
        public void SaveRepository_EndedSaveCannotContinue()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateRepositorySave("ended");
                save.World.Failure.IsEnded = true;
                repository.Save(save);

                Assert.That(repository.ProbeLatest().Status, Is.EqualTo(SaveProbeStatus.Ended));
            });
        }

        [Test]
        public void SaveRepository_DuplicateProbeDetectsTrimmedCaseInsensitiveName()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile existing = CreateRepositorySave("same-name");
                existing.DisplayName = "  Alpha  ";
                existing.Seed = "FIRST";
                repository.Save(existing);
                SaveFile candidate = CreateRepositorySave("candidate");
                candidate.DisplayName = "alpha";
                candidate.Seed = "SECOND";

                DuplicateSaveProbeResult result = repository.ProbeDuplicates(candidate);

                Assert.That(result.Match, Is.EqualTo(DuplicateSaveMatch.SameName));
                Assert.That(result.SkippedSaveCount, Is.Zero);
            });
        }

        [Test]
        public void SaveRepository_DuplicateProbeDetectsIdenticalConfiguration()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile existing = CreateRepositorySave("identical");
                existing.DisplayName = "Alpha";
                repository.Save(existing);
                SaveFile candidate = CreateRepositorySave("candidate");
                candidate.DisplayName = " alpha ";

                DuplicateSaveProbeResult result = repository.ProbeDuplicates(candidate);

                Assert.That(result.Match.HasFlag(DuplicateSaveMatch.SameName), Is.True);
                Assert.That(result.Match.HasFlag(DuplicateSaveMatch.IdenticalConfiguration), Is.True);
            });
        }

        [Test]
        public void SaveRepository_DuplicateProbeIgnoresDifferentConfiguration()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                repository.Save(CreateRepositorySave("existing"));
                SaveFile candidate = CreateRepositorySave("candidate");
                candidate.DisplayName = "Different";
                candidate.Seed = "OTHER";

                Assert.That(repository.ProbeDuplicates(candidate).Match, Is.EqualTo(DuplicateSaveMatch.None));
            });
        }

        [Test]
        public void SaveRepository_DuplicateProbeSkipsCorruptSave()
        {
            WithTemporaryDirectory(directory =>
            {
                string corruptDirectory = Path.Combine(directory, "corrupt");
                Directory.CreateDirectory(corruptDirectory);
                File.WriteAllText(Path.Combine(corruptDirectory, "main.json"), "not json");
                var repository = new SaveRepository(directory);

                DuplicateSaveProbeResult result = repository.ProbeDuplicates(CreateRepositorySave("candidate"));

                Assert.That(result.Match, Is.EqualTo(DuplicateSaveMatch.None));
                Assert.That(result.SkippedSaveCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void SaveRepository_BriefingAcknowledgementPersistsAfterAtomicSave()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateRepositorySave("briefing");
                Assert.That(save.BriefingAcknowledged, Is.False);
                repository.Save(save);
                save.BriefingAcknowledged = true;
                repository.Save(save);

                Assert.That(repository.Load("briefing", false).BriefingAcknowledged, Is.True);
            });
        }

        [Test]
        public void GameSession_RestorePreservesModeParentAndCommandLog()
        {
            SaveFile save = CreateRepositorySave("restore");
            save.Mode = SaveMode.Chained;
            save.ParentSaveId = "parent";
            save.CommandLog = new[] { new CommandLogEntry { Kind = CommandKinds.AdjustFunds, Amount = 17 } };

            GameSession session = GameSession.Restore(save, new OverseerPerspective());

            Assert.That(session.World, Is.SameAs(save.World));
            Assert.That(session.Mode, Is.EqualTo(SaveMode.Chained));
            Assert.That(session.ParentSaveId, Is.EqualTo("parent"));
            Assert.That(session.CommandLog.Single().Amount, Is.EqualTo(17));
        }

        [Test]
        public void SaveMigrationV1ToV2_MissingFields_AddsRequiredNodes()
        {
            var source = JObject.Parse("{\"schemaVersion\":1,\"mode\":\"Standalone\",\"world\":{},\"worldFacts\":{}}");
            var pipeline = new SaveMigrationPipeline(new ISaveMigration[] { new SaveMigrationV1ToV2() });

            var migrated = pipeline.Migrate(source, 2);

            Assert.That(migrated.Value<int>("schemaVersion"), Is.EqualTo(2));
            Assert.That(migrated.ContainsKey("parentSaveId"), Is.True);
            Assert.That(migrated["commandLog"], Is.TypeOf<JArray>());
        }

        [Test]
        public void Project_InfoAntimemetic_RemovesRecordButKeepsFunds()
        {
            var world = CreateWorld();
            world.Anomalies = new[]
            {
                world.Anomalies[0],
                new AnomalyInstance
                {
                    SiteId = new SiteId(1),
                    Definition = new ScpDefinition
                    {
                        Id = new ScpId(2),
                        Class = ObjectClass.Keter,
                        Traits = new[]
                        {
                            new TraitInstance { Trait = ScpTrait.InfoAntimemetic }
                        }
                    }
                }
            };
            var perspective = new BasicPerspective(IdentityRole.Overseer, ClearanceLevel.Level5);

            var view = perspective.Project<WorldViewModel>(world);

            Assert.That(view.Anomalies.Select(item => item.Id), Is.EqualTo(new[] { new ScpId(1) }));
            Assert.That(view.Funds, Is.EqualTo(world.Funds));
        }

        [Test]
        public void SaveRepository_EnumeratesIndependentPrimaryBackupStatesAndSkipsInvalidDirectories()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile normal = CreateRepositorySave("normal");
                normal.DisplayName = "Normal";
                normal.CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                normal.SavedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
                repository.Save(normal);
                repository.Save(normal);
                File.WriteAllText(Path.Combine(directory, "normal", "main.json"), "bad");
                SaveFile ended = CreateRepositorySave("ended-entry");
                ended.World.Failure.IsEnded = true;
                repository.Save(ended);
                SaveFile future = CreateRepositorySave("future-entry");
                repository.Save(future);
                string futurePath = Path.Combine(directory, "future-entry", "main.json");
                File.WriteAllText(futurePath, File.ReadAllText(futurePath).Replace("\"schemaVersion\": " + SaveService.CurrentSchemaVersion, "\"schemaVersion\": 99"));
                Directory.CreateDirectory(Path.Combine(directory, "bad..name"));
                Directory.CreateDirectory(Path.Combine(directory, "ignored.deleting-abc"));
                File.WriteAllText(Path.Combine(directory, "ignored.deleting-abc", "main.json"), "bad");

                SaveDirectoryEntry[] entries = repository.EnumerateDirectory();

                Assert.That(entries.Select(entry => entry.SaveId), Is.EqualTo(new[] { "ended-entry", "future-entry", "normal" }));
                Assert.That(entries.Single(entry => entry.SaveId == "normal").PrimaryState, Is.EqualTo(SaveFileState.InvalidOrCorrupt));
                Assert.That(entries.Single(entry => entry.SaveId == "normal").BackupState, Is.EqualTo(SaveFileState.Available));
                Assert.That(entries.Single(entry => entry.SaveId == "ended-entry").PrimaryState, Is.EqualTo(SaveFileState.Ended));
                Assert.That(entries.Single(entry => entry.SaveId == "future-entry").PrimaryState, Is.EqualTo(SaveFileState.IncompatibleVersion));
            });
        }

        /// <summary>
        /// 中文：验证玩家明确恢复损坏主档的合规备份时，只更新最近索引，主档与备份的内容、文件时间和备份可用状态保持不变。
        /// English: Verifies that explicit recovery of a compliant backup for a corrupt primary updates only the latest index while preserving both save files, timestamps and backup availability.
        /// </summary>
        [Test]
        public void SaveRepository_BackupRecoveryBecomesLatestAndProbeRemainsBackupAvailable()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateRepositorySave("recoverable");
                save.World.Funds = 100;
                repository.Save(save);
                save.World.Funds = 200;
                repository.Save(save);
                string mainPath = Path.Combine(directory, "recoverable", "main.json");
                string backupPath = Path.Combine(directory, "recoverable", "main.bak");
                File.WriteAllText(mainPath, "not json");
                string mainBefore = File.ReadAllText(mainPath);
                string backupBefore = File.ReadAllText(backupPath);
                DateTime mainTimeBefore = File.GetLastWriteTimeUtc(mainPath);
                DateTime backupTimeBefore = File.GetLastWriteTimeUtc(backupPath);

                SaveDirectoryOperationResult result = repository.SetLatest("recoverable", true);
                SaveProbeResult probe = repository.ProbeLatest();

                Assert.That(result.Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(probe.Status, Is.EqualTo(SaveProbeStatus.BackupAvailable));
                Assert.That(probe.SaveId, Is.EqualTo("recoverable"));
                Assert.That(File.ReadAllText(mainPath), Is.EqualTo(mainBefore));
                Assert.That(File.ReadAllText(backupPath), Is.EqualTo(backupBefore));
                Assert.That(File.GetLastWriteTimeUtc(mainPath), Is.EqualTo(mainTimeBefore));
                Assert.That(File.GetLastWriteTimeUtc(backupPath), Is.EqualTo(backupTimeBefore));
            });
        }

        /// <summary>
        /// 中文：验证版本过新的主档具有最高拒绝优先级，即使同目录备份有效也不能更新最近索引。
        /// English: Verifies that a future-version primary has refusal priority and cannot update the latest index even when its backup is valid.
        /// </summary>
        [Test]
        public void SaveRepository_BackupRecoveryRejectsFuturePrimaryEvenWhenBackupIsValid()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile save = CreateRepositorySave("future-primary");
                repository.Save(save);
                save.World.Funds = 200;
                repository.Save(save);
                string mainPath = Path.Combine(directory, "future-primary", "main.json");
                string futureJson = File.ReadAllText(mainPath).Replace("\"schemaVersion\": " + SaveService.CurrentSchemaVersion, "\"schemaVersion\": 99");
                File.WriteAllText(mainPath, futureJson);
                string indexBefore = File.ReadAllText(Path.Combine(directory, "index.json"));

                SaveDirectoryOperationResult result = repository.SetLatest("future-primary", true);

                Assert.That(result.Status, Is.Not.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(File.ReadAllText(Path.Combine(directory, "index.json")), Is.EqualTo(indexBefore));
                Assert.That(repository.ProbeLatest().Status, Is.EqualTo(SaveProbeStatus.IncompatibleVersion));
            });
        }

        [Test]
        public void SaveRepository_SetLatestDoesNotChangeSaveContentsOrTimestamps()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile first = CreateRepositorySave("first");
                repository.Save(first);
                first.World.Funds = 600;
                repository.Save(first);
                string mainPath = Path.Combine(directory, "first", "main.json");
                string backupPath = Path.Combine(directory, "first", "main.bak");
                string mainBefore = File.ReadAllText(mainPath);
                string backupBefore = File.ReadAllText(backupPath);
                DateTime mainTimeBefore = File.GetLastWriteTimeUtc(mainPath);
                DateTime backupTimeBefore = File.GetLastWriteTimeUtc(backupPath);

                Assert.That(repository.SetLatest("first").Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(File.ReadAllText(mainPath), Is.EqualTo(mainBefore));
                Assert.That(File.ReadAllText(backupPath), Is.EqualTo(backupBefore));
                Assert.That(File.GetLastWriteTimeUtc(mainPath), Is.EqualTo(mainTimeBefore));
                Assert.That(File.GetLastWriteTimeUtc(backupPath), Is.EqualTo(backupTimeBefore));
            });
        }

        [Test]
        public void SaveRepository_SetLatestDoesNotChangeSavedAtUtcAndDeleteRepairsIndex()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                SaveFile first = CreateRepositorySave("first");
                first.SavedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                repository.Save(first);
                DateTime savedAt = repository.Load("first", false).SavedAtUtc;
                SaveFile second = CreateRepositorySave("second");
                second.SavedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
                repository.Save(second);
                Assert.That(repository.SetLatest("first").Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(repository.Load("first", false).SavedAtUtc, Is.EqualTo(savedAt));
                Assert.That(repository.DeleteSave("first").Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(repository.ProbeLatest().SaveId, Is.EqualTo("second"));
                Assert.That(repository.DeleteSave("second").Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(repository.ProbeLatest().Status, Is.EqualTo(SaveProbeStatus.NoSave));
            });
        }

        [Test]
        public void SaveRepository_DeleteAllowsCorruptSaveAndRejectsPathTraversal()
        {
            WithTemporaryDirectory(directory =>
            {
                var repository = new SaveRepository(directory);
                Directory.CreateDirectory(Path.Combine(directory, "corrupt"));
                File.WriteAllText(Path.Combine(directory, "corrupt", "main.json"), "bad");
                Assert.That(repository.DeleteSave("corrupt").Status, Is.EqualTo(SaveDirectoryOperationStatus.Succeeded));
                Assert.That(repository.DeleteSave("..\\outside").Status, Is.EqualTo(SaveDirectoryOperationStatus.InvalidSaveId));
                Assert.That(repository.DeleteSave("../outside").Status, Is.EqualTo(SaveDirectoryOperationStatus.InvalidSaveId));
            });
        }

        private static SaveFile CreateRepositorySave(string saveId)
        {
            return new SaveFile
            {
                SaveId = saveId,
                DisplayName = saveId,
                Identity = IdentityRole.Overseer,
                Difficulty = GameDifficulty.Normal,
                Seed = "TEST-SEED",
                SaveKind = SaveKind.Manual,
                GameVersion = "test",
                Mode = SaveMode.Standalone,
                BriefingAcknowledged = false,
                World = CreateWorld()
            };
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "scp-save-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                action(directory);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static WorldState CreateWorld()
        {
            return new WorldState
            {
                Tick = 3,
                Funds = 5000,
                Random = new DeterministicRandom(21),
                Sites = new[]
                {
                    new SiteState { Id = new SiteId(1), Continent = Continent.Europe }
                },
                Anomalies = new[]
                {
                    new AnomalyInstance
                    {
                        SiteId = new SiteId(1),
                        Definition = new ScpDefinition
                        {
                            Id = new ScpId(1),
                            Class = ObjectClass.Safe
                        }
                    }
                }
            };
        }
    }
}
