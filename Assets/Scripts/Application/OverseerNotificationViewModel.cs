namespace Scp.Application
{
    /// <summary>
    /// 通知历史中的单条记录。
    /// 与 OverseerAlertViewModel 的区别：警报描述「当前持续存在的风险」，
    /// 通知描述「已经发生的事」。前者会随状态好转而消失，后者一旦产生就留在历史里。
    /// </summary>
    public sealed class OverseerNotificationViewModel
    {
        /// <summary>事件发生时的绝对 Tick。用于按时间排序与显示时间戳。</summary>
        public long Tick { get; set; }

        /// <summary>严重度。决定配色，也决定是否触发强制暂停。</summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>通知正文。已翻译为中文，界面直接显示。</summary>
        public string Message { get; set; } = string.Empty;
    }
}
