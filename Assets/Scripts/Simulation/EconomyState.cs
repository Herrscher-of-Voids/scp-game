using System;
using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：O5 财政持久化根状态；Funds 仍保存可用现金以兼容旧存档，本对象保存总资产、四类并行渠道、正式预算、草案、抚恤与责任历史。
    /// English: Persisted O5 finance root; WorldState.Funds remains available cash for legacy compatibility, while this object stores total assets, four concurrent channels, enacted/draft budgets, compensation, and accountability history.
    /// </summary>
    public sealed class EconomyState
    {
        /// <summary>中文：旧四选一字段仅用于反序列化兼容，不再影响收入结算。English: Legacy four-way field retained only for deserialization compatibility and no longer used by income settlement.</summary>
        public FundingSource FundingSource { get; set; } = FundingSource.GovernmentGrants;
        public long TotalAssets { get; set; }
        /// <summary>中文：独立受限应急储备余额，单位为最小货币单位；它是总资产内部重分类，不增加总资产或普通现金。English: Independent restricted emergency reserve balance in smallest currency units; it is an internal reclassification of total assets and adds neither assets nor ordinary cash.</summary>
        public long EmergencyReserveBalance { get; set; }
        public FundingChannelState[] FundingChannels { get; set; } = Array.Empty<FundingChannelState>();
        public BudgetState Budget { get; set; } = new BudgetState();
        public BudgetState? BudgetDraft { get; set; }
        public bool IsDraftRecorded { get; set; }
        /// <summary>中文：最近一次草案记录发生的确定性模拟 Tick；-1 表示从未记录，单位为模拟小时 Tick，不使用现实时间以保证存档回放一致。English: Deterministic simulation tick of the latest draft record; -1 means never recorded, measured in simulation-hour ticks rather than wall time so save replay stays identical.</summary>
        public long DraftRecordedTick { get; set; } = -1;
        /// <summary>中文：最近一次草案记录时的财政周期编号；-1 表示无记录，供界面生成具体状态文本。English: Fiscal cycle number of the latest draft record; -1 means no record and supports concrete UI status text.</summary>
        public int DraftRecordedCycle { get; set; } = -1;
        public bool IsBudgetSignedThisCycle { get; set; }
        public CompensationIncidentState[] CompensationIncidents { get; set; } = Array.Empty<CompensationIncidentState>();
        public FiscalHistoryRecord[] FiscalHistory { get; set; } = Array.Empty<FiscalHistoryRecord>();
        /// <summary>中文：仅保存真实完成的月结快照，不为开局前月份创建假数据。English: Stores only genuinely completed monthly settlements and never creates fake pre-game periods.</summary>
        public FiscalCycleSnapshot[] CycleHistory { get; set; } = Array.Empty<FiscalCycleSnapshot>();
        public long LastIncome { get; set; }
        public long LastExpenses { get; set; }
        public long LastCashFlow { get; set; }
        public long LastAnomalyCosts { get; set; }
        public int ConsecutiveNegativeCashFlowCycles { get; set; }

        /// <summary>中文：迁移旧存档的最小默认机制；只补缺失字段，不覆盖已有正式值，且把旧 GreyMarket 语义显示为“资产没收与投资”。English: Minimal legacy-save defaulting; fills only missing fields, preserves enacted values, and presents legacy GreyMarket semantics as “Asset Seizure & Investment.”</summary>
        public void EnsureFinanceDefaults()
        {
            if (TotalAssets <= 0) TotalAssets = EconomyRules.TemporaryStartingTotalAssets;
            if (FundingChannels == null || FundingChannels.Length != 4) FundingChannels = EconomyRules.CreateTemporaryFundingChannels();
            Budget ??= new BudgetState();
            CompensationIncidents ??= Array.Empty<CompensationIncidentState>();
            FiscalHistory ??= Array.Empty<FiscalHistoryRecord>();
            CycleHistory ??= Array.Empty<FiscalCycleSnapshot>();
        }
    }
}
