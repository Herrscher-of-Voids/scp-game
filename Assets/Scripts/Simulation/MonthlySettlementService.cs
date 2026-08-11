using System;
using System.Collections.Generic;
using Scp.Domain;

namespace Scp.Simulation
{
    public static class MonthlySettlementService
    {
        public static long Settle(WorldState world)
        {
            world.Council.CurrentCycle++;
            world.Facts.OverseerCyclesServed = world.Council.CurrentCycle;
            // 中文：月末阶段固定为议案截止 → 财务 → Alpha-1 → 设施 → 帷幕 → 政治/失败；前一阶段结束游戏后必须立即停止，避免终局后继续改写世界。
            // English: Month-end order is fixed as proposal deadline -> finance -> Alpha-1 -> sites -> veil -> politics/failure; once an earlier phase ends the game, processing must stop so terminal state is not mutated further.
            ResolveProposals(world);
            if (world.Failure.IsEnded)
            {
                return 0;
            }

            // 中文：四类渠道始终并行结算；旧 FundingSource 字段不再选择收入。九项一级预算只累计一次，研究、安全和七洲数组均为明细而不重复计费。
            // English: All four channels always settle concurrently; the legacy FundingSource field no longer selects income. The nine primary budgets are counted once, while research, security, and continent arrays remain non-billable detail.
            world.Economy.EnsureFinanceDefaults();
            var anomalyCosts = CalculateAnomalyCosts(world);
            var income = EconomyRules.ParallelNetIncome(world.Economy.FundingChannels);
            var expenses = checked(world.Economy.Budget.MonthlySpending() + anomalyCosts);
            var cashFlow = checked(income - expenses);
            world.Funds = checked(world.Funds + cashFlow);
            world.Economy.LastIncome = income;
            world.Economy.LastExpenses = expenses;
            world.Economy.LastCashFlow = cashFlow;
            world.Economy.LastAnomalyCosts = anomalyCosts;
            world.Economy.IsBudgetSignedThisCycle = false;
            // 中文：只在真实月结完成时追加趋势点，并深拷贝本周期实际预算；金额为最小货币单位，快照不会被后续草案或签发修改，从而确定性支持下一周期逐科目“较上月”。
            // English: Append a trend point only after a real settlement and deep-copy the actually executed budget; smallest-unit values cannot be mutated by later drafts or enactments, deterministically supporting next-cycle per-category comparisons.
            ProcessReserveAndFiscalFailure(world);
            var cycleHistory = new List<FiscalCycleSnapshot>(world.Economy.CycleHistory ?? Array.Empty<FiscalCycleSnapshot>())
            {
                new FiscalCycleSnapshot { Cycle=world.Council.CurrentCycle, Income=income, Expenses=expenses, NetCashFlow=cashFlow, ClosingCash=world.Funds, SettledBudget=world.Economy.Budget.Clone() }
            };
            world.Economy.CycleHistory = cycleHistory.ToArray();
            if (world.Failure.IsEnded)
            {
                return cashFlow;
            }

            ProcessAlphaOne(world);
            ProcessSites(world);
            ProcessVeil(world);
            if (world.Failure.IsEnded)
            {
                return cashFlow;
            }

            ProcessPolitics(world);
            if (world.Failure.IsEnded)
            {
                return cashFlow;
            }
            // 中文：每次完整周期最多新增两份确定性报告，防止待办无界增长；终局后不得再生成业务状态。
            // English: Each completed cycle adds at most two deterministic reports to bound backlog growth; no business state is generated after termination.
            ReportGenerationService.SupplementMonthly(world);
            world.Council.PrivilegeUsedThisCycle = false;
            foreach (var seat in world.Council.Seats)
            {
                seat.Pressure = seat.Pressure > 5 ? seat.Pressure - 5 : 0;
                if (seat.VetoCooldown > 0)
                {
                    seat.VetoCooldown--;
                }
            }

            return cashFlow;
        }

        private static void ResolveProposals(WorldState world)
        {
            foreach (var proposal in world.Council.Proposals)
            {
                // 中文：ResolveCycle 是公开截止周期，进入该周期即表决；使用小于等于避免所有玩家议案无故多等待一个月。
                // English: ResolveCycle is the public deadline cycle and voting occurs upon entering it; <= prevents every player proposal from waiting an unintended extra month.
                if (!proposal.IsResolved && proposal.ResolveCycle <= world.Council.CurrentCycle)
                {
                    ProposalResolver.Resolve(world, proposal);
                    if (world.Failure.IsEnded)
                    {
                        return;
                    }
                }
            }
        }

        private static long CalculateAnomalyCosts(WorldState world)
        {
            long cost = 0;
            foreach (var anomaly in world.Anomalies)
            {
                cost += anomaly.Definition.Requirement.MonthlyCost;
            }

            return cost;
        }

        private static void ProcessReserveAndFiscalFailure(WorldState world)
        {
            if (world.Economy.LastCashFlow < 0)
            {
                world.Economy.ConsecutiveNegativeCashFlowCycles++;
                var deficit = -world.Economy.LastCashFlow;
                var reserve = world.Economy.EmergencyReserveBalance;
                var covered = reserve < deficit ? reserve : deficit;
                world.Economy.EmergencyReserveBalance -= covered;
                world.Funds = checked(world.Funds + covered);
                FinanceHistory.Append(world, new FiscalHistoryRecord { Kind="EmergencyReserveDraw", SubjectId="cash-shortfall", Tick=world.Tick, Cycle=world.Council.CurrentCycle, Amount=covered, Decision="Covered monthly cash gap from independent reserve" });
            }
            else
            {
                world.Economy.ConsecutiveNegativeCashFlowCycles = 0;
            }

            if (world.Economy.ConsecutiveNegativeCashFlowCycles >= 3 &&
                world.Economy.EmergencyReserveBalance == 0)
            {
                End(world, GameEndReason.FiscalCollapse);
            }
        }

        private static void ProcessAlphaOne(WorldState world)
        {
            var alphaOne = world.Council.AlphaOne;
            var funded = world.Economy.Budget.AlphaOne >= EconomyRules.AlphaOneMaintenanceCost;
            if (!funded)
            {
                alphaOne.IsActive = false;
                alphaOne.IsDeployed = false;
                alphaOne.RebuildCycles = 0;
                alphaOne.LastResult = "Sealed";
                return;
            }

            if (!alphaOne.IsActive)
            {
                alphaOne.RebuildCycles++;
                if (alphaOne.RebuildCycles >= BudgetState.AlphaOneRebuildCyclesRequired)
                {
                    alphaOne.IsActive = true;
                    alphaOne.RebuildCycles = 0;
                    alphaOne.LastResult = "Reactivated";
                }
            }
            else if (alphaOne.IsDeployed)
            {
                alphaOne.IsDeployed = false;
                alphaOne.LastResult = "Operation completed";
            }
        }

        private static void ProcessSites(WorldState world)
        {
            var perSiteOperations = world.Sites.Length == 0 ? 0 : world.Economy.Budget.SiteOperations / world.Sites.Length;
            var perSiteResearch = world.Sites.Length == 0 ? 0 : world.Economy.Budget.Research / world.Sites.Length;
            foreach (var site in world.Sites)
            {
                var stabilityChange = (int)(perSiteOperations / 5000) - 80;
                site.TrueStability = Clamp(site.TrueStability + stabilityChange);
                site.TrueResearchOutput = (int)(perSiteResearch / 1000);
                // 中文：站点事故沿用世界共享确定性随机流；Chance 与伤亡取值都必须写回，避免每个设施重复同一次结果。
                // English: Site incidents use the shared deterministic world stream; both chance and casualty draws must write back so facilities do not repeat one result.
                if (world.RandomChance(Math.Max(0, 100 - site.TrueStability / 100)))
                {
                    site.TrueCasualties += world.NextRandomInt(1, 8);
                    site.TrueStability = Clamp(site.TrueStability - 500);
                    world.Veil.ByContinent[(int)site.Continent] -= 250;
                }

                if (site.AuditCyclesRemaining > 0)
                {
                    site.ReportedStability = site.TrueStability;
                    site.ReportedCasualties = site.TrueCasualties;
                    site.ReportedResearchOutput = site.TrueResearchOutput;
                    site.AuditCyclesRemaining--;
                    continue;
                }

                var distortion = (10000 - site.ReportCredibility) / 10;
                site.ReportedStability = Clamp(site.TrueStability + distortion);
                site.ReportedCasualties = Math.Max(0, site.TrueCasualties - distortion / 100);
                site.ReportedResearchOutput = site.TrueResearchOutput + distortion / 100;
            }
        }

        private static void ProcessVeil(WorldState world)
        {
            var previous = (int[])world.Veil.ByContinent.Clone();
            for (var index = 0; index < world.Veil.ByContinent.Length; index++)
            {
                var recovery = index < world.Economy.Budget.VeilOperations.Length
                    ? (int)(world.Economy.Budget.VeilOperations[index] / 2000)
                    : 0;
                world.Veil.ByContinent[index] = Clamp(world.Veil.ByContinent[index] + recovery - 30);
            }

            for (var index = 0; index < previous.Length; index++)
            {
                if (previous[index] >= 4000)
                {
                    continue;
                }

                var damage = (4000 - previous[index]) / 20;
                var left = (index + previous.Length - 1) % previous.Length;
                var right = (index + 1) % previous.Length;
                world.Veil.ByContinent[left] = Clamp(world.Veil.ByContinent[left] - damage);
                world.Veil.ByContinent[right] = Clamp(world.Veil.ByContinent[right] - damage);
            }

            world.Veil.RecalculateGlobal();
            if (world.Veil.HasFailed())
            {
                End(world, GameEndReason.VeilCollapse);
            }
        }

        private static void ProcessPolitics(WorldState world)
        {
            if (world.EthicsScore < -50)
            {
                world.Failure.HiddenEthicsRemovalRisk += 12;
            }

            if (world.Failure.HiddenEthicsRemovalRisk >= 100)
            {
                End(world, GameEndReason.EthicsRemoval);
                return;
            }

            var hostile = 0;
            foreach (var seat in world.Council.Seats)
            {
                if (!seat.IsPlayer && seat.Relationship <= -40)
                {
                    hostile++;
                }
            }

            if (hostile >= 6 || world.Council.PrivilegeUseCount >= 4)
            {
                if (world.Council.ImpeachmentWarning)
                {
                    AddImpeachmentProposal(world);
                }
                else
                {
                    world.Council.ImpeachmentWarning = true;
                }
            }
            else
            {
                world.Council.ImpeachmentWarning = false;
            }
        }

        private static void AddImpeachmentProposal(WorldState world)
        {
            foreach (var proposal in world.Council.Proposals)
            {
                if (proposal.Kind == ProposalKind.Impeachment && !proposal.IsResolved)
                {
                    return;
                }
            }

            var proposals = new List<ProposalState>(world.Council.Proposals);
            proposals.Add(new ProposalState
            {
                ProposalId = proposals.Count == 0 ? 1 : proposals[proposals.Count - 1].ProposalId + 1,
                Kind = ProposalKind.Impeachment,
                Threshold = ProposalThreshold.TwoThirds,
                Position = new AxisPosition(-100, -100, -100),
                SubmittedBy = FirstNpcSeat(world.Council),
                SubmittedCycle = world.Council.CurrentCycle,
                ResolveCycle = world.Council.CurrentCycle + 1
            });
            world.Council.Proposals = proposals.ToArray();
        }

        private static SeatId FirstNpcSeat(CouncilState council)
        {
            foreach (var seat in council.Seats)
            {
                if (seat.IsOccupied && !seat.IsPlayer)
                {
                    return seat.Id;
                }
            }

            return default;
        }

        private static void End(WorldState world, GameEndReason reason)
        {
            if (!world.Failure.IsEnded)
            {
                world.Failure.IsEnded = true;
                world.Failure.EndReason = reason;
            }
        }

        private static int Clamp(int value)
        {
            return value < 0 ? 0 : value > 10000 ? 10000 : value;
        }
    }
}
