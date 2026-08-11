namespace Scp.Domain
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// 以四个无符号字节保存的引擎无关 RGBA 颜色值；字段顺序固定为红、绿、蓝、透明度，总大小固定为 4 byte。
    /// Engine-independent RGBA colour stored as four unsigned bytes in red, green, blue and alpha order, with a fixed size of 4 bytes.
    /// </summary>
    /// <remarks>
    /// 该值对象只表达美术规范中的精确通道值，不执行色彩空间转换、插值或引擎资源加载，因此可由 Unity、Godot 与纯 .NET 工具共同引用。
    /// This value object only carries exact art-spec channel values; it performs no colour-space conversion, interpolation or engine loading, so Unity, Godot and pure .NET tools can share it.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    public readonly struct RgbaColor : IEquatable<RgbaColor>
    {
        /// <summary>红色通道，范围 0..255。Red channel in the inclusive range 0..255.</summary>
        public readonly byte R;

        /// <summary>绿色通道，范围 0..255。Green channel in the inclusive range 0..255.</summary>
        public readonly byte G;

        /// <summary>蓝色通道，范围 0..255。Blue channel in the inclusive range 0..255.</summary>
        public readonly byte B;

        /// <summary>透明度通道，范围 0..255；0 为完全透明，255 为完全不透明。Alpha channel in 0..255; 0 is transparent and 255 is opaque.</summary>
        public readonly byte A;

        /// <summary>
        /// 创建精确的 8-bit RGBA 颜色；所有参数均已由 byte 类型限制在 0..255，不再执行额外夹取。
        /// Creates an exact 8-bit RGBA colour; byte parameters already enforce 0..255, so no additional clamping occurs.
        /// </summary>
        /// <param name="red">红色通道。Red channel.</param>
        /// <param name="green">绿色通道。Green channel.</param>
        /// <param name="blue">蓝色通道。Blue channel.</param>
        /// <param name="alpha">透明度通道，默认完全不透明。Alpha channel, opaque by default.</param>
        public RgbaColor(byte red, byte green, byte blue, byte alpha = byte.MaxValue)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        /// <summary>按四个通道逐字节判断相等。Compares all four channels byte-for-byte.</summary>
        /// <param name="other">待比较颜色。The colour to compare.</param>
        /// <returns>四个通道完全一致时返回 true。True only when every channel matches.</returns>
        public bool Equals(RgbaColor other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        /// <summary>判断任意对象是否为通道完全一致的 <see cref="RgbaColor"/>。Tests whether an object is an equal <see cref="RgbaColor"/>.</summary>
        /// <param name="obj">待比较对象；null 或其他类型返回 false。Object to compare; null and other types return false.</param>
        /// <returns>对象类型及四通道均相等时返回 true。True when type and channels match.</returns>
        public override bool Equals(object? obj)
        {
            return obj is RgbaColor other && Equals(other);
        }

        /// <summary>把四个通道打包为稳定哈希值，不依赖运行时随机化。Packs all channels into a stable hash independent of runtime randomisation.</summary>
        /// <returns>由 RGBA 四字节组成的 32-bit 哈希。A 32-bit hash composed from the RGBA bytes.</returns>
        public override int GetHashCode()
        {
            return R | G << 8 | B << 16 | A << 24;
        }

        /// <summary>判断两个颜色是否完全相等。Tests two colours for exact equality.</summary>
        public static bool operator ==(RgbaColor left, RgbaColor right)
        {
            return left.Equals(right);
        }

        /// <summary>判断两个颜色是否存在任一通道差异。Tests whether any channel differs.</summary>
        public static bool operator !=(RgbaColor left, RgbaColor right)
        {
            return !left.Equals(right);
        }
    }
}
