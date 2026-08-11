namespace Scp.Godot
{
    using System;
    using System.Globalization;
    using global::Godot;
    using Scp.Application;
    using Scp.Simulation;

    /// <summary>
    /// 正式 O5 世界地图控件，使用 res://Assets/Resources/UI/WorldMap.png 的导入纹理并叠加设施标记。
    /// Official O5 world-map control using the imported res://Assets/Resources/UI/WorldMap.png texture with facility markers overlaid.
    /// </summary>
    public sealed partial class WorldMapView : Control
    {
        /// <summary>正式底图资源路径；仓库根即 Godot 工程根，因此可由 ResourceLoader 可靠加载。Official map resource path, reliably loadable because the repository root is the Godot project root.</summary>
        private const string MapResourcePath = "res://Assets/Resources/UI/WorldMap.png";
        /// <summary>中文：项目负责人原创针形帷幕标记的透明位图路径；仅帷幕事件节点使用，设施标记保持原语义。English: Transparent bitmap path for the project-owner-authored veil pin; only veil incident nodes use it, while facility markers retain their existing semantics.</summary>
        private const string VeilMarkerResourcePath = "res://Assets/Resources/UI/VeilMapMarker.png";
        /// <summary>设施标记边长，单位屏幕像素。Facility marker edge in screen pixels.</summary>
        private const float MarkerSize = 16f;
        /// <summary>中文：帷幕针形标记的权威屏幕显示尺寸，单位像素；比例 18:28，不随地图缩放改变控件尺寸。English: Authoritative on-screen veil pin size in pixels; its 18:28 ratio is fixed and the control size does not scale with map zoom.</summary>
        private static readonly Vector2 VeilMarkerSize = new Vector2(18f, 28f);
        /// <summary>允许的最小地图缩放。Minimum allowed map zoom.</summary>
        private const float MinZoom = 0.6f;
        /// <summary>允许的最大地图缩放。Maximum allowed map zoom.</summary>
        private const float MaxZoom = 3f;

        /// <summary>底图节点，尺寸和平移由本控件统一计算。Map image whose size and pan are controlled here.</summary>
        private TextureRect _image = null!;
        /// <summary>设施标记容器，与底图共用相同画布变换。Marker layer sharing the map canvas transform.</summary>
        private Control _markers = null!;
        /// <summary>最近一次投影出的设施数据。Latest projected facility data.</summary>
        private SiteReportViewModel[] _sites = Array.Empty<SiteReportViewModel>();
        /// <summary>中文：最近一次帷幕事件投影；洲级节点使用固定洲中心而不伪造具体地点。English: Latest veil-incident projection; continent-only nodes use fixed continent centres without fabricating point locations.</summary>
        private VeilIncidentViewModel[] _veilIncidents = Array.Empty<VeilIncidentViewModel>();
        /// <summary>中文：玩家选择地图帷幕事件时传回稳定 ID。English: Emits the stable ID when the player selects a veil incident on the map.</summary>
        public event Action<string>? VeilIncidentSelected;
        /// <summary>当前缩放倍率。Current zoom multiplier.</summary>
        private float _zoom = 1f;
        /// <summary>当前平移偏移，单位屏幕像素。Current pan offset in screen pixels.</summary>
        private Vector2 _pan;
        /// <summary>左键是否处于地图拖动状态。Whether left-button map dragging is active.</summary>
        private bool _isPanning;

        /// <summary>创建底图和标记层，并验证正式资源可由 ResourceLoader 读取。Creates map and marker layers and verifies the official resource loads through ResourceLoader.</summary>
        public override void _Ready()
        {
            ClipContents = true;
            MouseFilter = MouseFilterEnum.Stop;

            _image = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_image);

            _markers = new Control { MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_markers);

            _image.Texture = ResourceLoader.Load<Texture2D>(MapResourcePath);
            if (_image.Texture == null)
            {
                GD.PrintErr("WorldMapView: ResourceLoader could not load " + MapResourcePath);
            }

            Resized += UpdateCanvas;
            // 中文：投影数据允许在控件进入场景树前写入；此处标记层已经存在，统一消费缓存并建立标记，避免 SetSites/SetVeilIncidents 在 _Ready 前解引用空节点。
            // English: Projection data may be assigned before the control enters the scene tree; the marker layer now exists, so consume cached data here and build markers without dereferencing pre-_Ready nodes in SetSites/SetVeilIncidents.
            RebuildMarkers();
        }

        /// <summary>替换设施投影并重建标记；null 输入按空集合处理。Replaces projected facilities and rebuilds markers; null is treated as empty.</summary>
        /// <param name="sites">当前 O5 可见设施。Facilities visible to the current O5 projection.</param>
        public void SetSites(SiteReportViewModel[]? sites)
        {
            _sites = sites ?? Array.Empty<SiteReportViewModel>();
            // 中文：调用方可以在控件进入场景树前提供设施投影；初始化前只缓存，_Ready 会在标记层创建后统一重建。
            // English: Callers may provide facility projection before the control enters the scene tree; cache before initialisation and let _Ready rebuild after the marker layer exists.
            if (_markers != null) RebuildMarkers();
        }

        /// <summary>
        /// 中文：切换到帷幕事件图层并重建可选标记。参数为 O5 已过滤事件；空数组显示无事件地图。洲级节点仅落在固定洲中心，近似/确认节点必须带非零万分比坐标才使用坐标，返回值通过 VeilIncidentSelected 事件传递稳定 ID。
        /// English: Switches to the selectable veil-incident layer and rebuilds markers. Input is the O5-filtered incident array; empty input shows a no-incident map. Continent-only nodes use fixed continent centres, while approximate/confirmed nodes require non-zero ten-thousandths coordinates; selection returns a stable ID through VeilIncidentSelected.
        /// </summary>
        public void SetVeilIncidents(VeilIncidentViewModel[]? incidents)
        {
            _sites = Array.Empty<SiteReportViewModel>();
            _veilIncidents = incidents ?? Array.Empty<VeilIncidentViewModel>();
            // 中文：Godot 在父布局尚未接入场景树时不会调用本控件 _Ready；该边界只缓存事件，初始化完成后或后续更新时才重建标记。
            // English: Godot does not invoke this control's _Ready while its parent layout remains outside the scene tree; this boundary caches incidents and rebuilds only after initialisation or on later updates.
            if (_markers != null) RebuildMarkers();
        }

        /// <summary>处理滚轮缩放与左键拖动；缩放被夹在 0.6..3.0。Handles wheel zoom and left-button dragging, clamped to 0.6..3.0.</summary>
        /// <param name="@event">Godot GUI 输入事件。Godot GUI input event.</param>
        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button)
            {
                if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
                {
                    _zoom = Mathf.Clamp(_zoom * 1.25f, MinZoom, MaxZoom);
                    UpdateCanvas();
                }
                else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
                {
                    _zoom = Mathf.Clamp(_zoom / 1.25f, MinZoom, MaxZoom);
                    UpdateCanvas();
                }
                else if (button.ButtonIndex == MouseButton.Left)
                {
                    _isPanning = button.Pressed;
                }
            }
            else if (@event is InputEventMouseMotion motion && _isPanning)
            {
                _pan += motion.Relative;
                UpdateCanvas();
            }
        }

        /// <summary>计算保持 2:1 的完整地图，并同步底图、标记层及标记位置；地图以 Fit 方式居中，绝不裁切世界边缘。Computes a complete 2:1 map and synchronises image, marker layer, and marker positions; the map is centred with Fit behaviour and never crops world edges.</summary>
        private void UpdateCanvas()
        {
            float width = Size.X <= 1f || Size.Y <= 1f ? 800f : Mathf.Min(Size.X, Size.Y * 2f);
            float height = width * 0.5f;
            var canvas = new Vector2(width, height) * _zoom;
            Vector2 offset = (Size - canvas) * 0.5f;
            _image.Position = offset + _pan;
            _image.Size = canvas;
            _markers.Position = offset + _pan;
            _markers.Size = canvas;
            RepositionMarkers(canvas);
        }

        /// <summary>释放旧标记并按最新投影创建新标记，避免刷新后重复节点。Frees old markers and creates markers from the latest projection to prevent duplicates.</summary>
        private void RebuildMarkers()
        {
            foreach (Node child in _markers.GetChildren())
            {
                _markers.RemoveChild(child);
                child.QueueFree();
            }

            foreach (SiteReportViewModel site in _sites)
            {
                // 中文：只有投影给出地图点的设施才创建标记。位置保密、仅有未确认大区、多地点或非地球的设施 MapX/MapY 为 0，
                // 若交给按洲散开的回退逻辑会把它们摆到一个真实大洲上，等于伪造地理位置，因此此处直接跳过，由设施列表按精度分级说明。
                // English: Markers are created only for facilities the projection actually places. Redacted, unconfirmed-region, multi-location and
                // non-terrestrial facilities carry MapX/MapY of zero; routing them through the continent-scatter fallback would put them on a real
                // continent and thereby fabricate geography, so they are skipped here and described by precision tier in the facility list instead.
                if (site.MapX <= 0 || site.MapY <= 0)
                {
                    continue;
                }

                _markers.AddChild(CreateMarker(site));
            }

            foreach (VeilIncidentViewModel incident in _veilIncidents)
            {
                foreach (VeilPropagationNodeViewModel node in incident.Nodes)
                {
                    _markers.AddChild(CreateVeilMarker(incident, node));
                }
            }

            UpdateCanvas();
        }

        /// <summary>创建带悬停信息的设施方框；突破、停运和正常状态使用不同规范色。Creates a tooltip marker, using distinct specification colours for breach, offline and normal states.</summary>
        /// <param name="site">设施投影。Facility projection.</param>
        /// <returns>已保存归一化锚点的标记控件。Marker control carrying its normalised anchor.</returns>
        private static Control CreateMarker(SiteReportViewModel site)
        {
            SiteMapProjector.ResolveSiteAnchor(site.Continent, site.SiteId.Value, site.MapX, site.MapY, out float x, out float y);
            Color colour = site.BreachingAnomalyCount > 0 ? GodotArt.Critical : site.IsOperational ? GodotArt.Ink : GodotArt.Muted;
            string code = site.Code.Length > 0 ? site.Code : "Site-" + site.SiteId.Value.ToString(CultureInfo.InvariantCulture);
            string location = site.LocationText.Length > 0 ? site.LocationText : "位置未详";

            var marker = new Panel
            {
                Size = new Vector2(MarkerSize, MarkerSize),
                MouseFilter = MouseFilterEnum.Pass,
                TooltipText = code + "\n" + location
            };
            var box = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.12f), BorderColor = colour };
            box.SetBorderWidthAll(2);
            marker.AddThemeStyleboxOverride("panel", box);
            marker.SetMeta("anchor", new Vector2(x, y));
            return marker;
        }

        /// <summary>
        /// 中文：建立帷幕传播节点按钮。洲级精度使用固定洲中心，表达“在该洲”而非具体地点；更高精度仅使用投影给出的坐标。按钮返回事件稳定 ID，严重度只改变规范色，不改变业务状态。
        /// English: Creates a veil propagation-node button. Continent precision uses a fixed continent centre to mean “within this continent,” not a point location; higher precision uses only projected coordinates. The button returns the incident stable ID, while severity changes colour only and never business state.
        /// </summary>
        private Control CreateVeilMarker(VeilIncidentViewModel incident, VeilPropagationNodeViewModel node)
        {
            Vector2 anchor = node.LocationPrecision == VeilLocationPrecision.ContinentOnly || node.MapX <= 0 || node.MapY <= 0
                ? ContinentAnchor(node.Continent)
                : new Vector2(node.MapX / 10000f, node.MapY / 10000f);
            var marker = new TextureButton { TextureNormal = ResourceLoader.Load<Texture2D>(VeilMarkerResourcePath), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale, Size = VeilMarkerSize, CustomMinimumSize = VeilMarkerSize, TooltipText = incident.Title + "\n" + node.Continent + " · " + node.LocationPrecision, MouseDefaultCursorShape = CursorShape.PointingHand };
            // 中文：严重度为 0..10000 万分比；0..2499 绿、2500..4999 黄、5000..7499 橙、7500..10000 红。输入越界先夹取，颜色只表达严重度，不改变图案、位置或业务状态。
            // English: Severity uses 0..10000 ten-thousandths: 0..2499 green, 2500..4999 yellow, 5000..7499 orange, and 7500..10000 red. Out-of-range input is clamped; colour expresses severity only and never changes shape, location, or business state.
            int severity = Math.Clamp(incident.Severity, 0, 10000);
            Color colour = severity < 2500 ? new Color("46c878") : severity < 5000 ? new Color("e0c84b") : severity < 7500 ? new Color("ed8b3a") : new Color("e05252");
            marker.Modulate = colour;
            marker.SetMeta("anchor", anchor);
            marker.Pressed += () => VeilIncidentSelected?.Invoke(incident.StableId);
            return marker;
        }

        /// <summary>中文：七洲固定地图中心仅用于洲级不确定标记，数值为地图归一化坐标且不表示真实事件地点。English: Fixed continent centres are used only for uncertain continent-level markers; values are normalised map coordinates and do not represent real incident locations.</summary>
        private static Vector2 ContinentAnchor(Scp.Domain.Continent continent) => continent switch
        {
            Scp.Domain.Continent.NorthAmerica => new Vector2(.20f, .35f), Scp.Domain.Continent.SouthAmerica => new Vector2(.31f, .68f),
            Scp.Domain.Continent.Europe => new Vector2(.50f, .32f), Scp.Domain.Continent.Asia => new Vector2(.68f, .38f),
            Scp.Domain.Continent.Africa => new Vector2(.52f, .58f), Scp.Domain.Continent.Oceania => new Vector2(.82f, .70f),
            _ => new Vector2(.52f, .90f)
        };

        /// <summary>把标记的 0..1 锚点映射到当前画布像素，并以标记中心对齐。Maps 0..1 anchors to canvas pixels and centres each marker.</summary>
        /// <param name="canvas">当前地图画布尺寸，单位像素。Current map canvas size in pixels.</param>
        private void RepositionMarkers(Vector2 canvas)
        {
            foreach (Node child in _markers.GetChildren())
            {
                if (child is Control marker && marker.HasMeta("anchor"))
                {
                    var anchor = (Vector2)marker.GetMeta("anchor");
                    marker.Position = new Vector2(anchor.X * canvas.X - marker.Size.X * 0.5f, anchor.Y * canvas.Y - marker.Size.Y * 0.5f);
                }
            }
        }
    }
}
