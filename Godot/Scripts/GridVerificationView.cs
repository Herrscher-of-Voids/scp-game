namespace Scp.Godot
{
    using global::Godot;
    using Scp.Domain;

    /// <summary>
    /// 正式工程保留的网格渲染验证样本，不依赖 prototypes；用于检查 32px 网格、固定缩放和大批量绘制路径。
    /// Grid-rendering verification sample retained in the official project without prototype dependencies; checks the 32px grid, fixed zoom and bulk drawing path.
    /// </summary>
    /// <remarks>
    /// 人物色块仅用于测量大量实体同屏时的绘制成本，不是美术决定。正式人物必须使用像素精灵，并提供朝向与行走动画；本样本不会生成虚假占位图片。
    /// Personnel colour blocks only measure many-entity drawing cost and are not an art decision. Shipping characters must use pixel sprites with facing and walk animations; this sample generates no fake placeholder images.
    /// </remarks>
    public sealed partial class GridVerificationView : Control
    {
        /// <summary>验证网格列数，足以超出常见视口并触发可见区域剔除。Verification columns, large enough to exceed common viewports and exercise culling.</summary>
        private const int Columns = 240;
        /// <summary>验证网格行数。Verification rows.</summary>
        private const int Rows = 140;
        /// <summary>性能占位实体数，仅用于绘制预算验证。Performance-placeholder entity count used only for draw-budget verification.</summary>
        private const int PlaceholderEntityCount = 400;
        /// <summary>当前固定缩放档索引，默认 2× 管理档。Current fixed zoom index, defaulting to the 2x management tier.</summary>
        private int _zoomIndex = 1;

        /// <summary>铺满场景并请求初次绘制。Fills the scene and requests its first draw.</summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            ClipContents = true;
            QueueRedraw();
        }

        /// <summary>绘制可见网格分区与性能占位实体；所有坐标由固定公式导出，运行可复现。Draws visible grid zones and performance placeholders; all coordinates are deterministic.</summary>
        public override void _Draw()
        {
            int cell = ScpArtSpecification.GridPixelSize / ScpArtSpecification.ZoomTiers[_zoomIndex];
            DrawRect(new Rect2(Vector2.Zero, Size), GodotArt.Muted.Darkened(0.35f));
            int visibleColumns = Mathf.Min(Columns, Mathf.CeilToInt(Size.X / cell) + 1);
            int visibleRows = Mathf.Min(Rows, Mathf.CeilToInt(Size.Y / cell) + 1);

            for (int row = 0; row < visibleRows; row++)
            {
                for (int column = 0; column < visibleColumns; column++)
                {
                    Color floor = column < Columns / 3 ? GodotArt.FloorContainment : column >= Columns * 2 / 3 ? GodotArt.FloorResearch : GodotArt.FloorCommon;
                    DrawRect(new Rect2(column * cell, row * cell, cell, cell), floor);
                    if (row == 0 || column == 0 || row == Rows - 1 || column == Columns - 1)
                    {
                        DrawRect(new Rect2(column * cell, row * cell, cell, cell), GodotArt.Wall);
                    }
                }
            }

            for (int index = 0; index < PlaceholderEntityCount; index++)
            {
                int column = 2 + index * 7 % (Columns - 4);
                int row = 2 + index * 11 % (Rows - 4);
                if (column < visibleColumns && row < visibleRows)
                {
                    float inset = Mathf.Max(1, cell * 0.2f);
                    DrawRect(new Rect2(column * cell + inset, row * cell + inset, cell - inset * 2, cell - inset * 2), index % 40 == 0 ? GodotArt.Critical : GodotArt.Warning);
                }
            }

            DrawString(Theme.DefaultFont, new Vector2(12, 24), "GridMain 验证样本 · 人物色块仅为性能占位，正式采用像素精灵 / 方向 / 行走动画", HorizontalAlignment.Left, -1, 14, Colors.White);
        }

        /// <summary>滚轮只在 1×、2×、4× 三个固定档之间切换，避免半像素采样。The wheel switches only among fixed 1x, 2x and 4x tiers to avoid half-pixel sampling.</summary>
        /// <param name="@event">Godot GUI 输入事件。Godot GUI input event.</param>
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button && button.Pressed)
            {
                if (button.ButtonIndex == MouseButton.WheelUp)
                {
                    _zoomIndex = Mathf.Max(0, _zoomIndex - 1);
                    QueueRedraw();
                }
                else if (button.ButtonIndex == MouseButton.WheelDown)
                {
                    _zoomIndex = Mathf.Min(ScpArtSpecification.ZoomTiers.Length - 1, _zoomIndex + 1);
                    QueueRedraw();
                }
            }
        }
    }
}
