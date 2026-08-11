namespace Scp.Application
{
    /// <summary>
    /// 左栏「全球警报」的单条记录。
    /// 由 Application 层从世界状态派生，不是模拟层事件的直接转录——
    /// 警报描述的是「当前持续存在的风险」，而通知历史记录的是「已经发生的事」。
    /// </summary>
    public sealed class OverseerAlertViewModel
    {
        /// <summary>严重度。决定配色与是否强制暂停。</summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>警报标题。短句，直接显示在列表行上。</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>补充说明。写明具体数值或涉及对象，便于玩家判断优先级。</summary>
        public string Detail { get; set; } = string.Empty;
    }
}
