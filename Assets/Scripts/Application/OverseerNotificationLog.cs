using System.Collections.Generic;
using System.Globalization;

using Scp.Simulation;

namespace Scp.Application
{
    /// <summary>
    /// 通知历史的容器。把模拟层的 DomainEvent 转成界面可显示的中文记录并保留一段历史。
    /// 放在 Application 层的原因：翻译与措辞属于呈现决策，模拟层不应关心显示文本。
    /// 本类只在内存中保留，不写入存档——存档持久化通知需要提升 schema 版本，
    /// 那是独立的一次改动，不在本次界面工作范围内。
    /// </summary>
    public sealed class OverseerNotificationLog
    {
        /// <summary>
        /// 历史条数上限。超出后丢弃最旧的记录。
        /// 设上限是为了防止长局（无限周期）下无界增长吃掉内存。
        /// </summary>
        public const int Capacity = 200;

        // 用 List 而非环形缓冲：容量只有 200，移除首元素的成本可以忽略，
        // 换来的是显示顺序天然与插入顺序一致，不需要额外的索引换算。
        private readonly List<OverseerNotificationViewModel> _entries =
            new List<OverseerNotificationViewModel>();

        /// <summary>按发生顺序排列的通知记录，索引 0 为最旧。</summary>
        public IReadOnlyList<OverseerNotificationViewModel> Entries => _entries;

        /// <summary>
        /// 本次追加中是否出现了危急级通知。
        /// 控制器据此决定是否强制暂停时间（GDD 2.1：只有重大事件打断时间流动）。
        /// </summary>
        public bool HasCriticalSinceLastAppend { get; private set; }

        /// <summary>
        /// 把一批模拟事件转成通知并追加到历史。
        /// </summary>
        /// <param name="events">一次推进产生的事件列表，可以为空。</param>
        public void Append(IReadOnlyList<DomainEvent> events)
        {
            // 每次调用重置标记：这个标记表示「本次推进是否需要打断时间」，
            // 不是「历史里有没有危急事件」，否则一旦出现危急就永久无法继续推进。
            HasCriticalSinceLastAppend = false;
            if (events == null)
            {
                return;
            }

            for (var index = 0; index < events.Count; index++)
            {
                var entry = Translate(events[index]);
                if (entry == null)
                {
                    // 返回 null 表示该事件不需要向玩家展示（例如逐 Tick 的资源产出流水）。
                    continue;
                }

                if (entry.Severity == AlertSeverity.Critical)
                {
                    HasCriticalSinceLastAppend = true;
                }

                _entries.Add(entry);
            }

            // 先追加再裁剪，保证同一批事件内部的顺序不被打乱。
            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(0, _entries.Count - Capacity);
            }
        }

        /// <summary>清空历史。仅在重新开始会话时调用。</summary>
        public void Clear()
        {
            _entries.Clear();
            HasCriticalSinceLastAppend = false;
        }

        /// <summary>
        /// 单个事件到通知记录的翻译。
        /// 返回 null 表示该事件不进入通知历史。
        /// </summary>
        /// <param name="source">模拟层事件。</param>
        /// <returns>可显示的通知记录，或 null 表示不展示。</returns>
        private static OverseerNotificationViewModel? Translate(DomainEvent source)
        {
            switch (source.Kind)
            {
                case DomainEventKind.MonthlySettlement:
                    // 月结是玩家最需要看到的一条，净流量正负决定措辞。
                    return new OverseerNotificationViewModel
                    {
                        Tick = source.Tick,
                        Severity = source.Amount < 0 ? AlertSeverity.Warning : AlertSeverity.Notice,
                        Message = source.Amount < 0
                            ? "周期结算完成，净流量为负 " +
                                (-source.Amount).ToString("N0", CultureInfo.InvariantCulture) + "。"
                            : "周期结算完成，净流量 " +
                                source.Amount.ToString("N0", CultureInfo.InvariantCulture) + "。"
                    };

                case DomainEventKind.CommandRejected:
                    // 指令被拒通常是权限或前置条件问题，属于玩家必须知道的反馈。
                    return new OverseerNotificationViewModel
                    {
                        Tick = source.Tick,
                        Severity = AlertSeverity.Warning,
                        Message = "指令被拒绝：" + source.Detail
                    };

                case DomainEventKind.ObservationLocked:
                    // 观察人数满足时，观察锁定表示基层收容协议正在正常工作，不需要上报 O5。
                    // When observer requirements are satisfied, the lock is routine containment state and must not be escalated to O5.
                    // 该事件仍保留在模拟层，供站点主管、研究主任和诊断日志使用；这里只过滤 O5 视角。
                    // The simulation event remains available to site/research views and diagnostics; only the O5 projection filters it.
                    return null;

                case DomainEventKind.AnomalyActed:
                    // 异常主动行动是收容失效的前兆，按危急处理并打断时间。
                    return new OverseerNotificationViewModel
                    {
                        Tick = source.Tick,
                        Severity = AlertSeverity.Critical,
                        Message = DescribeScp(source) + " 发生主动行为：" +
                            (string.IsNullOrEmpty(source.Detail) ? "详情未记录。" : source.Detail)
                    };

                case DomainEventKind.FundsAdjusted:
                    return new OverseerNotificationViewModel
                    {
                        Tick = source.Tick,
                        Severity = AlertSeverity.Notice,
                        Message = "资金调整 " + source.Amount.ToString("N0", CultureInfo.InvariantCulture) +
                            (string.IsNullOrEmpty(source.Detail) ? "。" : "，" + source.Detail)
                    };

                case DomainEventKind.ResourceYielded:
                    // 资源产出每 Tick 都可能触发，逐条显示会把历史刷满，
                    // 因此不进入通知——其累计结果已经体现在月结与财政页里。
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 事件涉及的异常描述。只用编号，不写具体条目名称，
        /// 避免在代码里出现按 SCP 编号分支的措辞（02-代码规范.md 第 8 节）。
        /// </summary>
        private static string DescribeScp(DomainEvent source)
        {
            return source.ScpId.HasValue
                ? "SCP-" + source.ScpId.Value.Number.ToString(CultureInfo.InvariantCulture)
                : "某项异常";
        }
    }
}
