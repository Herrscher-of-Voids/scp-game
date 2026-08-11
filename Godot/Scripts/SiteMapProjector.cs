namespace Scp.Godot
{
    using Scp.Domain;

    /// <summary>
    /// 把设施可见位置转换为 2:1 世界地图上的归一化标记坐标；该类属于正式 Godot 表现层。
    /// Converts visible facility locations into normalised marker coordinates on the 2:1 world map; this type belongs to the official Godot presentation.
    /// </summary>
    public static class SiteMapProjector
    {
        /// <summary>
        /// 优先采用投影数据中的万分比坐标；缺失时按洲与设施编号确定性散开，刷新时不会跳动。
        /// Uses projected 0..10000 coordinates when present; otherwise scatters deterministically by continent and site number so refreshes never jump.
        /// </summary>
        /// <param name="continent">设施所属大洲。Facility continent.</param>
        /// <param name="siteNumber">设施编号，作为确定性散开输入。Facility number used for deterministic scatter.</param>
        /// <param name="mapX">横坐标万分比；0 表示缺失。Horizontal coordinate in 0..10000; 0 means absent.</param>
        /// <param name="mapY">纵坐标万分比；0 表示缺失。Vertical coordinate in 0..10000; 0 means absent.</param>
        /// <param name="x">输出 0..1 横坐标。Output normalised X.</param>
        /// <param name="y">输出 0..1 纵坐标。Output normalised Y.</param>
        public static void ResolveSiteAnchor(Continent continent, int siteNumber, int mapX, int mapY, out float x, out float y)
        {
            if (mapX > 0 && mapY > 0)
            {
                x = ClampMargin(mapX / 10000f);
                y = ClampMargin(mapY / 10000f);
                return;
            }

            ResolveContinentAnchor(continent, out float baseX, out float baseY);
            int slot = siteNumber < 0 ? -siteNumber : siteNumber;
            x = ClampMargin(baseX + (slot % 5 - 2) * 0.022f);
            y = ClampMargin(baseY + (slot / 5 % 5 - 2) * 0.030f);
        }

        /// <summary>返回各洲仅用于显示的代表锚点，不表示设施真实地理位置。Returns presentation-only continent anchors that never imply real facility geography.</summary>
        private static void ResolveContinentAnchor(Continent continent, out float x, out float y)
        {
            switch (continent)
            {
                case Continent.NorthAmerica: x = 0.22f; y = 0.32f; break;
                case Continent.SouthAmerica: x = 0.31f; y = 0.66f; break;
                case Continent.Europe: x = 0.50f; y = 0.27f; break;
                case Continent.Africa: x = 0.52f; y = 0.55f; break;
                case Continent.Asia: x = 0.70f; y = 0.33f; break;
                case Continent.Oceania: x = 0.83f; y = 0.72f; break;
                case Continent.Antarctica: x = 0.50f; y = 0.92f; break;
                default: x = 0.04f; y = 0.04f; break;
            }
        }

        /// <summary>把坐标夹到 0.02..0.98，避免 16px 标记被边界裁切。Clamps to 0.02..0.98 so 16px markers are not clipped.</summary>
        private static float ClampMargin(float value)
        {
            return value < 0.02f ? 0.02f : value > 0.98f ? 0.98f : value;
        }
    }
}
