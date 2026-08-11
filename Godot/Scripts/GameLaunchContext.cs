namespace Scp.Godot
{
    using Scp.Application;

    /// <summary>
    /// 中文：定义加载场景可执行的一次性工作；新建负责构建并保存候选世界，主档和备份严格读取指定版本，已加载交接只保存调用方给出的内存对象。
    /// English: Defines one-shot work executed by the loading scene: new game builds and saves a candidate world, primary/backup strictly load the selected version, and loaded handoff saves the supplied in-memory object only.
    /// 参数与边界：枚举不携带长期状态；备份类型绝不允许回退到主档或其他版本。
    /// Parameters and boundaries: the enum carries no long-lived state; backup work never falls back to primary or another version.
    /// </summary>
    internal enum GameLaunchKind
    {
        NewGame,
        ContinueGame,
        BackupContinue,
        PersistLoadedGame
    }

    /// <summary>
    /// 中文：跨场景的一次性工作请求。SaveId 精确指定磁盘槽位，Candidate 仅供新建或已加载保存，UpdateLatest 控制是否更新“最近存档”，ReturnScene 是失败后的安全返回页。
    /// English: One-shot cross-scene work request. SaveId selects an exact disk slot, Candidate is only for new or already-loaded saves, UpdateLatest controls the latest-save pointer, and ReturnScene is the safe failure destination.
    /// 单位/返回：字段不含时间单位且对象本身无返回值；消费后立即清空，候选存档不会成为长期全局状态。
    /// Units/return: fields contain no time units and the object has no return value; consumption clears it immediately so the candidate never becomes long-lived global state.
    /// </summary>
    internal sealed class GameLaunchRequest
    {
        public GameLaunchKind Kind { get; set; }
        public string SaveId { get; set; } = string.Empty;
        public SaveFile? Candidate { get; set; }
        public bool UpdateLatest { get; set; }
        public string ReturnScene { get; set; } = "res://Main.tscn";
    }

    /// <summary>
    /// 中文：保存跨场景的一次性工作与一次性已加载 SaveFile；磁盘 SaveRepository 仍是长期状态的唯一来源。
    /// English: Holds one-shot cross-scene work and one-shot loaded SaveFile handoff; the disk SaveRepository remains the sole long-lived state source.
    /// 确定性/原因：每个槽位仅允许一次 Set 后一次 Consume，避免目标场景重复读档或重放旧工作。
    /// Determinism/reason: each slot permits one Set followed by one Consume, preventing duplicate disk reads or replay of stale work in target scenes.
    /// </summary>
    internal static class GameLaunchContext
    {
        private static GameLaunchRequest? _pendingWork;
        private static SaveFile? _loadedSave;

        /// <summary>中文：替换待执行工作；request 不得为空且只存活到加载场景消费。English: Replaces pending work; request must be non-null and lives only until the loading scene consumes it.</summary>
        public static void SetWork(GameLaunchRequest request) => _pendingWork = request;

        /// <summary>中文：原子式取走并清空工作请求；无请求返回 null。English: Takes and clears the work request atomically; returns null when absent.</summary>
        public static GameLaunchRequest? ConsumeWork()
        {
            GameLaunchRequest? request = _pendingWork;
            _pendingWork = null;
            return request;
        }

        /// <summary>中文：把真实完成工作的 SaveFile 交给唯一目标场景；不复制、不写磁盘且覆盖旧的未消费值。English: Hands the SaveFile from completed real work to one target scene; it neither copies nor writes disk and replaces any stale unconsumed value.</summary>
        public static void DeliverLoaded(SaveFile save) => _loadedSave = save;

        /// <summary>中文：原子式消费已加载存档，避免目标页再次完整读取；无交接返回 null。English: Atomically consumes the loaded save to avoid another full read in the target screen; returns null when absent.</summary>
        public static SaveFile? ConsumeLoaded()
        {
            SaveFile? save = _loadedSave;
            _loadedSave = null;
            return save;
        }

        /// <summary>中文：创建指向 user://saves 的仓库；返回值只封装磁盘位置，不缓存世界状态。English: Creates a repository rooted at user://saves; the return value wraps disk location and caches no world state.</summary>
        public static SaveRepository CreateRepository()
        {
            string absoluteRoot = global::Godot.ProjectSettings.GlobalizePath("user://saves");
            return new SaveRepository(absoluteRoot);
        }
    }
}
