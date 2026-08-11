using System;
using System.Collections.Generic;
using System.Linq;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：应用层会话持有完整创建元数据、确定性世界快照、历史日志和当前待执行命令，并统一管理检查点。
    /// English: The application session owns complete creation metadata, the deterministic world snapshot, history, pending commands, and checkpoints.
    /// </summary>
    public sealed class GameSession
    {
        private readonly List<ICommand> _pendingCommands = new List<ICommand>();
        private readonly List<CommandLogEntry> _commandLog = new List<CommandLogEntry>();
        private readonly SaveService _saveService;
        private readonly WorldSimulation _simulation;
        private long _checkpointSequence;

        public GameSession(WorldState world, IPerspective perspective, SaveMode mode = SaveMode.Standalone, string? parentSaveId = null, SaveService? saveService = null)
            : this(new SaveFile { World = world, WorldFacts = world.Facts, Identity = perspective.Role, Mode = mode, ParentSaveId = parentSaveId }, perspective, saveService)
        {
        }

        /// <summary>中文：从完整存档恢复；快照和随机流是真值，历史日志不重放，待执行命令按原参数恢复。English: Restores from a complete save; snapshot and random stream are authoritative, history is not replayed, and pending commands are restored from their original parameters.</summary>
        public static GameSession Restore(SaveFile save, IPerspective perspective, SaveService? saveService = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            return new GameSession(save, perspective, saveService);
        }

        private GameSession(SaveFile save, IPerspective perspective, SaveService? saveService)
        {
            World = save.World ?? throw new InvalidOperationException("Save world is required.");
            Perspective = perspective ?? throw new ArgumentNullException(nameof(perspective));
            SaveId = save.SaveId;
            DisplayName = save.DisplayName;
            Identity = save.Identity;
            Difficulty = save.Difficulty;
            Seed = save.Seed;
            CreatedAtUtc = save.CreatedAtUtc;
            GameVersion = save.GameVersion;
            SaveKind = save.SaveKind;
            Mode = save.Mode;
            ParentSaveId = save.ParentSaveId;
            BriefingAcknowledged = save.BriefingAcknowledged;
            Briefing = save.Briefing ?? new OverseerBriefingMetadata();
            WorldFacts = save.WorldFacts ?? World.Facts;
            World.Facts = WorldFacts;
            _commandLog.AddRange(save.CommandLog ?? Array.Empty<CommandLogEntry>());
            foreach (var entry in save.PendingCommands ?? Array.Empty<CommandLogEntry>()) _pendingCommands.Add(CommandLogCodec.Decode(entry));
            _checkpointSequence = save.Checkpoint?.CheckpointSequence ?? 0;
            _saveService = saveService ?? new SaveService();
            _simulation = new WorldSimulation(World, perspective.Clearance);
        }

        public WorldState World { get; }
        public IPerspective Perspective { get; }
        public string SaveId { get; }
        public string DisplayName { get; }
        public IdentityRole Identity { get; }
        public GameDifficulty Difficulty { get; }
        public string Seed { get; }
        public DateTime CreatedAtUtc { get; }
        public string GameVersion { get; }
        public SaveKind SaveKind { get; }
        public SaveMode Mode { get; }
        public string? ParentSaveId { get; }
        public bool BriefingAcknowledged { get; set; }
        public OverseerBriefingMetadata Briefing { get; }
        public WorldFacts WorldFacts { get; }
        public IReadOnlyList<CommandLogEntry> CommandLog => _commandLog;
        public bool IsEnded => World.Failure.IsEnded;

        /// <summary>中文：提交命令同时写入审计日志和待执行队列；终局后拒绝新命令。English: Submitting a command writes both the audit log and pending queue; ended sessions reject new commands.</summary>
        public void Submit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsEnded) throw new InvalidOperationException("The session has ended.");
            _pendingCommands.Add(command);
            _commandLog.Add(CommandLogCodec.Encode(command, World.Tick));
        }

        /// <summary>
        /// 中文：在写入命令日志前以当前 O5 可见世界执行严格验证；返回失败时队列、日志和世界均不改变，供条件解析与批量原子拒绝使用。
        /// English: Strictly validates against the current O5-visible world before logging; failure leaves queue, log, and world unchanged for condition parsing and atomic batch rejection.
        /// </summary>
        public ValidationResult TrySubmit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsEnded) return ValidationResult.Failure("The session has ended.");
            // 中文：仅针对帷幕事件命令比较同一事件、同一动作和当前世界 Tick；这是提交边界的输入去重，不改变模拟 Tick 或通用命令生命周期。
            // English: Only veil-incident commands compare incident, action, and current world Tick at the submission boundary; this input deduplication does not alter simulation ticks or the generic command lifecycle.
            // 中文：命令日志是持久化审计真值，因此页面重建、重复回调或存档恢复都不能绕过去重；不同动作和后续 Tick 保持合法。
            // English: The command log is the persisted audit source of truth, so page rebuilds, duplicate callbacks, and save restoration cannot bypass deduplication; other actions and later ticks remain valid.
            if (command is VeilIncidentActionCommand veilCommand && _commandLog.Exists(entry =>
                entry.Kind == CommandKinds.VeilIncidentAction &&
                entry.SubmittedAtTick == World.Tick &&
                entry.VeilIncidentId == veilCommand.IncidentId &&
                entry.VeilAction == veilCommand.Action))
                return ValidationResult.Failure("同一事件的相同帷幕动作已在当前 Tick 提交，重复操作已拒绝。");
            var validation = command.Validate(new WorldQuery(World, Perspective.Clearance));
            if (!validation.IsValid) return validation;
            Submit(command);
            return ValidationResult.Success();
        }

        /// <summary>
        /// 中文：立即应用仅改变财政草案编辑状态的命令，不推进 Tick、不触发月结算，但仍写命令历史并随存档持久化；调用方不得用于正式签发或其他业务结果。
        /// English: Immediately applies commands that only edit finance-draft state without advancing ticks or triggering monthly settlement, while still appending command history and persisting in saves; callers must not use it for enactment or other business outcomes.
        /// </summary>
        public ValidationResult TryApplyFinanceDraft(ICommand command)
        {
            if (command is not (SaveBudgetDraftCommand or DiscardBudgetDraftCommand or SetCompensationAmountCommand))
                return ValidationResult.Failure("Command is not an immediate finance draft edit.");
            if (IsEnded) return ValidationResult.Failure("The session has ended.");
            ValidationResult validation = command.Validate(new WorldQuery(World, Perspective.Clearance));
            if (!validation.IsValid) return validation;
            command.Apply(World, new EventBuffer());
            _commandLog.Add(CommandLogCodec.Encode(command, World.Tick));
            return ValidationResult.Success();
        }

        /// <summary>中文：推进后在月边界或终局自动生成检查点；其余推进保持纯确定性。English: Creates a checkpoint after month boundaries or termination while keeping other advancement purely deterministic.</summary>
        public TickResult Advance(int ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (IsEnded) throw new InvalidOperationException("The session has ended.");
            var events = new List<DomainEvent>();
            for (var index = 0; index < ticks; index++)
            {
                var result = _simulation.Tick(index == 0 ? _pendingCommands.ToArray() : Array.Empty<ICommand>());
                events.AddRange(result.Events);
                if (index == 0) _pendingCommands.Clear();
                if (IsEnded) break;
                if (World.Tick % WorldSimulation.MonthlyTicks == 0) CheckpointReason = CheckpointReason.MonthBoundary;
            }
            if (IsEnded) CheckpointReason = CheckpointReason.Terminal;
            return new TickResult(World, events);
        }

        public CheckpointReason CheckpointReason { get; private set; }

        /// <summary>中文：退出前强制建立退出检查点；调用方随后可安全关闭进程。English: Forces an exit checkpoint before shutdown so the caller can safely close the process.</summary>
        public void CheckpointOnExit(string path) => SaveCheckpoint(path, CheckpointReason.Exit);

        /// <summary>中文：重大命令提交后建立检查点；命令本身仍在下一次 Tick 执行。English: Creates a checkpoint after a major command is submitted; the command itself still executes on the next tick.</summary>
        public void CheckpointAfterMajorCommand(string path) => SaveCheckpoint(path, CheckpointReason.MajorCommand);

        public void Save(string path) => SaveCheckpoint(path, CheckpointReason.None);

        private void SaveCheckpoint(string path, CheckpointReason reason)
        {
            _checkpointSequence++;
            var save = CreateSave(reason);
            _saveService.Save(path, save);
        }

        public SaveFile CreateSave(CheckpointReason reason = CheckpointReason.None)
        {
            var ended = IsEnded;
            return new SaveFile
            {
                SaveId = SaveId,
                DisplayName = DisplayName,
                Identity = Identity,
                Difficulty = Difficulty,
                Seed = Seed,
                CreatedAtUtc = CreatedAtUtc,
                SaveKind = SaveKind,
                GameVersion = GameVersion,
                Mode = Mode,
                ParentSaveId = ParentSaveId,
                BriefingAcknowledged = BriefingAcknowledged,
                Briefing = Briefing,
                World = World,
                WorldFacts = World.Facts,
                CommandLog = _commandLog.ToArray(),
                PendingCommands = _pendingCommands.Select(command => CommandLogCodec.Encode(command, World.Tick)).ToArray(),
                Checkpoint = new SaveSessionMetadata { CheckpointReason = reason, CheckpointSequence = _checkpointSequence, CheckpointTick = World.Tick },
                Epilogue = ended ? new EpilogueService().CreateReport(World) : new EpilogueReport()
            };
        }
    }
}
