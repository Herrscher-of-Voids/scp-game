using System;

using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class WorldState
    {
        public int SchemaVersion { get; set; } = 3;

        public long Tick { get; set; }

        public DeterministicRandom Random { get; set; } = new DeterministicRandom(1);

        /// <summary>
        /// 中文：从世界级确定性随机流取得闭区间下界、开区间上界的整数，并立即把推进后的可变结构体写回世界状态。参数单位为无量纲整数；当下界不小于上界时沿用随机实现抛出异常。所有模拟代码必须经此入口取值，避免属性返回的结构体副本令种子停滞。
        /// English: Draws an integer from the world deterministic stream using an inclusive lower and exclusive upper bound, then immediately writes the advanced mutable struct back to world state. Parameters are dimensionless integers; invalid bounds retain the generator's exception behavior. Simulation code must use this entry point so a struct copy returned by the property cannot freeze the seed.
        /// </summary>
        public int NextRandomInt(int minInclusive, int maxExclusive)
        {
            var random = Random;
            var value = random.NextInt(minInclusive, maxExclusive);
            Random = random;
            return value;
        }

        /// <summary>
        /// 中文：按万分比概率使用世界级确定性随机流判定一次事件，并写回推进后的状态。参数范围为 0..10000；0 与 10000 分别确定为不发生和必然发生，越界由随机实现拒绝。集中入口保证存档回放与不同调用点共享同一条随机序列。
        /// English: Evaluates one event against a per-ten-thousand probability on the world deterministic stream and writes back the advanced state. The parameter range is 0..10000; zero and ten thousand are deterministic never/always boundaries, while out-of-range values are rejected by the generator. This central entry keeps save replay and all callers on one shared sequence.
        /// </summary>
        public bool RandomChance(int perTenThousand)
        {
            var random = Random;
            var value = random.Chance(perTenThousand);
            Random = random;
            return value;
        }

        public long Funds { get; set; }

        public EconomyState Economy { get; set; } = new EconomyState();

        public VeilState Veil { get; set; } = new VeilState();

        /// <summary>中文：独立帷幕事件集合；空数组是旧存档和正常无事件态的自然默认值，顺序为确定性处理顺序。English: Independent veil-incident collection; an empty array is the natural default for legacy saves and normal no-incident worlds, and order defines deterministic processing.</summary>
        public VeilIncidentState[] VeilIncidents { get; set; } = Array.Empty<VeilIncidentState>();

        public CouncilState Council { get; set; } = new CouncilState();

        public SiteState[] Sites { get; set; } = Array.Empty<SiteState>();

        public AnomalyInstance[] Anomalies { get; set; } = Array.Empty<AnomalyInstance>();

        public PersonnelPool Personnel { get; set; } = new PersonnelPool();

        public FactionRelations Relations { get; set; } = new FactionRelations();

        public int EthicsScore { get; set; }

        public FailureState Failure { get; set; } = new FailureState();

        public WorldFacts Facts { get; set; } = new WorldFacts();

        /// <summary>中文：待处理与已决报告的完整持久化集合；数组顺序是确定性 UI 与存档顺序。English: Complete persisted report collection; array order is the deterministic UI and save order.</summary>
        public ReportState[] Reports { get; set; } = Array.Empty<ReportState>();

        /// <summary>中文：公开审批审计轨迹，只追加不覆盖。English: Public approval audit trail that is append-only.</summary>
        public ReportApprovalRecord[] ReportApprovals { get; set; } = Array.Empty<ReportApprovalRecord>();

        /// <summary>中文：下一报告稳定序号，不依赖删除或数组长度。English: Next stable report sequence independent of deletion or array length.</summary>
        public int NextReportSequence { get; set; }
    }
}
