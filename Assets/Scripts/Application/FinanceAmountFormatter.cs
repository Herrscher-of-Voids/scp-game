using System;
using System.Globalization;

namespace Scp.Application
{
    /// <summary>
    /// 中文：财政界面共享的中文自动金额格式器。输入和返回均不改变原始 64 位整数货币值：主文本按绝对值选择“万/亿/万亿”并固定两位小数，零和不足一万元保留完整整数货币单位；Tooltip 始终显示带千位分隔的完整金额和“货币单位”。long.MinValue 使用 decimal 取绝对值，避免整数溢出。所有格式固定使用 InvariantCulture，保证不同系统区域设置下测试、存档截图和 UI 文本一致。
    /// English: Shared Chinese automatic money formatter for finance UI. Input and output never mutate the original 64-bit integer currency value: display text selects ten-thousand, hundred-million, or trillion units by absolute magnitude with two decimals, while zero and values below ten thousand retain full integer currency units. Tooltips always show the complete grouped amount plus the currency label. decimal absolute value handles long.MinValue without integer overflow. InvariantCulture keeps tests, save screenshots, and UI text deterministic across system locales.
    /// </summary>
    public static class FinanceAmountFormatter
    {
        public static string Format(long value)
        {
            decimal absolute = Math.Abs((decimal)value);
            decimal divisor;
            string unit;
            if (absolute >= 1_000_000_000_000m) { divisor = 1_000_000_000_000m; unit = "万亿"; }
            else if (absolute >= 100_000_000m) { divisor = 100_000_000m; unit = "亿"; }
            else if (absolute >= 10_000m) { divisor = 10_000m; unit = "万"; }
            else return value.ToString("N0", CultureInfo.InvariantCulture) + " 货币单位";
            return (value / divisor).ToString("F2", CultureInfo.InvariantCulture) + " " + unit;
        }

        /// <summary>中文：返回 Tooltip 完整数值，金额单位为整数货币且保留负号；不做自动缩写。English: Returns the full tooltip amount in integer currency units with sign preserved and no abbreviation.</summary>
        public static string FormatFull(long value) => value.ToString("N0", CultureInfo.InvariantCulture) + " 货币单位";

        /// <summary>中文：返回带显式正负号的自动单位文本；零使用“+”以表达相对变化没有下降。English: Returns auto-unit text with an explicit sign; zero uses plus to express no negative change.</summary>
        public static string FormatSigned(long value) => (value >= 0 ? "+" : "-") + FormatAbsolute(value);

        /// <summary>中文：返回绝对值的自动单位文本，并以 decimal 处理 long.MinValue，供“缺口/结余”等已经由词语表达方向的界面使用。English: Returns auto-unit text for the absolute magnitude and uses decimal for long.MinValue, intended for UI where words such as deficit or surplus already express direction.</summary>
        public static string FormatAbsolute(long value)
        {
            decimal absolute = Math.Abs((decimal)value);
            if (absolute >= 1_000_000_000_000m) return (absolute / 1_000_000_000_000m).ToString("F2", CultureInfo.InvariantCulture) + " 万亿";
            if (absolute >= 100_000_000m) return (absolute / 100_000_000m).ToString("F2", CultureInfo.InvariantCulture) + " 亿";
            if (absolute >= 10_000m) return (absolute / 10_000m).ToString("F2", CultureInfo.InvariantCulture) + " 万";
            return absolute.ToString("N0", CultureInfo.InvariantCulture) + " 货币单位";
        }
    }
}
