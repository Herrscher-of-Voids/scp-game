using System;
using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 中文：把当前 M1 的全部命令转换为无类型名参数记录并恢复为可执行命令；字段默认值必须与命令默认值一致。
    /// English: Converts every current M1 command into a type-name-free parameter record and restores executable commands; defaults must match command defaults.
    /// </summary>
    public static class CommandLogCodec
    {
        public static CommandLogEntry Encode(ICommand command, long tick)
        {
            var entry = new CommandLogEntry
            {
                SubmittedAtTick = tick,
                RequiredClearance = command.RequiredClearance,
                Kind = command.GetType().Name.Replace("Command", string.Empty)
            };
            switch (command)
            {
                case AdjustFundsCommand value: entry.Kind = CommandKinds.AdjustFunds; entry.Amount = value.Amount; break;
                case AllocateBudgetCommand value: entry.Kind = CommandKinds.AllocateBudget; entry.Budget = value.Budget; break;
                case SelectFundingSourceCommand value: entry.Kind = CommandKinds.SelectFundingSource; entry.Source = value.Source; break;
                case SubmitProposalCommand value: entry.Kind = CommandKinds.SubmitProposal; entry.ProposalKind = value.Kind; entry.Threshold = value.Threshold; entry.Position = value.Position; break;
                case CastPlayerVoteCommand value: entry.Kind = CommandKinds.CastPlayerVote; entry.ProposalId = value.ProposalId; entry.Choice = value.Choice; break;
                case LobbySeatCommand value: entry.Kind = CommandKinds.LobbySeat; entry.SeatId = value.SeatId; entry.SupportBonus = value.SupportBonus; entry.ExchangeSupport = value.ExchangeSupport; break;
                case PressureSeatCommand value: entry.Kind = CommandKinds.PressureSeat; entry.SeatId = value.SeatId; entry.PressureAmount = value.PressureAmount; break;
                case AuditSiteCommand value: entry.Kind = CommandKinds.AuditSite; entry.SiteId = value.SiteId; entry.Cost = value.Cost; break;
                case DirectAnomalyContactCommand: entry.Kind = CommandKinds.DirectAnomalyContact; break;
                case UsePrivilegeCommand value: entry.Kind = CommandKinds.UsePrivilege; entry.EmergencyAction = value.EmergencyAction; break;
                case TerminatePersonnelCommand value: entry.Kind = CommandKinds.TerminatePersonnel; entry.Count = value.Count; break;
                case ReportApprovalCommand value: entry.Kind = CommandKinds.ReportApproval; entry.ReportIds = (string[])value.ReportIds.Clone(); entry.ReportDecision = value.Decision; entry.Conditions = value.Conditions; break;
                case SaveBudgetDraftCommand value: entry.Kind = CommandKinds.SaveBudgetDraft; entry.Budget = value.Budget.Clone(); break;
                case DiscardBudgetDraftCommand: entry.Kind = CommandKinds.DiscardBudgetDraft; break;
                case SignBudgetCommand: entry.Kind = CommandKinds.SignBudget; break;
                case SetCompensationAmountCommand value: entry.Kind = CommandKinds.SetCompensationAmount; entry.IncidentId = value.IncidentId; entry.PersonnelId = value.PersonnelId; entry.Amount = value.Amount; break;
                case DecideCompensationCommand value: entry.Kind = CommandKinds.DecideCompensation; entry.IncidentId = value.IncidentId; entry.CompensationDecision = value.Decision; break;
                case PayCompensationCommand value: entry.Kind = CommandKinds.PayCompensation; entry.IncidentId = value.IncidentId; break;
                case VeilIncidentActionCommand value: entry.Kind = CommandKinds.VeilIncidentAction; entry.VeilIncidentId = value.IncidentId; entry.VeilAction = value.Action; break;
                default: throw new NotSupportedException(command.GetType().FullName);
            }
            return entry;
        }

        public static ICommand Decode(CommandLogEntry entry)
        {
            switch (entry.Kind)
            {
                case CommandKinds.AdjustFunds: return new AdjustFundsCommand { Amount = entry.Amount, RequiredClearance = entry.RequiredClearance };
                case CommandKinds.AllocateBudget: return new AllocateBudgetCommand { Budget = entry.Budget };
                case CommandKinds.SelectFundingSource: return new SelectFundingSourceCommand { Source = entry.Source };
                case CommandKinds.SubmitProposal: return new SubmitProposalCommand { Kind = entry.ProposalKind, Threshold = entry.Threshold, Position = entry.Position };
                case CommandKinds.CastPlayerVote: return new CastPlayerVoteCommand { ProposalId = entry.ProposalId, Choice = entry.Choice };
                case CommandKinds.LobbySeat: return new LobbySeatCommand { SeatId = entry.SeatId, SupportBonus = entry.SupportBonus, ExchangeSupport = entry.ExchangeSupport };
                case CommandKinds.PressureSeat: return new PressureSeatCommand { SeatId = entry.SeatId, PressureAmount = entry.PressureAmount };
                case CommandKinds.AuditSite: return new AuditSiteCommand { SiteId = entry.SiteId, Cost = entry.Cost };
                case CommandKinds.DirectAnomalyContact: return new DirectAnomalyContactCommand();
                case CommandKinds.UsePrivilege: return new UsePrivilegeCommand { EmergencyAction = entry.EmergencyAction };
                case CommandKinds.TerminatePersonnel: return new TerminatePersonnelCommand { Count = entry.Count };
                case CommandKinds.ReportApproval: return new ReportApprovalCommand { ReportIds = (string[])entry.ReportIds.Clone(), Decision = entry.ReportDecision, Conditions = entry.Conditions };
                case CommandKinds.SaveBudgetDraft: return new SaveBudgetDraftCommand { Budget = entry.Budget.Clone() };
                case CommandKinds.DiscardBudgetDraft: return new DiscardBudgetDraftCommand();
                case CommandKinds.SignBudget: return new SignBudgetCommand();
                case CommandKinds.SetCompensationAmount: return new SetCompensationAmountCommand { IncidentId = entry.IncidentId, PersonnelId = entry.PersonnelId, Amount = entry.Amount };
                case CommandKinds.DecideCompensation: return new DecideCompensationCommand { IncidentId = entry.IncidentId, Decision = entry.CompensationDecision };
                case CommandKinds.PayCompensation: return new PayCompensationCommand { IncidentId = entry.IncidentId };
                case CommandKinds.VeilIncidentAction: return new VeilIncidentActionCommand { IncidentId = entry.VeilIncidentId, Action = entry.VeilAction };
                default: throw new NotSupportedException(entry.Kind);
            }
        }
    }
}
