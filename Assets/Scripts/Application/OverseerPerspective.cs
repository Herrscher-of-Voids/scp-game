using System;
using System.Collections.Generic;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    public sealed class OverseerPerspective : IPerspective
    {
        public IdentityRole Role => IdentityRole.Overseer;

        public ClearanceLevel Clearance => ClearanceLevel.Level5;

        public TViewModel Project<TViewModel>(WorldState world)
        {
            if (typeof(TViewModel) != typeof(OverseerViewModel))
            {
                throw new NotSupportedException(typeof(TViewModel).FullName);
            }

            var seats = new CouncilSeatViewModel[world.Council.Seats.Length];
            for (var index = 0; index < seats.Length; index++)
            {
                var source = world.Council.Seats[index];
                seats[index] = new CouncilSeatViewModel
                {
                    SeatId = source.Id,
                    IsOccupied = source.IsOccupied,
                    IsPlayer = source.IsPlayer
                };
            }

            var sites = new SiteReportViewModel[world.Sites.Length];
            for (var index = 0; index < sites.Length; index++)
            {
                var source = world.Sites[index];
                var audited = source.AuditCyclesRemaining > 0;

                // 异常数量按设施聚合。这属于结构性事实，不经站点自报，因此不做失真处理。
                var anomalyCount = 0;
                var breachingCount = 0;
                for (var anomalyIndex = 0; anomalyIndex < world.Anomalies.Length; anomalyIndex++)
                {
                    var anomaly = world.Anomalies[anomalyIndex];
                    if (!anomaly.SiteId.Equals(source.Id))
                    {
                        continue;
                    }

                    anomalyCount++;
                    if (anomaly.BreachStage > BreachStage.Latent)
                    {
                        breachingCount++;
                    }
                }

                sites[index] = new SiteReportViewModel
                {
                    SiteId = source.Id,
                    Code = source.Code,
                    DisplayLabel = source.DisplayLabel,
                    LocationText = source.LocationText,
                    LocationPrecision = source.LocationPrecision,
                    MapX = source.MapX,
                    MapY = source.MapY,
                    // 以下四项是结构性事实：编号、位置、等级、观察员编制不由站点自报，不失真。
                    Continent = source.Continent,
                    SecurityLevel = source.SecurityLevel,
                    AvailableObservers = source.AvailableObservers,
                    IsOperational = source.IsOperational,
                    AnomalyCount = anomalyCount,
                    BreachingAnomalyCount = breachingCount,
                    // 以下三项是站点自报值：审计期内等于真实值，否则可能被篡改。
                    Stability = audited ? source.TrueStability : source.ReportedStability,
                    Casualties = audited ? source.TrueCasualties : source.ReportedCasualties,
                    ResearchOutput = audited ? source.TrueResearchOutput : source.ReportedResearchOutput,
                    IsAudited = audited
                };
            }

            var voteRecords = new VoteRecordViewModel[world.Council.VoteRecords.Length];
            for (var index = 0; index < voteRecords.Length; index++)
            {
                var source = world.Council.VoteRecords[index];
                var votes = new SeatVoteViewModel[source.Votes.Length];
                for (var voteIndex = 0; voteIndex < votes.Length; voteIndex++)
                {
                    var vote = source.Votes[voteIndex];
                    votes[voteIndex] = new SeatVoteViewModel
                    {
                        SeatId = vote.SeatId,
                        Choice = vote.Choice
                    };
                }

                voteRecords[index] = new VoteRecordViewModel
                {
                    ProposalId = source.ProposalId,
                    Kind = source.Kind,
                    Threshold = source.Threshold,
                    Cycle = source.Cycle,
                    Passed = source.Passed,
                    Votes = votes
                };
            }

            var proposals = new ProposalViewModel[world.Council.Proposals.Length];
            for (var index = 0; index < proposals.Length; index++)
            {
                var source = world.Council.Proposals[index];
                proposals[index] = new ProposalViewModel
                {
                    ProposalId = source.ProposalId,
                    Kind = source.Kind,
                    Threshold = source.Threshold,
                    SubmittedBy = source.SubmittedBy,
                    SubmittedCycle = source.SubmittedCycle,
                    ResolveCycle = source.ResolveCycle,
                    PlayerVote = source.PlayerVote,
                    IsResolved = source.IsResolved,
                    Passed = source.Passed,
                    // 中文：冷却周期是议会公开程序信息，可安全投影；NPC 立场、关系与交换债务仍不离开模拟层。
                    // English: The cooldown cycle is public council procedure and safe to project; NPC axes, relationships, and vote debts remain inside simulation.
                    ResubmitAvailableCycle = source.ResubmitAvailableCycle
                };
            }

            // 中文：报告与公开审批记录逐项复制，避免 UI 修改数组后污染世界快照。
            // English: Reports and public approval records are copied item by item so UI array mutation cannot corrupt the world snapshot.
            var reports = new ReportViewModel[world.Reports.Length];
            for (var index = 0; index < reports.Length; index++)
            {
                var source = world.Reports[index];
                reports[index] = new ReportViewModel { Id = source.Id, Category = source.Category, Risk = source.Risk, Status = source.Status, Title = source.Title, Summary = source.Summary, CreatedTick = source.CreatedTick, Source = source.Source, AllowsBatch = source.AllowsBatch };
            }
            var approvals = new ReportApprovalViewModel[world.ReportApprovals.Length];
            for (var index = 0; index < approvals.Length; index++)
            {
                var source = world.ReportApprovals[index];
                approvals[index] = new ReportApprovalViewModel { Id = source.Id, ReportIds = (string[])source.ReportIds.Clone(), Decision = source.Decision, DecidedTick = source.DecidedTick, Conditions = source.Conditions };
            }

            // 中文：旧存档在投影入口补齐新财政默认字段；只补缺失值，确保旧 GreyMarket 不再恢复成四选一结算。
            // English: The projection boundary fills new finance defaults for legacy saves without overwriting existing values, ensuring legacy GreyMarket never revives four-way settlement.
            world.Economy.EnsureFinanceDefaults();
            var budget = world.Economy.Budget;
            var draft = world.Economy.BudgetDraft ?? budget;
            var alphaOne = world.Council.AlphaOne;

            // 顶栏要显示「当前年月」，而模拟层只有小时 Tick，因此在投影期换算。
            // 起始年月目前取独立模式默认值；串联与大事件模式接入后改为从存档模式读取。
            var elapsedCycles = FoundationCalendar.ElapsedCycles(world.Tick);
            int calendarYear;
            int calendarMonth;
            FoundationCalendar.Resolve(
                FoundationCalendar.StandaloneStartYear,
                FoundationCalendar.StandaloneStartMonth,
                elapsedCycles,
                out calendarYear,
                out calendarMonth);

            object result = new OverseerViewModel
            {
                // Tick 与周期是公开信息，任何权限都能看到，无需失真处理。
                Tick = world.Tick,
                CurrentCycle = world.Council.CurrentCycle,
                CalendarYear = calendarYear,
                CalendarMonth = calendarMonth,
                DayOfCycle = FoundationCalendar.DayOfCycle(world.Tick),
                Funds = world.Funds,
                LastCashFlow = world.Economy.LastCashFlow,
                LastIncome = world.Economy.LastIncome,
                LastExpenses = world.Economy.LastExpenses,
                FundingSource = world.Economy.FundingSource,
                ConsecutiveDeficitCycles = world.Economy.ConsecutiveNegativeCashFlowCycles,
                Budget = ProjectBudget(budget),
                Finance = ProjectFinance(world, budget, draft),
                VeilByContinent = (int[])world.Veil.ByContinent.Clone(),
                GlobalVeil = world.Veil.Global,
                Veil = ProjectVeil(world),
                Sites = sites,
                Reports = reports,
                ReportApprovals = approvals,
                Seats = seats,
                VoteRecords = voteRecords,
                Proposals = proposals,
                // 警报由世界状态派生而非直接转录事件：它描述当前持续存在的风险，
                // 派生过程只使用 O5 可见的数据（未审计设施用自报值）。
                Alerts = OverseerAlertService.Derive(world),
                AlphaOne = new AlphaOneViewModel
                {
                    IsActive = alphaOne.IsActive,
                    IsDeployed = alphaOne.IsDeployed,
                    RebuildCycles = alphaOne.RebuildCycles,
                    Deployments = alphaOne.Deployments,
                    LastResult = alphaOne.LastResult
                },
                ContactRestrictionActive = world.Council.ContactRestrictionActive,
                CanSubmitWorldRestart = StrategicCapabilityService.IsAvailable(world, StrategicCapability.WorldReconstruction),
                AmnesticSupplyAvailable = StrategicCapabilityService.IsAvailable(world, StrategicCapability.AmnesticSupply),
                Failure = new FailureViewModel
                {
                    IsEnded = world.Failure.IsEnded,
                    EndReason = world.Failure.EndReason
                }
            };
            return (TViewModel)result;
        }

        /// <summary>
        /// 中文：深拷贝匿名帷幕事件投影；洲级位置把坐标强制为 0，设施引用仅原样公开内部稳定 ID，不推断设施名称或隐藏真相。返回对象可由 UI 安全修改而不污染世界。
        /// English: Deep-copies anonymous veil incidents; continent-only positions force coordinates to zero, while facility references expose only the stored internal stable ID without inferring a facility name or hidden truth. The returned object is safe for UI mutation.
        /// </summary>
        private static VeilViewModel ProjectVeil(WorldState world)
        {
            VeilIncidentState[] sources = world.VeilIncidents ?? Array.Empty<VeilIncidentState>();
            var incidents = new VeilIncidentViewModel[sources.Length];
            for (int index = 0; index < incidents.Length; index++)
            {
                VeilIncidentState source = sources[index];
                var nodes = new VeilPropagationNodeViewModel[source.PropagationNodes.Length];
                for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    VeilPropagationNode node = source.PropagationNodes[nodeIndex];
                    bool mapped = node.LocationPrecision != VeilLocationPrecision.ContinentOnly && node.MapX > 0 && node.MapY > 0;
                    nodes[nodeIndex] = new VeilPropagationNodeViewModel { StableId = node.StableId, Continent = node.Continent, FirstObservedTick = node.FirstObservedTick, LocationPrecision = node.LocationPrecision, MapX = mapped ? node.MapX : 0, MapY = mapped ? node.MapY : 0, Exposure = node.Exposure };
                }
                var timeline = new VeilTimelineEntryViewModel[source.Dispositions.Length];
                for (int recordIndex = 0; recordIndex < timeline.Length; recordIndex++)
                {
                    VeilDispositionRecord record = source.Dispositions[recordIndex];
                    timeline[recordIndex] = new VeilTimelineEntryViewModel { StableId = record.StableId, Tick = record.Tick, Action = record.Action, Effect = record.Effect };
                }
                long exposure = 0; foreach (VeilPropagationNode node in source.PropagationNodes ?? Array.Empty<VeilPropagationNode>()) { try { exposure = checked(exposure + Math.Clamp(node.Exposure, 0, 10000)); } catch (OverflowException) { exposure = long.MaxValue; break; } }
                incidents[index] = new VeilIncidentViewModel { StableId = source.StableId, Title = source.AnonymousTitle, SourceCategory = source.SourceCategory, CreatedTick = source.CreatedTick, DiscoveredTick = source.DiscoveredTick, OriginContinent = source.OriginContinent, FacilityStableId = source.FacilityStableId ?? string.Empty, LocationPrecision = source.LocationPrecision, Severity = source.Severity, Stage = source.CurrentStage, Loss = source.Loss, Recovery = source.Recovery, EstimatedAffectedPeople = VeilOverviewProjection.EstimateAffectedPeople(source), Exposure = exposure, InvolvedContinents = VeilOverviewProjection.ResolveInvolvedContinents(source), Status = source.Status, Nodes = nodes, Timeline = timeline };
            }
            return new VeilViewModel { GlobalIntegrity = world.Veil.Global, IntegrityByContinent = (int[])world.Veil.ByContinent.Clone(), Incidents = incidents, Alerts = OverseerAlertService.DeriveVeil(world), OverviewMetrics = VeilOverviewProjection.Project(world) };
        }

        /// <summary>中文：深拷贝九项预算与二级明细，金额单位保持 64 位整数。English: Deep-copies nine primary budgets and their secondary detail while preserving 64-bit integer currency.</summary>
        private static BudgetViewModel ProjectBudget(BudgetState source) => new BudgetViewModel
        {
            SiteOperations=source.SiteOperations, ContainmentMaintenance=source.ContainmentMaintenance, Research=source.Research, Security=source.Security,
            MobileTaskForces=source.MobileTaskForces, AlphaOne=source.AlphaOne, VeilAndCover=source.VeilAndCover,
            AdministrationAndIntelligence=source.AdministrationAndIntelligence, PersonnelAndEthics=source.PersonnelAndEthics,
            ResearchDetail=new ResearchBudgetDetail{BasicResearch=source.ResearchDetail.BasicResearch,PriorityProjects=source.ResearchDetail.PriorityProjects,ContainmentTechnology=source.ResearchDetail.ContainmentTechnology,AnomalousApplications=source.ResearchDetail.AnomalousApplications},
            SecurityDetail=new SecurityBudgetDetail{SiteSecurity=source.SecurityDetail.SiteSecurity,MtfHeadquarters=source.SecurityDetail.MtfHeadquarters,MtfTeamCount=source.SecurityDetail.MtfTeamCount,MtfTeamMaintenance=source.SecurityDetail.MtfTeamMaintenance,MtfDeployment=source.SecurityDetail.MtfDeployment,AlphaOne=source.SecurityDetail.AlphaOne},
            VeilOperations=(long[])(source.VeilOperations??Array.Empty<long>()).Clone()
        };

        /// <summary>中文：计算顶部六指标并复制四渠道/抚恤；无上月实绩时使用当前并行净收入与正式预算作为本月预测，不补造历史点。English: Computes the six top indicators and copies channels/compensation; before first settlement, current concurrent net income and enacted budget form this-month projections without inventing historical points.</summary>
        private static FinanceViewModel ProjectFinance(WorldState world,BudgetState enacted,BudgetState draft)
        {
            long income=world.Economy.LastIncome!=0?world.Economy.LastIncome:EconomyRules.ParallelNetIncome(world.Economy.FundingChannels);
            long expenses=world.Economy.LastExpenses!=0?world.Economy.LastExpenses:enacted.TotalSpending();
            var channels=new FundingChannelViewModel[world.Economy.FundingChannels.Length];
            for(int i=0;i<channels.Length;i++){FundingChannelState item=world.Economy.FundingChannels[i];channels[i]=new FundingChannelViewModel{Key=item.Key,Name=item.DisplayName,Income=item.Income,FixedCost=item.FixedCost,NetIncome=item.NetIncome,Risk=item.Risk,Relationship=item.Relationship,CycleChange=item.CycleChange};}
            var incidents=new CompensationIncidentViewModel[world.Economy.CompensationIncidents.Length];
            for(int i=0;i<incidents.Length;i++){CompensationIncidentState item=world.Economy.CompensationIncidents[i];var people=new CompensationPersonViewModel[item.Personnel.Length];for(int p=0;p<people.Length;p++)people[p]=new CompensationPersonViewModel{PersonnelId=item.Personnel[p].PersonnelId,Name=item.Personnel[p].DisplayName,Amount=item.Personnel[p].Amount,Status=item.Personnel[p].Status};incidents[i]=new CompensationIncidentViewModel{IncidentId=item.IncidentId,Facility=item.FacilityLabel,ReportedTick=item.ReportedTick,Status=item.Status,DelayCycles=item.DelayCycles,Personnel=people};}
            // 中文：历史投影反转为最新在前，使当前周期出现在趋势带左侧；空数组保持空，不制造零值月份。
            // English: Reverse settled history to newest-first so the current cycle appears at the trend strip's left; an empty array remains empty and creates no zero-valued months.
            var history=new FiscalCycleViewModel[world.Economy.CycleHistory.Length];
            for(int i=0;i<history.Length;i++){FiscalCycleSnapshot item=world.Economy.CycleHistory[world.Economy.CycleHistory.Length-1-i];history[i]=new FiscalCycleViewModel{Cycle=item.Cycle,Income=item.Income,Expenses=item.Expenses,NetCashFlow=item.NetCashFlow,ClosingCash=item.ClosingCash};}
            // 中文：右栏只投影最近三次真实财政决定，按追加历史倒序且不制造占位决定；金额和 Tick 保持原始整数，供 UI 定量显示。
            // English: The right column projects only the latest three real fiscal decisions in reverse append order and never fabricates placeholders; original integer amounts and ticks remain available for quantitative UI text.
            int decisionCount=Math.Min(3,world.Economy.FiscalHistory.Length);var decisions=new FiscalDecisionViewModel[decisionCount];
            for(int i=0;i<decisionCount;i++){FiscalHistoryRecord item=world.Economy.FiscalHistory[world.Economy.FiscalHistory.Length-1-i];decisions[i]=new FiscalDecisionViewModel{Kind=item.Kind,SubjectId=item.SubjectId,Decision=item.Decision,Amount=item.Amount,Tick=item.Tick,Cycle=item.Cycle};}
            // 中文：义务和事故先从完整事故数组独立汇总，再与空或非空的决定列表分别写入投影；金额为整数货币，已支付/已拒绝事故不再计入待办。English: Obligations and pending cases are aggregated independently from the full incident array before decisions are assigned; values are integer currency and paid/refused incidents no longer count as pending.
            long obligations=0;int pending=0;foreach(CompensationIncidentViewModel incident in incidents){if(incident.Status==CompensationStatus.Paid||incident.Status==CompensationStatus.Refused)continue;pending++;foreach(CompensationPersonViewModel person in incident.Personnel)obligations=checked(obligations+person.Amount);}
            long necessaryExpenses=enacted.NecessaryMonthlySpending();decimal reserveMonths=necessaryExpenses>0?decimal.Round(world.Economy.EmergencyReserveBalance/(decimal)necessaryExpenses,1):0;long liquidityGap=Math.Max(0,expenses-world.Funds);
            // 中文：变化口径取最新真实月结的逐科目预算快照；旧档若只有汇总历史而没有该快照，则仍显示有历史的“上月”缺失状态，绝不把当前正式预算伪装成上月实绩。
            // English: The comparison basis comes from the newest real settlement's per-category budget snapshot; legacy saves with aggregate-only history retain a missing-prior state rather than disguising the current enacted budget as prior actuals.
            BudgetState? previousBudget=world.Economy.CycleHistory.Length>0?world.Economy.CycleHistory[world.Economy.CycleHistory.Length-1].SettledBudget:null;
            BudgetLineViewModel[] lines=ProjectBudgetLines(enacted,draft,previousBudget);
            return new FinanceViewModel{AvailableCash=world.Funds,TotalAssets=world.Economy.TotalAssets,ReserveBalance=world.Economy.EmergencyReserveBalance,NecessaryMonthlyExpenses=necessaryExpenses,MonthlyIncome=income,MonthlyExpenses=expenses,NetCashFlow=income-expenses,ReserveMonths=reserveMonths,AnomalyCosts=world.Economy.LastAnomalyCosts,IsDraftRecorded=world.Economy.IsDraftRecorded,DraftRecordedTick=world.Economy.DraftRecordedTick,DraftRecordedCycle=world.Economy.DraftRecordedCycle,IsBudgetSignedThisCycle=world.Economy.IsBudgetSignedThisCycle,EnactedBudget=ProjectBudget(enacted),DraftBudget=ProjectBudget(draft),BudgetLines=lines,RiskSummary=new FinanceRiskSummaryViewModel{CashFlow=income-expenses,ReserveMonths=reserveMonths,LiquidityGap=liquidityGap,UnpaidObligations=obligations,PendingIncidentCount=pending},Channels=channels,CompensationIncidents=incidents,CycleHistory=history,RecentDecisions=decisions};
        }

        /// <summary>中文：按固定业务顺序建立十条六列表格投影。新局变化相对集中正式预算基准；有逐科目月结快照后改为相对上月实绩。最低线与比例始终相对集中基准，所有除法使用 decimal；比较基数为零时百分比为 null，明确交给 UI 显示“新增”或“—”。English: Builds ten six-column rows in fixed business order. New games compare drafts with the centralized enacted baseline; once a per-category settlement snapshot exists, comparisons use prior actuals. Minimum and ratio remain relative to the centralized baseline, all division uses decimal, and a zero comparison basis yields null percent for explicit “new” or dash UI wording.</summary>
        private static BudgetLineViewModel[] ProjectBudgetLines(BudgetState enacted,BudgetState draft,BudgetState? previous)
        {
            string[] keys={"设施运营","收容维护","研究与实验","安保","普通 MTF","Alpha-1","帷幕与掩盖","行政与情报","人员与伦理保障"};
            long[] baseline={enacted.SiteOperations,enacted.ContainmentMaintenance,enacted.Research,enacted.Security,enacted.MobileTaskForces,enacted.AlphaOne,enacted.VeilAndCover,enacted.AdministrationAndIntelligence,enacted.PersonnelAndEthics};
            long[] draftValues={draft.SiteOperations,draft.ContainmentMaintenance,draft.Research,draft.Security,draft.MobileTaskForces,draft.AlphaOne,draft.VeilAndCover,draft.AdministrationAndIntelligence,draft.PersonnelAndEthics};
            long[]? previousValues=previous==null?null:new[]{previous.SiteOperations,previous.ContainmentMaintenance,previous.Research,previous.Security,previous.MobileTaskForces,previous.AlphaOne,previous.VeilAndCover,previous.AdministrationAndIntelligence,previous.PersonnelAndEthics};
            var rows=new BudgetLineViewModel[keys.Length];for(int i=0;i<rows.Length;i++){long comparison=previousValues?[i]??baseline[i];long delta=draftValues[i]-comparison;rows[i]=new BudgetLineViewModel{Key=keys[i],BaselineAmount=baseline[i],PreviousAmount=previousValues?[i],DraftAmount=draftValues[i],ChangeAmount=delta,ChangePercent=comparison>0?decimal.Round(delta*100m/comparison,1):null,ChangeBasis=previousValues==null?"相对预算基准":"较上月",MinimumLine=checked(baseline[i]*80/100),RatioPercent=baseline[i]>0?decimal.Round(draftValues[i]*100m/baseline[i],0):0};}return rows;
        }

        public IReadOnlyList<CommandDescriptor> AvailableCommands(WorldState world)
        {
            return Array.Empty<CommandDescriptor>();
        }
    }
}
