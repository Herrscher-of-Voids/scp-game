using System;
using System.Collections.Generic;
using System.Globalization;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：按稳定序号生成内部业务报告；初始化覆盖四类，月结算最多补充两份，且不消耗世界随机流。
    /// English: Generates internal business reports from stable sequences; initialization covers all four categories and monthly settlement adds at most two without consuming world randomness.
    /// </summary>
    public static class ReportGenerationService
    {
        public static ReportState[] CreateInitial(long tick)
        {
            int sequence = 0;
            return new[]
            {
                Create(ReportCategory.Facility, ReportRisk.Low, tick, ref sequence),
                Create(ReportCategory.Anomaly, ReportRisk.High, tick, ref sequence),
                Create(ReportCategory.Personnel, ReportRisk.Medium, tick, ref sequence),
                Create(ReportCategory.External, ReportRisk.Critical, tick, ref sequence)
            };
        }

        public static void SupplementMonthly(WorldState world)
        {
            var reports = new List<ReportState>(world.Reports);
            int sequence = world.NextReportSequence;
            for (int index = 0; index < 2; index++)
            {
                var category = (ReportCategory)((world.Council.CurrentCycle + index) % 4);
                var risk = index == 0 ? ReportRisk.Low : ReportRisk.Medium;
                reports.Add(Create(category, risk, world.Tick, ref sequence));
            }
            world.NextReportSequence = sequence;
            world.Reports = reports.ToArray();
        }

        private static ReportState Create(ReportCategory category, ReportRisk risk, long tick, ref int sequence)
        {
            sequence++;
            string categoryName = category.ToString();
            return new ReportState
            {
                Id = "RPT-" + sequence.ToString("D6", CultureInfo.InvariantCulture), Category = category, Risk = risk,
                Title = categoryName + " Review " + sequence.ToString("D4", CultureInfo.InvariantCulture),
                Summary = "Deterministic internal " + categoryName.ToLowerInvariant() + " report for O5 review.",
                CreatedTick = tick, Source = "Foundation Internal / " + categoryName, AllowsBatch = risk == ReportRisk.Low
            };
        }
    }
}
