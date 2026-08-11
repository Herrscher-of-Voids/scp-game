using Scp.Domain;

namespace Scp.Simulation
{
    public readonly struct TraitContext
    {
        public TraitContext(WorldState world, AnomalyInstance anomaly, SiteState site, IWorldQuery query)
        {
            World = world;
            Anomaly = anomaly;
            Site = site;
            Query = query;
        }

        public WorldState World { get; }

        public AnomalyInstance Anomaly { get; }

        public SiteState Site { get; }

        public IWorldQuery Query { get; }

        /// <summary>
        /// 中文：为 trait 处理器提供世界级确定性整数取值；参数为闭下界与开上界，返回值无单位，并由 WorldState 统一推进和写回随机状态。
        /// English: Provides trait handlers with a world-level deterministic integer draw; bounds are inclusive/exclusive, the result is dimensionless, and WorldState centrally advances and writes back the random state.
        /// </summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return World.NextRandomInt(minInclusive, maxExclusive);
        }

        /// <summary>
        /// 中文：为 trait 处理器按万分比判定事件；边界与确定性由 WorldState 的共享随机入口保证。
        /// English: Evaluates a trait event using a per-ten-thousand probability; WorldState's shared random entry guarantees boundaries and determinism.
        /// </summary>
        public bool Chance(int perTenThousand)
        {
            return World.RandomChance(perTenThousand);
        }
    }
}
