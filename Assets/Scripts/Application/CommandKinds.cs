namespace Scp.Application
{
    public static class CommandKinds
    {
        public const string AdjustFunds = "AdjustFunds";
        public const string AllocateBudget = "AllocateBudget";
        public const string SelectFundingSource = "SelectFundingSource";
        public const string SubmitProposal = "SubmitProposal";
        public const string CastPlayerVote = "CastPlayerVote";
        public const string LobbySeat = "LobbySeat";
        public const string PressureSeat = "PressureSeat";
        public const string AuditSite = "AuditSite";
        public const string DirectAnomalyContact = "DirectAnomalyContact";
        public const string UsePrivilege = "UsePrivilege";
        public const string TerminatePersonnel = "TerminatePersonnel";
        public const string ReportApproval = "ReportApproval";
        public const string SaveBudgetDraft = "SaveBudgetDraft";
        public const string DiscardBudgetDraft = "DiscardBudgetDraft";
        public const string SignBudget = "SignBudget";
        public const string SetCompensationAmount = "SetCompensationAmount";
        public const string DecideCompensation = "DecideCompensation";
        public const string PayCompensation = "PayCompensation";
        /// <summary>中文：匿名帷幕事件级处置命令日志键。English: Command-log key for anonymous incident-level veil responses.</summary>
        public const string VeilIncidentAction = "VeilIncidentAction";
    }
}
