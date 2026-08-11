using System;
using System.Collections.Generic;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：按固定 24 Tick（游戏小时）间隔推进活动帷幕事件。传播使用显式通用洲连接图而非数组相邻关系；本切片只提供最小临时强度，不构成最终数值设计。输入世界会就地更新，无返回值；暂停、结束或尚未到间隔的事件不变化，过程不读取随机流。
    /// English: Advances active veil incidents at fixed 24-tick (game-hour) intervals. Spread uses an explicit generic continent graph rather than array adjacency; provisional strengths in this slice are not final balance design. The input world is mutated in place with no return value; paused, closed, or not-yet-due incidents do not change, and no random stream is consumed.
    /// </summary>
    public static class VeilIncidentService
    {
        private const long ProgressIntervalTicks = 24;
        private static readonly Continent[][] Connections =
        {
            new[] { Continent.SouthAmerica, Continent.Europe, Continent.Asia },
            new[] { Continent.NorthAmerica, Continent.Africa },
            new[] { Continent.NorthAmerica, Continent.Asia, Continent.Africa },
            new[] { Continent.NorthAmerica, Continent.Europe, Continent.Africa, Continent.Oceania },
            new[] { Continent.SouthAmerica, Continent.Europe, Continent.Asia },
            new[] { Continent.Asia, Continent.Antarctica },
            new[] { Continent.Oceania }
        };

        public static void Process(WorldState world, IEventSink events)
        {
            foreach (VeilIncidentState incident in world.VeilIncidents ?? Array.Empty<VeilIncidentState>())
            {
                if (incident.Status != VeilIncidentStatus.Active || world.Tick - incident.LastProgressTick < ProgressIntervalTicks) continue;
                incident.LastProgressTick = world.Tick;
                if (incident.CurrentStage < VeilIncidentStage.MultiContinentSystemicCrisis) incident.CurrentStage++;
                incident.Loss = Clamp(incident.Loss + 350 + incident.Severity / 20);
                AddSpreadNode(incident, world.Tick);
                AppendRecord(incident, world.Tick, VeilActionKind.Monitor, "传播阶段推进至“" + DescribeStage(incident.CurrentStage) + "”。");
                int continent = (int)incident.PropagationNodes[incident.PropagationNodes.Length - 1].Continent;
                if (continent >= 0 && continent < world.Veil.ByContinent.Length)
                {
                    world.Veil.ByContinent[continent] = Clamp(world.Veil.ByContinent[continent] - 120 - incident.Severity / 50);
                    world.Veil.RecalculateGlobal();
                }
                events.Emit(new DomainEvent { Kind = DomainEventKind.VeilIncidentChanged, Tick = world.Tick, Detail = incident.StableId + ":" + incident.CurrentStage });
            }
        }

        /// <summary>中文：向事件时间线追加稳定序号记录；参数 Tick 单位为游戏小时，无返回值。English: Appends a stable-sequence timeline record; tick is measured in game hours and there is no return value.</summary>
        public static void AppendRecord(VeilIncidentState incident, long tick, VeilActionKind action, string effect)
        {
            var records = new List<VeilDispositionRecord>(incident.Dispositions ?? Array.Empty<VeilDispositionRecord>())
            {
                new VeilDispositionRecord { StableId = incident.StableId + "-REC-" + incident.NextRecordSequence.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Tick = tick, Action = action, Effect = effect }
            };
            incident.NextRecordSequence++;
            incident.Dispositions = records.ToArray();
        }

        private static void AddSpreadNode(VeilIncidentState incident, long tick)
        {
            Continent from = incident.PropagationNodes.Length == 0 ? incident.OriginContinent : incident.PropagationNodes[incident.PropagationNodes.Length - 1].Continent;
            Continent[] candidates = Connections[(int)from];
            Continent? selected = null;
            foreach (Continent candidate in candidates)
            {
                bool exists = Array.Exists(incident.PropagationNodes, node => node.Continent == candidate);
                if (!exists) { selected = candidate; break; }
            }
            if (!selected.HasValue) return;
            Continent target = selected.Value;
            var nodes = new List<VeilPropagationNode>(incident.PropagationNodes)
            {
                new VeilPropagationNode { StableId = incident.StableId + "-NODE-" + incident.PropagationNodes.Length.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), Continent = target, FirstObservedTick = tick, LocationPrecision = VeilLocationPrecision.ContinentOnly, Exposure = Clamp(incident.Severity + incident.Loss / 2) }
            };
            incident.PropagationNodes = nodes.ToArray();
        }

        public static VeilIncidentState? Find(WorldState world, string stableId) => Array.Find(world.VeilIncidents ?? Array.Empty<VeilIncidentState>(), item => string.Equals(item.StableId, stableId, StringComparison.Ordinal));
        public static string DescribeStage(VeilIncidentStage stage) => stage switch { VeilIncidentStage.ClueBacklog => "线索积压", VeilIncidentStage.LocalPublicAwareness => "局部公众认知", VeilIncidentStage.CrossRegionMediaSpread => "媒体/网络跨地区传播", VeilIncidentStage.PublicInstitutionFailure => "公共机构失控", _ => "多洲系统性危机" };
        private static int Clamp(int value) => value < 0 ? 0 : value > 10000 ? 10000 : value;
    }
}
