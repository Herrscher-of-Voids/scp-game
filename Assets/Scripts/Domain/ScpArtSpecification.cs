namespace Scp.Domain
{
    /// <summary>
    /// 《05-美术风格指南》的引擎无关规范值，是网格尺寸、固定缩放档与调色板的唯一代码事实来源。
    /// Engine-independent values from docs/05-美术风格指南.md and the single code source of truth for grid size, fixed zoom tiers and palette colours.
    /// </summary>
    /// <remarks>
    /// 表现层只能把这些值转换为各引擎颜色类型，不得另行维护魔法色值；数组调用方不得修改 <see cref="ZoomTiers"/>。
    /// Presentation layers may only convert these values into engine colour types and must not maintain duplicate magic values; callers must not mutate <see cref="ZoomTiers"/>.
    /// </remarks>
    public static class ScpArtSpecification
    {
        /// <summary>单格边长，单位像素；一格对应一米。Cell edge in pixels; one cell represents one metre.</summary>
        public const int GridPixelSize = 32;

        /// <summary>精灵导入 Pixels Per Unit，必须与单格边长一致以保持像素对齐。Sprite import pixels-per-unit, equal to the grid size for pixel alignment.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>1× 细节操作档；缩小除数为 1。Native detail tier with a scale divisor of 1.</summary>
        public const int ZoomTierDetail = 1;

        /// <summary>2× 常规管理档；缩小除数为 2。Default management tier with a scale divisor of 2.</summary>
        public const int ZoomTierDefault = 2;

        /// <summary>4× 全站概览档；缩小除数为 4。Site overview tier with a scale divisor of 4.</summary>
        public const int ZoomTierOverview = 4;

        /// <summary>全部合法固定缩放除数，按细到粗排列；禁止无级缩放以保护 1px 描边。All legal fixed scale divisors, ordered detail to overview; continuous zoom is forbidden to preserve 1px outlines.</summary>
        public static readonly int[] ZoomTiers = { ZoomTierDetail, ZoomTierDefault, ZoomTierOverview };

        /// <summary>描边 #1A1C20，所有精灵统一 1px 描边。Outline #1A1C20 for the universal 1px sprite outline.</summary>
        public static readonly RgbaColor Outline = new RgbaColor(0x1A, 0x1C, 0x20);

        /// <summary>墙体 #5A6068，标准混凝土。Wall #5A6068 for standard concrete.</summary>
        public static readonly RgbaColor Wall = new RgbaColor(0x5A, 0x60, 0x68);

        /// <summary>普通地面 #8E9299。Common floor #8E9299.</summary>
        public static readonly RgbaColor FloorCommon = new RgbaColor(0x8E, 0x92, 0x99);

        /// <summary>收容区地面 #767B82。Containment floor #767B82.</summary>
        public static readonly RgbaColor FloorContainment = new RgbaColor(0x76, 0x7B, 0x82);

        /// <summary>研究区地面 #9AA0A6。Research floor #9AA0A6.</summary>
        public static readonly RgbaColor FloorResearch = new RgbaColor(0x9A, 0xA0, 0xA6);

        /// <summary>阴影 #000000，透明度 25%（0x40）。Shadow #000000 at 25% alpha (0x40).</summary>
        public static readonly RgbaColor Shadow = new RgbaColor(0x00, 0x00, 0x00, 0x40);

        /// <summary>D 级人员 #D9782D。Class-D personnel #D9782D.</summary>
        public static readonly RgbaColor PersonnelClassD = new RgbaColor(0xD9, 0x78, 0x2D);

        /// <summary>MTF Alpha-1 #8C2226。MTF Alpha-1 #8C2226.</summary>
        public static readonly RgbaColor PersonnelAlphaOne = new RgbaColor(0x8C, 0x22, 0x26);

        /// <summary>正常状态 #4E9A5C。Normal state #4E9A5C.</summary>
        public static readonly RgbaColor StateNormal = new RgbaColor(0x4E, 0x9A, 0x5C);

        /// <summary>注意状态 #C9A227。Caution state #C9A227.</summary>
        public static readonly RgbaColor StateCaution = new RgbaColor(0xC9, 0xA2, 0x27);

        /// <summary>警告状态 #D9782D。Warning state #D9782D.</summary>
        public static readonly RgbaColor StateWarning = new RgbaColor(0xD9, 0x78, 0x2D);

        /// <summary>危急状态 #B03030。Critical state #B03030.</summary>
        public static readonly RgbaColor StateCritical = new RgbaColor(0xB0, 0x30, 0x30);

        /// <summary>未知或不可信数据 #5A5F66。Unknown or untrusted data #5A5F66.</summary>
        public static readonly RgbaColor StateUnknown = new RgbaColor(0x5A, 0x5F, 0x66);

        /// <summary>Apollyon #1A1C20，与描边同色。Apollyon #1A1C20, intentionally identical to the outline.</summary>
        public static readonly RgbaColor ObjectClassApollyon = new RgbaColor(0x1A, 0x1C, 0x20);

        /// <summary>纸面底色 #E6E3DC。Paper background #E6E3DC.</summary>
        public static readonly RgbaColor PaperBackground = new RgbaColor(0xE6, 0xE3, 0xDC);

        /// <summary>正文色 #22242A。Paper body text #22242A.</summary>
        public static readonly RgbaColor PaperBodyText = new RgbaColor(0x22, 0x24, 0x2A);

        /// <summary>表格线 #B5B0A6。Paper table rule #B5B0A6.</summary>
        public static readonly RgbaColor PaperTableRule = new RgbaColor(0xB5, 0xB0, 0xA6);

        /// <summary>印章色 #A32E2E。Paper stamp red #A32E2E.</summary>
        public static readonly RgbaColor PaperStampRed = new RgbaColor(0xA3, 0x2E, 0x2E);
    }
}
