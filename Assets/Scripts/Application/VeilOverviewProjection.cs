using System;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：从帷幕世界状态建立只读、确定性的事件人数估算和十一项洲际总览。输入比例单位均为 0..10000 万分比，预算为 64 位货币，人数单位为人；方法不读取随机流、不修改世界，溢出时饱和到 long.MaxValue。
    /// English: Builds read-only deterministic incident population estimates and eleven continent metrics from veil world state. Ratio inputs use 0..10000 ten-thousandths, budgets use 64-bit currency, and people use persons; methods consume no randomness, mutate no world state, and saturate overflow at long.MaxValue.
    /// </summary>
    public static class VeilOverviewProjection
    {
        private const int ContinentCount = 7;
        private static readonly int[] StageMultipliers = { 1, 3, 10, 30, 100 };

        /// <summary>
        /// 中文：估算单事件涉及人数。severity 和节点 exposure 会夹到 0..10000；阶段倍率依次为 1/3/10/30/100，涉及洲至少按 1 计。返回人数为确定性估算，任何乘法越界均饱和，不抛出也不回绕。
        /// English: Estimates people affected by one incident. Severity and node exposure are clamped to 0..10000; stage multipliers are 1/3/10/30/100 and at least one continent is counted. The return is a deterministic estimate, with multiplication overflow saturated rather than thrown or wrapped.
        /// </summary>
        /// <param name="incident">中文：只读事件状态。English: Read-only incident state.</param>
        /// <returns>中文：估算涉及人数，单位为人。English: Estimated affected people, measured in persons.</returns>
        public static long EstimateAffectedPeople(VeilIncidentState incident)
        {
            if (incident == null) throw new ArgumentNullException(nameof(incident));
            int severity = ClampRatio(incident.Severity);
            int stageIndex = Math.Clamp((int)incident.CurrentStage, 0, StageMultipliers.Length - 1);
            Continent[] continents = ResolveInvolvedContinents(incident);
            long exposureTotal = 0;
            int nodeCount = 0;
            foreach (VeilPropagationNode node in incident.PropagationNodes ?? Array.Empty<VeilPropagationNode>())
            {
                exposureTotal = SaturatingAdd(exposureTotal, ClampRatio(node.Exposure));
                nodeCount++;
            }
            long averageExposure = nodeCount == 0 ? 0 : exposureTotal / nodeCount;
            long estimate = SaturatingMultiply(severity, 100);
            estimate = SaturatingMultiply(estimate, StageMultipliers[stageIndex]);
            estimate = SaturatingMultiply(estimate, 10000 + averageExposure) / 10000;
            return SaturatingMultiply(estimate, Math.Max(1, continents.Length));
        }

        /// <summary>
        /// 中文：返回起源洲与传播节点洲的去重集合，按 Continent 枚举顺序稳定排序。无有效节点时只返回有效起源洲；越界枚举不会写入数组。
        /// English: Returns the deduplicated origin and propagation-node continents in stable Continent enum order. With no valid nodes it returns only a valid origin; out-of-range enum values never index arrays.
        /// </summary>
        /// <param name="incident">中文：只读事件状态。English: Read-only incident state.</param>
        /// <returns>中文：真实涉及洲集合。English: Actual involved-continent set.</returns>
        public static Continent[] ResolveInvolvedContinents(VeilIncidentState incident)
        {
            if (incident == null) throw new ArgumentNullException(nameof(incident));
            var included = new bool[ContinentCount];
            Include(included, incident.OriginContinent);
            foreach (VeilPropagationNode node in incident.PropagationNodes ?? Array.Empty<VeilPropagationNode>()) Include(included, node.Continent);
            int count = 0;
            foreach (bool value in included) if (value) count++;
            var result = new Continent[count];
            int cursor = 0;
            for (int index = 0; index < included.Length; index++) if (included[index]) result[cursor++] = (Continent)index;
            return result;
        }

        /// <summary>
        /// 中文：建立固定十一项总览。全球事件只计一次；损失/恢复归起源洲，节点暴露按节点洲累加，人数按真实涉及洲确定性均分，七洲预算直接复制现有数组；待执行处置因世界投影没有可靠洲归属而保持全局与七洲为 0。
        /// English: Builds the fixed eleven metrics. Global incidents are counted once; loss/recovery belong to the origin, node exposure accumulates by node continent, people are deterministically divided across actual continents, and continent budgets copy the existing array; pending actions remain zero globally and regionally because the world projection has no reliable continent ownership.
        /// </summary>
        /// <param name="world">中文：只读世界快照。English: Read-only world snapshot.</param>
        /// <returns>中文：固定顺序的十一项卡片投影。English: Eleven card projections in fixed order.</returns>
        public static VeilOverviewMetricViewModel[] Project(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var integrity = CopySeven(world.Veil?.ByContinent);
            var active = new long[ContinentCount]; var alerts = new long[ContinentCount]; var loss = new long[ContinentCount]; var recovery = new long[ContinentCount];
            var exposure = new long[ContinentCount]; var people = new long[ContinentCount]; var recovering = new long[ContinentCount]; var resolved = new long[ContinentCount];
            long activeTotal = 0; long lossTotal = 0; long recoveryTotal = 0; long exposureTotal = 0; long peopleTotal = 0; long recoveringTotal = 0; long resolvedTotal = 0;
            foreach (VeilIncidentState incident in world.VeilIncidents ?? Array.Empty<VeilIncidentState>())
            {
                int origin = ValidIndex(incident.OriginContinent);
                bool open = incident.Status is not (VeilIncidentStatus.Resolved or VeilIncidentStatus.Withdrawn);
                if (open) { activeTotal = SaturatingAdd(activeTotal, 1); AddAt(active, origin, 1); }
                if (incident.Status == VeilIncidentStatus.Recovering) { recoveringTotal = SaturatingAdd(recoveringTotal, 1); AddAt(recovering, origin, 1); }
                if (incident.Status == VeilIncidentStatus.Resolved) { resolvedTotal = SaturatingAdd(resolvedTotal, 1); AddAt(resolved, origin, 1); }
                lossTotal = SaturatingAdd(lossTotal, Math.Max(0, incident.Loss)); AddAt(loss, origin, Math.Max(0, incident.Loss));
                recoveryTotal = SaturatingAdd(recoveryTotal, Math.Max(0, incident.Recovery)); AddAt(recovery, origin, Math.Max(0, incident.Recovery));
                foreach (VeilPropagationNode node in incident.PropagationNodes ?? Array.Empty<VeilPropagationNode>()) { long value = ClampRatio(node.Exposure); exposureTotal = SaturatingAdd(exposureTotal, value); AddAt(exposure, ValidIndex(node.Continent), value); }
                if (open) { long estimate = EstimateAffectedPeople(incident); peopleTotal = SaturatingAdd(peopleTotal, estimate); Distribute(people, ResolveInvolvedContinents(incident), estimate); }
                if (open) AddAt(alerts, origin, 1);
            }
            for (int index = 0; index < integrity.Length; index++) if (integrity[index] < 4000) alerts[index] = SaturatingAdd(alerts[index], 1);
            long[] budget = CopySeven(world.Economy?.Budget?.VeilOperations);
            long budgetTotal = Sum(budget);
            long alertTotal = OverseerAlertService.DeriveVeil(world).LongLength;
            return new[]
            {
                Metric("integrity", "全球完整度", Math.Clamp(world.Veil?.Global ?? 0, 0, 10000), integrity, VeilMetricFormat.Ratio),
                Metric("active", "活动事件数", activeTotal, active, VeilMetricFormat.Count),
                Metric("alerts", "警报数", alertTotal, alerts, VeilMetricFormat.Count, "全球值包含无法归属单一大洲的系统性警报。"),
                Metric("loss", "损失总量", lossTotal, loss, VeilMetricFormat.Ratio),
                Metric("recovery", "恢复总量", recoveryTotal, recovery, VeilMetricFormat.Ratio),
                Metric("exposure", "暴露总量", exposureTotal, exposure, VeilMetricFormat.Ratio),
                Metric("people", "估算涉及人数", peopleTotal, people, VeilMetricFormat.People),
                Metric("budget", "帷幕预算", budgetTotal, budget, VeilMetricFormat.Money),
                Metric("pending", "待执行处置", 0, new long[ContinentCount], VeilMetricFormat.Count, "七洲暂无可归属待执行处置：0"),
                Metric("recovering", "恢复中事件", recoveringTotal, recovering, VeilMetricFormat.Count),
                Metric("resolved", "已解决事件", resolvedTotal, resolved, VeilMetricFormat.Count)
            };
        }

        private static VeilOverviewMetricViewModel Metric(string key, string title, long value, long[] byContinent, VeilMetricFormat format, string note = "") => new VeilOverviewMetricViewModel { Key = key, Title = title, Value = value, ByContinent = byContinent, Format = format, TooltipNote = note };
        private static int ClampRatio(int value) => Math.Clamp(value, 0, 10000);
        private static int ValidIndex(Continent continent) { int index = (int)continent; return index >= 0 && index < ContinentCount ? index : -1; }
        private static void Include(bool[] values, Continent continent) { int index = ValidIndex(continent); if (index >= 0) values[index] = true; }
        private static void AddAt(long[] values, int index, long value) { if (index >= 0) values[index] = SaturatingAdd(values[index], value); }
        private static long[] CopySeven(int[]? source) { var result = new long[ContinentCount]; for (int index = 0; index < result.Length; index++) result[index] = source != null && index < source.Length ? Math.Max(0, source[index]) : 0; return result; }
        private static long[] CopySeven(long[]? source) { var result = new long[ContinentCount]; for (int index = 0; index < result.Length; index++) result[index] = source != null && index < source.Length ? Math.Max(0, source[index]) : 0; return result; }
        private static long Sum(long[] values) { long total = 0; foreach (long value in values) total = SaturatingAdd(total, value); return total; }
        private static void Distribute(long[] target, Continent[] continents, long value) { if (continents.Length == 0 || value <= 0) return; long share = value / continents.Length; long remainder = value % continents.Length; for (int index = 0; index < continents.Length; index++) AddAt(target, ValidIndex(continents[index]), share + (index < remainder ? 1 : 0)); }
        private static long SaturatingAdd(long left, long right) { if (left < 0 || right < 0) return 0; try { return checked(left + right); } catch (OverflowException) { return long.MaxValue; } }
        private static long SaturatingMultiply(long left, long right) { if (left <= 0 || right <= 0) return 0; try { return checked(left * right); } catch (OverflowException) { return long.MaxValue; } }
    }
}
