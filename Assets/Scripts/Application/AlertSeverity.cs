namespace Scp.Application
{
    /// <summary>
    /// 警报严重度。决定界面配色，也决定时间流速是否被强制打断。
    /// 规则见 GDD 2.1：只有 Critical 视为「重大事件」并强制自动暂停，
    /// Notice 与 Warning 只进入通知历史，不打断时间。
    /// </summary>
    public enum AlertSeverity
    {
        /// <summary>提示。仅供知悉，不需要立即处理。</summary>
        Notice,

        /// <summary>警告。需要关注，但尚未触及失败条件。</summary>
        Warning,

        /// <summary>危急。触发强制暂停，通常直接关联失败条件。</summary>
        Critical
    }
}
