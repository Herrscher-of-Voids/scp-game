using System;
using System.Globalization;

namespace Scp.Application
{
    /// <summary>
    /// Tick 与基金会历法之间的换算。
    /// 顶栏需要显示「当前年月 + 累计周期」（GDD 2.1、10-O5监督者.md 6.0），
    /// 而模拟层只有从 0 起算的小时 Tick，因此换算规则集中在这里。
    /// 纯整数运算，不依赖 DateTime，避免破坏确定性（02-代码规范.md 第 7 节）。
    /// </summary>
    public static class FoundationCalendar
    {
        /// <summary>一天的 Tick 数。1 Tick = 游戏内 1 小时。</summary>
        public const int TicksPerDay = 24;

        /// <summary>一个周期（1 个月）的 Tick 数。与 WorldSimulation.MonthlyTicks 必须一致。</summary>
        public const int TicksPerCycle = 720;

        /// <summary>一年的周期数。基金会历法按 12 个等长月处理，不做闰月。</summary>
        public const int CyclesPerYear = 12;

        /// <summary>
        /// 独立模式的起始年份。串联与大事件模式各自配置起始年月（GDD 已定稿 #20 相关），
        /// 因此这里只是独立模式的默认值，不是全局硬编码的唯一纪元。
        /// </summary>
        public const int StandaloneStartYear = 1998;

        /// <summary>独立模式的起始月份（1–12）。</summary>
        public const int StandaloneStartMonth = 1;

        /// <summary>
        /// 由绝对 Tick 推出已完整经过的周期数。用作「累计周期」显示值。
        /// </summary>
        /// <param name="tick">世界的绝对 Tick 数，必须为非负。</param>
        /// <returns>已完成的周期数；不足一个周期时返回 0。</returns>
        public static int ElapsedCycles(long tick)
        {
            // 负 Tick 不是合法世界状态，但显示层不应因此抛异常，统一夹到 0。
            if (tick <= 0)
            {
                return 0;
            }

            return (int)(tick / TicksPerCycle);
        }

        /// <summary>
        /// 由起始年月与已经过周期数推出当前年月。
        /// </summary>
        /// <param name="startYear">起始年份。</param>
        /// <param name="startMonth">起始月份，取值 1–12。</param>
        /// <param name="elapsedCycles">已经过的周期数，非负。</param>
        /// <param name="year">输出：当前年份。</param>
        /// <param name="month">输出：当前月份，1–12。</param>
        public static void Resolve(
            int startYear,
            int startMonth,
            int elapsedCycles,
            out int year,
            out int month)
        {
            // 先把「起始月 + 已过周期」折算成从 0 起算的月序号，再拆回年月。
            // startMonth - 1 是为了让 1 月对应偏移 0，避免 12 月进位错位。
            var absoluteMonthIndex = (startMonth - 1) + (elapsedCycles < 0 ? 0 : elapsedCycles);
            year = startYear + (absoluteMonthIndex / CyclesPerYear);
            month = (absoluteMonthIndex % CyclesPerYear) + 1;
        }

        /// <summary>
        /// 生成顶栏用的年月文本，例如「1998 年 1 月」。
        /// </summary>
        /// <param name="year">年份。</param>
        /// <param name="month">月份，1–12。</param>
        /// <returns>供界面直接显示的中文年月字符串。</returns>
        public static string FormatYearMonth(int year, int month)
        {
            return year.ToString(CultureInfo.InvariantCulture) + " 年 " +
                month.ToString(CultureInfo.InvariantCulture) + " 月";
        }

        /// <summary>
        /// 计算当前周期内已经过的天数，供「本周期进度」一类的显示使用。
        /// </summary>
        /// <param name="tick">世界的绝对 Tick 数。</param>
        /// <returns>本周期内已完整经过的天数，范围 0–29。</returns>
        public static int DayOfCycle(long tick)
        {
            if (tick <= 0)
            {
                return 0;
            }

            return (int)(tick % TicksPerCycle / TicksPerDay);
        }

        /// <summary>
        /// 中文：把独立模式绝对 Tick 纯格式化为准确游戏内日期时间，权威纪元为 1998-01-01 00:00，1 Tick 等于 1 小时。负值夹到 0，超出 DateTime 可表示范围时夹到最大整点；不读取系统时钟且不显示内部 Tick。
        /// English: Purely formats an absolute standalone-mode tick as exact in-game date-time using the authoritative 1998-01-01 00:00 epoch and one hour per tick. Negative values clamp to zero and values beyond DateTime range clamp to the latest full hour; no system clock is read and internal ticks are not displayed.
        /// </summary>
        /// <param name="tick">中文：世界绝对 Tick，单位为游戏小时。English: Absolute world tick measured in game hours.</param>
        /// <returns>中文：格式为 YYYY-MM-DD HH:00 的确定性文本。English: Deterministic text in YYYY-MM-DD HH:00 format.</returns>
        public static string FormatStandaloneDateTime(long tick)
        {
            var epoch = new DateTime(StandaloneStartYear, StandaloneStartMonth, 1, 0, 0, 0, DateTimeKind.Unspecified);
            long safeTick = Math.Max(0, tick);
            long maximumHours = (DateTime.MaxValue.Ticks - epoch.Ticks) / TimeSpan.TicksPerHour;
            DateTime value = epoch.AddHours(Math.Min(safeTick, maximumHours));
            return value.ToString("yyyy-MM-dd HH':00'", CultureInfo.InvariantCulture);
        }
    }
}
