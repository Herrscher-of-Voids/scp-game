namespace Scp.Godot
{
    using global::Godot;
    using Scp.Domain;

    /// <summary>
    /// 把引擎无关美术规范转换为 Godot 类型的正式表现层转接器。
    /// Official presentation adapter converting engine-independent art specifications into Godot types.
    /// </summary>
    public static class GodotArt
    {
        /// <summary>纸面底色。Paper background colour.</summary>
        public static readonly Color Paper = ToColor(ScpArtSpecification.PaperBackground);
        /// <summary>正文墨色。Body ink colour.</summary>
        public static readonly Color Ink = ToColor(ScpArtSpecification.PaperBodyText);
        /// <summary>表格与面板边线色。Table and panel rule colour.</summary>
        public static readonly Color Rule = ToColor(ScpArtSpecification.PaperTableRule);
        /// <summary>印章与危急通知色。Stamp and critical-alert colour.</summary>
        public static readonly Color Stamp = ToColor(ScpArtSpecification.PaperStampRed);
        /// <summary>未知或次要文字色。Unknown or secondary text colour.</summary>
        public static readonly Color Muted = ToColor(ScpArtSpecification.StateUnknown);
        /// <summary>墙体色。Wall colour.</summary>
        public static readonly Color Wall = ToColor(ScpArtSpecification.Wall);
        /// <summary>普通地面色。Common floor colour.</summary>
        public static readonly Color FloorCommon = ToColor(ScpArtSpecification.FloorCommon);
        /// <summary>收容区地面色。Containment floor colour.</summary>
        public static readonly Color FloorContainment = ToColor(ScpArtSpecification.FloorContainment);
        /// <summary>研究区地面色。Research floor colour.</summary>
        public static readonly Color FloorResearch = ToColor(ScpArtSpecification.FloorResearch);
        /// <summary>正常状态色。Normal state colour.</summary>
        public static readonly Color Normal = ToColor(ScpArtSpecification.StateNormal);
        /// <summary>警告状态色。Warning state colour.</summary>
        public static readonly Color Warning = ToColor(ScpArtSpecification.StateWarning);
        /// <summary>危急状态色。Critical state colour.</summary>
        public static readonly Color Critical = ToColor(ScpArtSpecification.StateCritical);
        /// <summary>O5 黑色底板。O5 near-black background.</summary>
        public static readonly Color OverseerBackground = new Color("060607");
        /// <summary>O5 深灰面板。O5 dark panel fill.</summary>
        public static readonly Color OverseerPanel = new Color("101013");
        /// <summary>O5 浅灰边线。O5 light-grey rule.</summary>
        public static readonly Color OverseerRule = new Color("77777e");
        /// <summary>O5 正文白色。O5 body text white.</summary>
        public static readonly Color OverseerText = new Color("e4e4e8");
        /// <summary>O5 次要灰字。O5 secondary grey text.</summary>
        public static readonly Color OverseerMuted = new Color("96969e");
        /// <summary>中文：收入与改善使用的克制绿色。English: Restrained green for income and improvement.</summary>
        public static readonly Color Positive = new Color("57b87a");
        /// <summary>中文：中性数据与编辑焦点使用的青蓝。English: Cyan-blue for neutral data and edit focus.</summary>
        public static readonly Color Information = new Color("56a8c7");

        /// <summary>
        /// 以 8-bit 通道直接构造 Godot 颜色，避免在各视图重复十六进制值。
        /// Creates a Godot colour directly from 8-bit channels so views never duplicate hexadecimal values.
        /// </summary>
        /// <param name="color">引擎无关规范颜色。Engine-independent specification colour.</param>
        /// <returns>通道一致的 Godot 颜色。Godot colour with identical channels.</returns>
        public static Color ToColor(RgbaColor color)
        {
            return Color.Color8(color.R, color.G, color.B, color.A);
        }
    }
}
