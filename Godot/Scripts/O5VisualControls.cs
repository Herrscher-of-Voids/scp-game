namespace Scp.Godot
{
    using System;
    using global::Godot;

    /// <summary>
    /// 中文：财政数据墙只绘制应用层提供的上一周期收入、支出、净流量和当前资金；金额单位为基金会货币单位，缺失预测绝不补点。
    /// English: The finance wall draws only application-provided last income, expenses, cash flow, and current funds in Foundation currency units; unavailable forecasts are never invented.
    /// 边界/确定性：零值使用固定最小标尺，所有几何由四个输入确定；控件无业务返回值。
    /// Edge/determinism: zero values use a fixed minimum scale and all geometry is determined by the four inputs; the control returns no business state.
    /// </summary>
    public sealed partial class FinanceWallView : Control
    {
        private long _income;
        private long _expenses;
        private long _cashFlow;
        private long _funds;

        /// <summary>中文：替换真实财政快照并请求重绘。English: Replaces the real finance snapshot and queues a redraw.</summary>
        public void SetData(long income, long expenses, long cashFlow, long funds)
        {
            _income = income; _expenses = expenses; _cashFlow = cashFlow; _funds = funds; QueueRedraw();
        }

        /// <summary>中文：以灰度柱和折线绘制四项已知值；红色只标负现金流。English: Draws four known values as grayscale bars and a line; red is reserved for negative cash flow.</summary>
        public override void _Draw()
        {
            Vector2 size = Size;
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.025f, 0.03f, 0.035f, 0.9f));
            for (int i = 1; i < 5; i++) DrawLine(new Vector2(0, size.Y * i / 5f), new Vector2(size.X, size.Y * i / 5f), new Color(1, 1, 1, 0.09f), 1);
            long[] values = { _income, _expenses, Math.Abs(_cashFlow), Math.Max(0, _funds) };
            long maximum = 1; foreach (long value in values) maximum = Math.Max(maximum, value);
            float step = size.X / 5f;
            var points = new Vector2[4];
            for (int i = 0; i < values.Length; i++)
            {
                float height = (float)(values[i] / (double)maximum) * Math.Max(1f, size.Y - 26f);
                float x = step * (i + 0.65f);
                Color color = i == 2 && _cashFlow < 0 ? new Color(0.72f, 0.08f, 0.08f, 0.95f) : new Color(0.72f - i * 0.1f, 0.72f - i * 0.1f, 0.72f - i * 0.1f, 0.9f);
                DrawRect(new Rect2(x, size.Y - height, step * 0.55f, height), color);
                points[i] = new Vector2(x + step * 0.275f, size.Y - height);
            }
            for (int i = 1; i < points.Length; i++) DrawLine(points[i - 1], points[i], new Color(0.95f, 0.95f, 0.95f, 0.9f), 2);
        }
    }

    /// <summary>
    /// 中文：七洲帷幕态势控件按真实万分比完整度绘制非地理区域节点；最低完整度节点可脉冲，但明确属于完整度警示而非事件定位。
    /// English: Seven-region veil control draws non-geographic nodes from real ten-thousandth integrity values; the lowest node may pulse strictly as an integrity warning, never as an event location.
    /// 参数/边界：数组索引对应七洲，短数组按无数据处理；动画相位只影响视觉，不写回模拟层。
    /// Parameters/edges: array indices map to seven continents and short arrays mean unavailable data; animation phase is visual only and never writes simulation state.
    /// </summary>
    public sealed partial class VeilSituationView : Control
    {
        private int[] _values = Array.Empty<int>();
        private float _phase;
        public bool ReducedMotion { get; set; }

        public void SetData(int[] values) { _values = values ?? Array.Empty<int>(); QueueRedraw(); }
        public override void _Process(double delta) { if (!ReducedMotion) { _phase += (float)delta; QueueRedraw(); } }

        /// <summary>中文：绘制抽象世界轮廓、七节点及最低值红色状态环，不绘制伪造传播线。English: Draws an abstract world silhouette, seven nodes, and a red status ring at the lowest value without fabricated propagation links.</summary>
        public override void _Draw()
        {
            Vector2 s = Size;
            DrawRect(new Rect2(Vector2.Zero, s), new Color(0.02f, 0.025f, 0.03f, 0.82f));
            Vector2[] outline = { new(0.05f*s.X,0.45f*s.Y),new(0.2f*s.X,0.25f*s.Y),new(0.36f*s.X,0.42f*s.Y),new(0.5f*s.X,0.28f*s.Y),new(0.72f*s.X,0.32f*s.Y),new(0.92f*s.X,0.52f*s.Y),new(0.72f*s.X,0.72f*s.Y),new(0.48f*s.X,0.62f*s.Y),new(0.25f*s.X,0.76f*s.Y) };
            for (int i=1;i<outline.Length;i++) DrawLine(outline[i-1],outline[i],new Color(1,1,1,0.18f),2);
            Vector2[] anchors = { new(.18f*s.X,.38f*s.Y),new(.28f*s.X,.68f*s.Y),new(.48f*s.X,.36f*s.Y),new(.68f*s.X,.42f*s.Y),new(.5f*s.X,.62f*s.Y),new(.82f*s.X,.67f*s.Y),new(.55f*s.X,.84f*s.Y) };
            int lowest = -1; int lowValue = int.MaxValue;
            for (int i=0;i<7;i++) if (i<_values.Length && _values[i]<lowValue) { lowValue=_values[i]; lowest=i; }
            for (int i=0;i<anchors.Length;i++)
            {
                float integrity = i<_values.Length ? Mathf.Clamp(_values[i]/10000f,0,1) : 0;
                DrawCircle(anchors[i],6,new Color(integrity,integrity,integrity,1));
                if (i==lowest) DrawArc(anchors[i],ReducedMotion?12:12+4*Mathf.Sin(_phase*4),0,Mathf.Tau,32,new Color(.8f,.05f,.05f,.9f),2);
            }
        }
    }

    /// <summary>
    /// 中文：会议全景资源不可用时绘制十三席圆桌轮廓；席位几何只表达公开编号与占用关系，不生成容貌、性别或隐藏状态。
    /// English: Draws the thirteen-seat round-table silhouette when the panorama asset is unavailable; seat geometry expresses only public numbering and occupancy, never appearance, gender, or hidden state.
    /// 参数/边界：SeatCount 为可见总席数，通常为 13；小于 1 时只绘制桌面。动画相位单位为现实秒，只驱动方格噪声且不返回业务值。
    /// Parameters/edges: SeatCount is the visible seat total, normally 13; values below one draw only the table. Phase uses real seconds and drives square noise only, returning no business value.
    /// </summary>
    public sealed partial class CouncilPanoramaView : Control
    {
        public int SeatCount { get; set; } = 13;
        public bool ReducedMotion { get; set; }
        private float _phase;

        public override void _Process(double delta) { if (!ReducedMotion) { _phase += (float)delta; QueueRedraw(); } }
        public override void _Draw()
        {
            Vector2 s = Size; Vector2 center = new(s.X * .5f, s.Y * .57f);
            DrawRect(new Rect2(Vector2.Zero, s), new Color(.018f, .02f, .023f, 1));
            DrawEllipse(center, new Vector2(s.X * .34f, s.Y * .25f), new Color(.11f, .11f, .12f), new Color(.62f, .62f, .64f));
            int count = Math.Max(0, SeatCount);
            for (int i = 0; i < count; i++)
            {
                float angle = -Mathf.Pi * .92f + i * Mathf.Tau / Math.Max(1, count);
                Vector2 position = center + new Vector2(Mathf.Cos(angle) * s.X * .4f, Mathf.Sin(angle) * s.Y * .34f);
                DrawCircle(position, 18, new Color(.025f, .025f, .028f));
                DrawArc(position, 20, 0, Mathf.Tau, 20, new Color(.7f, .7f, .72f), 2);
                for (int block = 0; block < 5; block++)
                {
                    int seed = i * 37 + block * 19 + (ReducedMotion ? 0 : (int)(_phase * 8));
                    float bx = position.X - 13 + seed % 23; float by = position.Y - 10 + (seed / 7) % 18;
                    DrawRect(new Rect2(bx, by, 4 + seed % 4, 3 + (seed / 3) % 4), new Color(.75f, .75f, .77f, .7f));
                }
            }
        }

        /// <summary>中文：用两层椭圆近似桌面，避免依赖缺失位图。English: Approximates the tabletop with two ellipse polygons without depending on a missing bitmap.</summary>
        private void DrawEllipse(Vector2 center, Vector2 radius, Color fill, Color outline)
        {
            const int segments = 64; var points = new Vector2[segments];
            for (int i = 0; i < segments; i++) { float angle = i * Mathf.Tau / segments; points[i] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y); }
            DrawColoredPolygon(points, fill); for (int i = 0; i < segments; i++) DrawLine(points[i], points[(i + 1) % segments], outline, 2);
        }
    }

    /// <summary>
    /// 中文：使用统一黑西装轮廓、席位编号和动态乱码方格绘制匿名发言者半身；所有席位共享同一几何基础，仅确定性噪声种子不同。
    /// English: Draws an anonymous speaker bust from one black-suit silhouette, seat number, and dynamic static squares; every seat shares identical geometry and differs only by deterministic noise seed.
    /// </summary>
    public sealed partial class AnonymousSpeakerView : Control
    {
        public int SeatNumber { get; set; } = 1;
        public bool ReducedMotion { get; set; }
        private float _phase;
        public override void _Process(double delta) { if (!ReducedMotion) { _phase += (float)delta; QueueRedraw(); } }
        public override void _Draw()
        {
            Vector2 s = Size; DrawRect(new Rect2(Vector2.Zero, s), new Color(.015f, .017f, .02f));
            Vector2 head = new(s.X * .5f, s.Y * .3f); DrawCircle(head, Math.Min(s.X, s.Y) * .13f, new Color(.04f, .04f, .045f));
            var torso = new Vector2[] { new(s.X*.2f,s.Y*.92f),new(s.X*.3f,s.Y*.48f),new(s.X*.5f,s.Y*.42f),new(s.X*.7f,s.Y*.48f),new(s.X*.8f,s.Y*.92f) };
            DrawColoredPolygon(torso, new Color(.025f,.025f,.03f));
            for (int block=0;block<28;block++) { int seed=SeatNumber*97+block*43+(ReducedMotion?0:(int)(_phase*12)); float x=head.X-s.X*.14f+(seed%101)/100f*s.X*.28f; float y=head.Y-s.Y*.11f+((seed/11)%101)/100f*s.Y*.22f; float edge=3+seed%8; DrawRect(new Rect2(x,y,edge,edge),new Color(.52f+seed%4*.1f,.52f+seed%4*.1f,.54f+seed%4*.1f,.82f)); }
            DrawString(ThemeDB.FallbackFont,new Vector2(14,s.Y-16),"O5-"+SeatNumber,HorizontalAlignment.Left,-1,22,new Color(.82f,.82f,.84f));
        }
    }

    /// <summary>
    /// 中文：业务成功后的短时覆盖动画；类型决定授权印章或红笔批注，关闭动画时保持最终反馈后短暂移除。
    /// English: Short overlay feedback after business success; kind selects authorization stamp or red annotation, and reduced motion still preserves the final feedback briefly.
    /// </summary>
    public sealed partial class DecisionOverlay : Label
    {
        /// <summary>中文：显示指定反馈；duration 单位为现实秒。English: Shows the requested feedback; duration is measured in real seconds.</summary>
        public void Play(string text, bool red, bool reducedMotion, double duration = 0.25)
        {
            Text = text; HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
            AddThemeFontSizeOverride("font_size", 28); AddThemeColorOverride("font_color", red ? new Color(.75f,.04f,.04f) : new Color(.12f,.12f,.12f));
            Modulate = new Color(1,1,1,reducedMotion?1:0); Scale = reducedMotion ? Vector2.One : new Vector2(1.35f,1.35f); Show();
            Tween tween = CreateTween(); tween.SetParallel(); tween.TweenProperty(this,"modulate:a",1.0,duration); tween.TweenProperty(this,"scale",Vector2.One,duration);
            tween.Chain().TweenInterval(reducedMotion?0.45:0.7); tween.Chain().TweenCallback(Callable.From(Hide));
        }
    }
}
