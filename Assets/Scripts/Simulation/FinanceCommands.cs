using System;
using System.Collections.Generic;
using System.Linq;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>中文：仅记录预算草案，不推进结算；草案深拷贝进世界状态，随会话/存档保留。English: Records a budget draft without settlement; a deep copy enters world state and persists with the session/save.</summary>
    public sealed class SaveBudgetDraftCommand : ICommand
    {
        public BudgetState Budget { get; set; } = new BudgetState();
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;
        public ValidationResult Validate(IWorldQuery world) => FinanceCommandValidation.ValidateBudget(world, Budget);
        /// <summary>中文：深拷贝九项草案并记录确定性 Tick/周期；金额单位保持整数货币，保存不推进时间且不改变正式预算。English: Deep-copies the nine-category draft and records deterministic tick/cycle metadata; saving advances no time and does not alter the enacted budget.</summary>
        public void Apply(WorldState world, IEventSink events)
        {
            world.Economy.EnsureFinanceDefaults();
            world.Economy.BudgetDraft = Budget.Clone();
            world.Economy.IsDraftRecorded = true;
            world.Economy.DraftRecordedTick = world.Tick;
            world.Economy.DraftRecordedCycle = world.Council.CurrentCycle;
        }
    }

    /// <summary>中文：撤销草案并恢复正式预算作为显示基线；不产生 Tick 或财政历史。English: Discards the draft and restores the enacted budget as display baseline; it produces neither a tick nor fiscal history.</summary>
    public sealed class DiscardBudgetDraftCommand : ICommand
    {
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;
        public ValidationResult Validate(IWorldQuery world) => O5CommandValidation.Validate(world);
        public void Apply(WorldState world, IEventSink events) { world.Economy.BudgetDraft = null; world.Economy.IsDraftRecorded = false; }
    }

    /// <summary>中文：正式签发当前草案；一级预算成为结算依据，并记录未处理抚恤的拖延决定。English: Enacts the current draft as settlement authority and records delay decisions for every unresolved compensation incident.</summary>
    public sealed class SignBudgetCommand : ICommand
    {
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;
        public ValidationResult Validate(IWorldQuery world)
        {
            ValidationResult access = O5CommandValidation.Validate(world);
            if (!access.IsValid) return access;
            WorldState state = ((WorldQuery)world).World;
            return state.Economy.BudgetDraft == null ? ValidationResult.Failure("No recorded budget draft.") : FinanceCommandValidation.ValidateBudget(world, state.Economy.BudgetDraft);
        }
        public void Apply(WorldState world, IEventSink events)
        {
            world.Economy.EnsureFinanceDefaults();
            world.Economy.Budget = world.Economy.BudgetDraft!.Clone();
            world.Economy.BudgetDraft = null; world.Economy.IsDraftRecorded = false; world.Economy.IsBudgetSignedThisCycle = true;
            FinanceHistory.Append(world, new FiscalHistoryRecord { Kind="BudgetSigned", SubjectId="cycle-"+world.Council.CurrentCycle, Tick=world.Tick, Cycle=world.Council.CurrentCycle, Amount=world.Economy.Budget.TotalSpending(), Decision="Signed" });
            foreach (CompensationIncidentState incident in world.Economy.CompensationIncidents.Where(item => item.Status == CompensationStatus.Pending))
            {
                incident.Status = CompensationStatus.Delayed; incident.DelayCycles++;
                FinanceHistory.Append(world, new FiscalHistoryRecord { Kind="CompensationDisposition", SubjectId=incident.IncidentId, Tick=world.Tick, Cycle=world.Council.CurrentCycle, Decision="Delayed" });
            }
        }
    }

    /// <summary>中文：保存单人抚恤金额，金额可为零（清除决定）但不得为负；不自动支付或替玩家决定。English: Stores one person's compensation amount; zero clears the decision, negatives are invalid, and no payment or moral choice is automated.</summary>
    public sealed class SetCompensationAmountCommand : ICommand
    {
        public string IncidentId { get; set; } = string.Empty;
        public string PersonnelId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;
        public ValidationResult Validate(IWorldQuery world)
        {
            ValidationResult access=O5CommandValidation.Validate(world); if(!access.IsValid)return access;
            if(Amount<0)return ValidationResult.Failure("Compensation cannot be negative.");
            WorldState state=((WorldQuery)world).World;
            return state.Economy.CompensationIncidents.Any(i=>i.IncidentId==IncidentId&&i.Personnel.Any(p=>p.PersonnelId==PersonnelId)) ? ValidationResult.Success() : ValidationResult.Failure("Compensation record not found.");
        }
        public void Apply(WorldState world,IEventSink events)
        {
            FallenPersonnelCompensation person=world.Economy.CompensationIncidents.First(i=>i.IncidentId==IncidentId).Personnel.First(p=>p.PersonnelId==PersonnelId); person.Amount=Amount;
            FinanceHistory.Append(world,new FiscalHistoryRecord{Kind="CompensationAmount",SubjectId=IncidentId+":"+PersonnelId,Amount=Amount,Tick=world.Tick,Cycle=world.Council.CurrentCycle,Decision="Recorded"});
        }
    }

    /// <summary>中文：明确把事故抚恤标记为拖延或拒绝，并写入责任历史；该命令不改动逐人金额。English: Explicitly delays or refuses an incident and appends accountability history without changing per-person amounts.</summary>
    public sealed class DecideCompensationCommand : ICommand
    {
        public string IncidentId { get; set; }=string.Empty;
        public CompensationStatus Decision { get; set; }
        public ClearanceLevel RequiredClearance=>ClearanceLevel.Level5;
        public ValidationResult Validate(IWorldQuery world)
        {
            ValidationResult access=O5CommandValidation.Validate(world);if(!access.IsValid)return access;
            if(Decision!=CompensationStatus.Delayed&&Decision!=CompensationStatus.Refused)return ValidationResult.Failure("Decision must be delayed or refused.");
            return ((WorldQuery)world).World.Economy.CompensationIncidents.Any(i=>i.IncidentId==IncidentId)?ValidationResult.Success():ValidationResult.Failure("Compensation incident not found.");
        }
        public void Apply(WorldState world,IEventSink events)
        {
            CompensationIncidentState incident=world.Economy.CompensationIncidents.First(i=>i.IncidentId==IncidentId);incident.Status=Decision;if(Decision==CompensationStatus.Delayed)incident.DelayCycles++;
            FinanceHistory.Append(world,new FiscalHistoryRecord{Kind="CompensationDisposition",SubjectId=IncidentId,Tick=world.Tick,Cycle=world.Council.CurrentCycle,Decision=Decision.ToString()});
        }
    }

    /// <summary>
    /// 中文：正式支付一份事故的逐人抚恤；验证要求所有金额已填写且总额不超过可用现金，应用时逐人写历史并一次性扣款，保证确定性与责任链完整。
    /// English: Formally pays per-person compensation for one incident; validation requires every amount to be entered and the total to fit available cash, while application appends one record per person and deducts once for deterministic accountability.
    /// </summary>
    public sealed class PayCompensationCommand : ICommand
    {
        public string IncidentId { get; set; } = string.Empty;
        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            ValidationResult access = O5CommandValidation.Validate(world);
            if (!access.IsValid) return access;
            WorldState state = ((WorldQuery)world).World;
            CompensationIncidentState? incident = state.Economy.CompensationIncidents.FirstOrDefault(item => item.IncidentId == IncidentId);
            if (incident == null) return ValidationResult.Failure("Compensation incident not found.");
            if (incident.Status == CompensationStatus.Paid) return ValidationResult.Failure("Compensation is already paid.");
            if (incident.Personnel.Length == 0 || incident.Personnel.Any(person => person.Amount <= 0)) return ValidationResult.Failure("Every fallen person requires a positive amount.");
            try
            {
                long total = incident.Personnel.Aggregate(0L, (sum, person) => checked(sum + person.Amount));
                return total <= state.Funds ? ValidationResult.Success() : ValidationResult.Failure("Compensation exceeds available cash.");
            }
            catch (OverflowException) { return ValidationResult.Failure("Compensation total exceeds supported range."); }
        }

        public void Apply(WorldState world, IEventSink events)
        {
            CompensationIncidentState incident = world.Economy.CompensationIncidents.First(item => item.IncidentId == IncidentId);
            long total = 0;
            foreach (FallenPersonnelCompensation person in incident.Personnel)
            {
                total = checked(total + person.Amount);
                person.Status = CompensationStatus.Paid;
                FinanceHistory.Append(world, new FiscalHistoryRecord { Kind="CompensationPaid", SubjectId=IncidentId+":"+person.PersonnelId, Amount=person.Amount, Tick=world.Tick, Cycle=world.Council.CurrentCycle, Decision="Paid" });
            }
            world.Funds = checked(world.Funds - total);
            incident.Status = CompensationStatus.Paid;
        }
    }

    internal static class FinanceCommandValidation
    {
        public static ValidationResult ValidateBudget(IWorldQuery world,BudgetState budget)
        {
            ValidationResult access=O5CommandValidation.Validate(world);if(!access.IsValid)return access;
            if(budget==null)return ValidationResult.Failure("Budget is required.");
            long[] primary={budget.SiteOperations,budget.ContainmentMaintenance,budget.Research,budget.Security,budget.MobileTaskForces,budget.AlphaOne,budget.VeilAndCover,budget.AdministrationAndIntelligence,budget.PersonnelAndEthics};
            if(primary.Any(value=>value<0)||(budget.VeilOperations??Array.Empty<long>()).Any(value=>value<0))return ValidationResult.Failure("Budget values cannot be negative.");
            try { budget.TotalSpending(); } catch(OverflowException) { return ValidationResult.Failure("Budget total exceeds supported range."); }
            return ValidationResult.Success();
        }
    }

    internal static class FinanceHistory
    {
        public static void Append(WorldState world,FiscalHistoryRecord record){var items=new List<FiscalHistoryRecord>(world.Economy.FiscalHistory??Array.Empty<FiscalHistoryRecord>()){record};world.Economy.FiscalHistory=items.ToArray();}
    }
}
