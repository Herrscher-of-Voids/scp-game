using System.Collections.Generic;
using System.Globalization;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 从世界状态派生 O5 左栏的「全球警报」列表。
    /// 放在 Application 层而非 Simulation 层，因为它是投影产物：
    /// 同一个世界状态在不同身份视角下应该派生出不同的警报集合。
    /// 本类不修改世界状态，只读。
    /// </summary>
    public static class OverseerAlertService
    {
        /// <summary>帷幕危急阈值。低于此值视为该洲即将失控（GDD 3.6 失败判定用 20% 作系统性失效线）。</summary>
        private const int VeilCriticalThreshold = 2000;

        /// <summary>帷幕警告阈值。低于此值开始向相邻洲扩散（GDD 3.6 暂定 40%）。</summary>
        private const int VeilWarningThreshold = 4000;

        /// <summary>设施稳定度警告阈值。低于此值视为运营风险。</summary>
        private const int SiteStabilityWarningThreshold = 4000;

        /// <summary>连续赤字周期数警告线。达到 3 个周期且储备清零即财政崩溃（MonthlySettlementService）。</summary>
        private const int DeficitWarningCycles = 2;

        /// <summary>
        /// 按固定顺序派生警报。顺序固定是为了让相同世界状态产出完全相同的列表，
        /// 避免界面刷新时行序抖动。
        /// </summary>
        /// <param name="world">只读的世界状态。</param>
        /// <returns>按危急优先排列的警报数组；无风险时返回空数组。</returns>
        public static OverseerAlertViewModel[] Derive(WorldState world)
        {
            // 分三个桶收集，最后按 危急 → 警告 → 提示 拼接。
            // 不用排序算法，避免相同严重度的条目因排序不稳定而换位。
            var critical = new List<OverseerAlertViewModel>();
            var warning = new List<OverseerAlertViewModel>();
            var notice = new List<OverseerAlertViewModel>();

            CollectVeilAlerts(world, critical, warning);
            CollectBreachAlerts(world, critical, warning);
            CollectSiteAlerts(world, warning);
            CollectFiscalAlerts(world, critical, warning);
            CollectCouncilAlerts(world, critical, notice);

            var result = new OverseerAlertViewModel[critical.Count + warning.Count + notice.Count];
            var cursor = 0;
            for (var index = 0; index < critical.Count; index++)
            {
                result[cursor++] = critical[index];
            }

            for (var index = 0; index < warning.Count; index++)
            {
                result[cursor++] = warning[index];
            }

            for (var index = 0; index < notice.Count; index++)
            {
                result[cursor++] = notice[index];
            }

            return result;
        }

        /// <summary>
        /// 中文：只派生帷幕页可见警报。返回数组仅来自帷幕完整度与匿名事件；100% 或任何未越阈值的并列最低洲均不会生成警报。
        /// English: Derives alerts visible on the veil page only. The returned array comes solely from veil integrity and anonymous incidents; 100% values and tied-lowest continents above thresholds never generate alerts.
        /// </summary>
        public static OverseerAlertViewModel[] DeriveVeil(WorldState world)
        {
            var critical = new List<OverseerAlertViewModel>();
            var warning = new List<OverseerAlertViewModel>();
            CollectVeilAlerts(world, critical, warning);
            foreach (VeilIncidentState incident in world.VeilIncidents ?? System.Array.Empty<VeilIncidentState>())
            {
                if (incident.Status is VeilIncidentStatus.Resolved or VeilIncidentStatus.Withdrawn) continue;
                var target = incident.CurrentStage >= VeilIncidentStage.PublicInstitutionFailure ? critical : warning;
                target.Add(new OverseerAlertViewModel
                {
                    Severity = incident.CurrentStage >= VeilIncidentStage.PublicInstitutionFailure ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Title = incident.AnonymousTitle,
                    Detail = "传播阶段：" + VeilIncidentService.DescribeStage(incident.CurrentStage) + "。"
                });
            }
            critical.AddRange(warning);
            return critical.ToArray();
        }

        /// <summary>七洲帷幕逐洲检查，另加全球总值检查。</summary>
        private static void CollectVeilAlerts(
            WorldState world,
            List<OverseerAlertViewModel> critical,
            List<OverseerAlertViewModel> warning)
        {
            var lowContinents = 0;
            for (var index = 0; index < world.Veil.ByContinent.Length; index++)
            {
                var value = world.Veil.ByContinent[index];
                if (value < VeilCriticalThreshold)
                {
                    // 单洲归零不直接结束游戏，但三洲同时低于 20% 会触发系统性失效。
                    lowContinents++;
                    critical.Add(new OverseerAlertViewModel
                    {
                        Severity = AlertSeverity.Critical,
                        Title = DescribeContinent(index) + " 帷幕接近崩溃",
                        Detail = "完整度 " + FormatRatio(value) + "，该洲异常公开化风险极高。"
                    });
                }
                else if (value < VeilWarningThreshold)
                {
                    warning.Add(new OverseerAlertViewModel
                    {
                        Severity = AlertSeverity.Warning,
                        Title = DescribeContinent(index) + " 帷幕跌破扩散阈值",
                        Detail = "完整度 " + FormatRatio(value) + "，泄密已开始向相邻大洲溢出。"
                    });
                }
            }

            // 三洲同时低于阈值即使总值未归零也会结束游戏，这一条必须单独提示。
            if (lowContinents >= 3)
            {
                critical.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Critical,
                    Title = "帷幕系统性失效在即",
                    Detail = "已有 " + lowContinents.ToString(CultureInfo.InvariantCulture) +
                        " 个大洲低于临界线，达到三个即判定全局失败。"
                });
            }
        }

        /// <summary>收容突破检查。按突破阶段区分严重度。</summary>
        private static void CollectBreachAlerts(
            WorldState world,
            List<OverseerAlertViewModel> critical,
            List<OverseerAlertViewModel> warning)
        {
            var regional = 0;
            var siteWide = 0;
            var partial = 0;
            for (var index = 0; index < world.Anomalies.Length; index++)
            {
                switch (world.Anomalies[index].BreachStage)
                {
                    case BreachStage.Regional:
                        regional++;
                        break;
                    case BreachStage.SiteWide:
                        siteWide++;
                        break;
                    case BreachStage.Partial:
                        partial++;
                        break;
                }
            }

            // 不写具体 SCP 编号，只按突破阶段汇总。
            // 这条规则来自 02-代码规范.md 第 8 节：业务分支禁止依赖具体编号。
            if (regional > 0)
            {
                critical.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Critical,
                    Title = "区域级收容突破",
                    Detail = regional.ToString(CultureInfo.InvariantCulture) + " 项异常已扩散至区域级。"
                });
            }

            if (siteWide > 0)
            {
                critical.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Critical,
                    Title = "站点级收容突破",
                    Detail = siteWide.ToString(CultureInfo.InvariantCulture) + " 项异常已突破至站点级。"
                });
            }

            if (partial > 0)
            {
                warning.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Warning,
                    Title = "局部收容失效",
                    Detail = partial.ToString(CultureInfo.InvariantCulture) + " 项异常处于局部失效状态。"
                });
            }
        }

        /// <summary>设施运营风险检查。稳定度用站点自报值，因此提示中注明可信度。</summary>
        private static void CollectSiteAlerts(WorldState world, List<OverseerAlertViewModel> warning)
        {
            for (var index = 0; index < world.Sites.Length; index++)
            {
                var site = world.Sites[index];
                if (!site.IsOperational)
                {
                    warning.Add(new OverseerAlertViewModel
                    {
                        Severity = AlertSeverity.Warning,
                        Title = "站点-" + site.Id.Value.ToString(CultureInfo.InvariantCulture) + " 已停止运作",
                        Detail = "该设施当前不产出研究，也不维持收容。"
                    });
                    continue;
                }

                // 这里读 ReportedStability 而不是 TrueStability：警报是给 O5 看的，
                // O5 在未审计的情况下只能看到自报值（10-O5监督者.md 第 5 节）。
                var audited = site.AuditCyclesRemaining > 0;
                var visibleStability = audited ? site.TrueStability : site.ReportedStability;
                if (visibleStability < SiteStabilityWarningThreshold)
                {
                    warning.Add(new OverseerAlertViewModel
                    {
                        Severity = AlertSeverity.Warning,
                        Title = "站点-" + site.Id.Value.ToString(CultureInfo.InvariantCulture) + " 稳定度偏低",
                        Detail = "稳定度 " + FormatRatio(visibleStability) +
                            (audited ? "，数据已审计。" : "，数据来自站点自报，未经核验。")
                    });
                }
            }
        }

        /// <summary>财政风险检查。连续赤字加储备耗尽是明确的失败路径。</summary>
        private static void CollectFiscalAlerts(
            WorldState world,
            List<OverseerAlertViewModel> critical,
            List<OverseerAlertViewModel> warning)
        {
            var economy = world.Economy;
            if (economy.ConsecutiveNegativeCashFlowCycles >= DeficitWarningCycles &&
                economy.EmergencyReserveBalance == 0)
            {
                critical.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Critical,
                    Title = "财政崩溃在即",
                    Detail = "已连续 " +
                        economy.ConsecutiveNegativeCashFlowCycles.ToString(CultureInfo.InvariantCulture) +
                        " 个周期赤字且应急储备已清零。"
                });
            }
            else if (economy.ConsecutiveNegativeCashFlowCycles > 0)
            {
                warning.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Warning,
                    Title = "现金流为负",
                    Detail = "已连续 " +
                        economy.ConsecutiveNegativeCashFlowCycles.ToString(CultureInfo.InvariantCulture) +
                        " 个周期赤字，应急储备 " + economy.EmergencyReserveBalance.ToString("N0", CultureInfo.InvariantCulture) + "。"
                });
            }
        }

        /// <summary>议会与政治风险检查。弹劾预警是玩家唯一能提前看到的罢免信号。</summary>
        private static void CollectCouncilAlerts(
            WorldState world,
            List<OverseerAlertViewModel> critical,
            List<OverseerAlertViewModel> notice)
        {
            if (world.Council.ImpeachmentWarning)
            {
                // 议会弹劾有预警，伦理委员会武力罢免没有——后者刻意不出现在警报里
                // （10-O5监督者.md 3.6），因此这里只提示弹劾。
                critical.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Critical,
                    Title = "议会弹劾预警",
                    Detail = "议会内部对你的敌意已达阈值，下一周期可能提出弹劾议案。"
                });
            }

            var pending = 0;
            for (var index = 0; index < world.Council.Proposals.Length; index++)
            {
                if (!world.Council.Proposals[index].IsResolved)
                {
                    pending++;
                }
            }

            if (pending > 0)
            {
                notice.Add(new OverseerAlertViewModel
                {
                    Severity = AlertSeverity.Notice,
                    Title = "待表决议案 " + pending.ToString(CultureInfo.InvariantCulture) + " 项",
                    Detail = "未在截止周期前表决的议案将按缺席处理。"
                });
            }
        }

        /// <summary>大洲索引到中文名。与 GDD 3.6 的七洲代号一一对应。</summary>
        private static string DescribeContinent(int index)
        {
            return ((Continent)index) switch
            {
                Continent.NorthAmerica => "北美洲",
                Continent.SouthAmerica => "南美洲",
                Continent.Europe => "欧洲",
                Continent.Asia => "亚洲",
                Continent.Africa => "非洲",
                Continent.Oceania => "大洋洲",
                Continent.Antarctica => "南极洲",
                _ => "未记录区域"
            };
        }

        /// <summary>万分比定点数转百分比文本。定点数规则见 02-代码规范.md 第 7 节。</summary>
        private static string FormatRatio(int value)
        {
            // 除以 100 得到百分数整数部分，取余得到小数第一位，全程整数运算。
            var percent = value / 100;
            var fraction = value % 100 / 10;
            return percent.ToString(CultureInfo.InvariantCulture) + "." +
                fraction.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
