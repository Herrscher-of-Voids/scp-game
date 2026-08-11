namespace Scp.Godot
{
    using global::Godot;

    /// <summary>
    /// 中文：《SCP：常态的代价 / SCP: Necessary Measures》的独立制作人员、来源与许可档案页面。
    /// English: Standalone credits, sources and licences archive for "SCP: Necessary Measures".
    /// 控制对象：核心开发者署名、SCP 官方资料入口、具体条目发布边界、第三方资源、项目许可声明和负责人提供的繁体许可文本。
    /// Controlled objects: core developer credit, official SCP references, item-level publication boundary, third-party assets, project licence declaration and the Traditional Chinese licence text supplied by the project owner.
    /// 参数与单位：页面边距、分区间距和字号均为逻辑像素；正文宽度随视口缩放，纵向内容由滚动容器承载。
    /// Parameters and units: margins, section spacing and font sizes use logical pixels; body width follows the viewport and vertical overflow is handled by a scroll container.
    /// 返回值：按钮只产生场景切换或操作系统打开网址的界面副作用，不返回业务数据。
    /// Return value: buttons only cause scene changes or ask the operating system to open URLs and return no business data.
    /// 边界情况：未核实的具体 SCP 条目不展示猜测作者；外部网址打开失败时只显示错误，不改变许可文本或署名数据。
    /// Edge cases: unverified SCP items never display guessed authors; a failed external URL request only shows an error and never mutates licence or attribution data.
    /// 确定性与原因：全部署名和网址是已确认的固定文本，按固定顺序渲染，以便发布审查并清楚区分 CC BY-SA 3.0、GPLv3 与第三方许可。
    /// Determinism and rationale: all credits and URLs are confirmed constants rendered in a fixed order for release review and clear separation of CC BY-SA 3.0, GPLv3 and third-party licences.
    /// </summary>
    public sealed partial class CreditsScreen : Control
    {
        private const string MainScenePath = "res://Main.tscn";
        private const string CcBySaUrl = "https://creativecommons.org/licenses/by-sa/3.0/";
        private const string GplUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
        private const string FoundationLicensingGuideUrl = "http://scp-zh-tr.wikidot.com/licensing-guide";

        // 中文：每个二元组固定保存玩家可读类别和已确认网址；数组顺序就是页面显示顺序，不执行网络探测或来源推断。
        // English: Each tuple stores a player-facing category and confirmed URL; array order is display order, with no network probing or source inference.
        private static readonly (string Category, string Url)[] FoundationReferences =
        {
            ("SCP-CN 中文站", "https://scp-wiki-cn.wikidot.com/"),
            ("SCP-EN 英文站", "https://scp-wiki.wikidot.com/"),
            ("机动特遣队 / MTF", "https://scp-wiki-cn.wikidot.com/task-forces"),
            ("基金会设施", "https://scp-wiki-cn.wikidot.com/secure-facilities-locations"),
            ("人员及角色档案", "https://scp-wiki-cn.wikidot.com/personnel-and-character-dossier"),
            ("SCP 项目等级", "https://scp-wiki-cn.wikidot.com/object-classes"),
            ("相关组织", "https://scp-wiki-cn.wikidot.com/groups-of-interest"),
            ("部门", "https://scp-wiki-cn.wikidot.com/departments-complete-list"),
            ("世界线设定", "https://scp-wiki-cn.wikidot.com/canon-hub"),
            ("中国分部机动特遣队 / MTF", "https://scp-wiki-cn.wikidot.com/task-forces-cn"),
            ("中国分部设施", "https://scp-wiki-cn.wikidot.com/secure-facilities-locations-cn"),
            ("中国分部相关组织", "https://scp-wiki-cn.wikidot.com/groups-of-interest-cn")
        };

        private AudioManager _audio = null!;
        private Label _status = null!;

        /// <summary>
        /// 中文：初始化黑白档案主题并一次性建立可滚动页面；不读取存档、不联网，也不改写许可资料。
        /// English: Initializes the monochrome archive theme and builds the scrollable page once; it reads no saves, performs no networking and never rewrites licence data.
        /// </summary>
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            _audio = GetNode<AudioManager>("/root/AudioManager");
            Theme = CreateTheme();
            BuildUi();
            QueueRedraw();
        }

        /// <summary>
        /// 中文：以视口逻辑像素绘制纯黑背景；正文面板使用稍亮黑色以保持 1280×720 下的层级和白字对比。
        /// English: Draws a pure near-black background in viewport logical pixels; the slightly lighter body panel preserves hierarchy and white-text contrast at 1280×720.
        /// </summary>
        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color("050506"));

        /// <summary>
        /// 中文：创建固定标题、返回入口与全宽纵向滚动正文；所有长文本启用智能换行，横向滚动明确禁用。
        /// English: Creates the title, return entry and full-width vertically scrolling body; all long text uses smart wrapping and horizontal scrolling is explicitly disabled.
        /// </summary>
        private void BuildUi()
        {
            var margin = new MarginContainer
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetLeft = 34.0f,
                OffsetTop = 22.0f,
                OffsetRight = -34.0f,
                OffsetBottom = -22.0f
            };
            AddChild(margin);

            var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);

            var header = new HBoxContainer();
            var back = new Button { Text = "← 返回主标题", FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(190, 42) };
            _audio.BindButton(back);
            back.Pressed += ReturnToMainTitle;
            header.AddChild(back);
            header.AddChild(new Label
            {
                Text = "制作人员与许可 / CREDITS & LICENCES",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            });
            header.AddChild(new Control { CustomMinimumSize = new Vector2(190, 0) });
            root.AddChild(header);
            root.AddChild(new HSeparator());

            var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            root.AddChild(panel);
            var scroll = new ScrollContainer
            {
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            panel.AddChild(scroll);
            var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 10);
            scroll.AddChild(body);

            AddSection(body, "01 / 核心制作人员 · CORE DEVELOPMENT");
            AddBody(body, "开发者／策划／程序／美术整合：空之律者\n第三方作者与资源贡献者在各自来源条目中署名，不列入核心开发团队。");

            AddSection(body, "02 / 基金会通用资料 · OFFICIAL SCP REFERENCES");
            AddBody(body, "以下按资料类别列出官方页面入口，不以此列表替代具体作品的作者、原作与翻译署名。具体内容正式接入时仍须逐项核实。");
            foreach ((string category, string url) in FoundationReferences)
            {
                AddLink(body, category, url);
            }

            AddSection(body, "03 / 具体 SCP 条目 · ITEM-LEVEL ATTRIBUTION");
            AddNotice(body, "正式内容接入后逐项列出；未核实内容不得公开发布。\n当前没有作者、原作与翻译均已完整核实的正式使用清单，本页不猜测作者。");

            AddSection(body, "04 / 资源署名 · THIRD-PARTY ASSETS");
            AddAsset(body, "SCP 基金会标志", "原设计：far2\n高分辨率 PNG：Aelanna\nSVG 页面维护：BmboB\n许可：CC BY-SA 3.0\n本项目修改：生成白色显示派生版", "https://commons.wikimedia.org/wiki/File:SCP_Foundation_(emblem).svg");
            AddAsset(body, "Natural Earth 1:110m Physical Land", "提供者：Natural Earth\n许可：Public Domain\n本项目修改：生成地图 PNG", "https://www.naturalearthdata.com/downloads/110m-physical-vectors/110m-land/");
            AddAsset(body, "Kenney UI Audio", "作者／发布者：Kenney\n许可：CC0", "https://kenney.nl/assets/ui-audio");
            AddAsset(body, "lost in the unknown", "作者：johndekale\n许可：CC0", "https://opengameart.org/content/lost-in-the-unknown");

            AddSection(body, "05 / 许可区分与本项目正式声明 · LICENCE SEPARATION");
            AddBody(body, "SCP 衍生内容：CC BY-SA 3.0\n源代码：GNU General Public License v3.0（GPLv3）\n第三方资源：分别遵循各资源条目所列许可。");
            AddLink(body, "CC BY-SA 3.0 许可全文", CcBySaUrl);
            AddLink(body, "GPLv3 许可全文", GplUrl);
            AddNotice(body, "与 SCP 基金会相关内容和 Logo 使用 CC BY-SA 3.0。《SCP：常态的代价 / SCP: Necessary Measures》为相关内容的衍生作品，并以相同方式共享。\n\n本项目源代码采用 GPLv3；代码许可不替代 SCP 衍生内容必须遵守的 CC BY-SA 3.0。第三方资源继续适用各自许可。");

            AddSection(body, "06 / 基金会发行声明样板 · FOUNDATION RELEASE NOTICE TEMPLATES");
            AddBody(body, "以下繁体中文发行声明样板来源于 SCP 繁体中文站许可指南。本页只展示发行声明样板，不转载完整许可说明正文。");
            AddLink(body, "SCP 繁体中文站许可指南", FoundationLicensingGuideUrl);
            AddTraditionalLicenceTemplates(body);

            _status = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 26)
            };
            _status.AddThemeColorOverride("font_color", new Color("ffb8b8"));
            root.AddChild(_status);
        }

        /// <summary>
        /// 中文：只展示许可指南中的繁体发行声明样板，不转载游戏开发者说明正文；CC BY-SA 与 GPLv3 样板分开呈现，避免玩家误认为两者是同一许可证。
        /// English: Displays only the Traditional Chinese release-notice templates from the licensing guide and omits the game-developer guidance prose; CC BY-SA and GPLv3 templates remain separate so players do not mistake them for one licence.
        /// 参数与边界：body 是当前滚动页的纵向容器；模板文字保持负责人确认的繁体内容，仅修正排版和提供可点击网址。
        /// Parameters and boundaries: body is the current scroll page's vertical container; confirmed Traditional Chinese wording is retained, with only layout cleanup and clickable URLs added.
        /// </summary>
        private void AddTraditionalLicenceTemplates(VBoxContainer body)
        {
            AddBody(body, "發行聲明樣板：「與SCP基金會相關的內容，包含SCP基金會的Logo皆是以 創作共用 相同方式分享 3.0 進行授權，並且所有概念皆源自於 http://www.scp-wiki.net 及其作者群。[在此插入遊戲名稱]，為此些內容的衍生作品，特此同時將其以 創作共用 相同方式分享 3.0 進行發行。」");
            AddLink(body, "發行聲明樣板中的 SCP Wiki 網址", "http://www.scp-wiki.net");
            AddBody(body, "GPL發行聲明樣板：「（一行用來寫下程式的名字以及簡述其內容。） Copyright (C)（年份）（作者名字） 該程式為自由軟體：你可根據自由軟體基金會所發表之第三版或任何更新版本（根據你自己所選）的GNU通用公眾條款進行再分發和/或更改。該程式的發行希望是有益的，但不連帶有任何的保證；甚至沒有對其可批發性或可適配於特定用途性作出隱晦的保證。詳見GNU通用公眾條款以獲得更多詳細資訊。隨著該程式，您應該要一同收到一份GNU通用公眾條款的影本。若沒有，請見 http://www.gnu.org/licenses/ 。」");
            AddLink(body, "GPL 發行聲明樣板中的 GNU 許可網址", "http://www.gnu.org/licenses/");
        }

        /// <summary>
        /// 中文：添加分区标题和分隔线；标题字号为 21 逻辑像素，确保 1280×720 下仍可辨认层级。
        /// English: Adds a section heading and separator; the 21-logical-pixel heading keeps hierarchy legible at 1280×720.
        /// </summary>
        private static void AddSection(VBoxContainer body, string title)
        {
            var heading = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            heading.AddThemeFontSizeOverride("font_size", 21);
            heading.AddThemeColorOverride("font_color", Colors.White);
            body.AddChild(heading);
            body.AddChild(new HSeparator());
        }

        /// <summary>
        /// 中文：添加全宽智能换行正文；输入是固定显示文本，方法不解析 URL 或富文本标签，避免许可原文被解释或改写。
        /// English: Adds full-width smart-wrapped body copy; input is fixed display text and no URL or rich-text markup is parsed, preventing interpretation or rewriting of licence wording.
        /// </summary>
        private static void AddBody(VBoxContainer body, string text)
        {
            body.AddChild(new Label
            {
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            });
        }

        /// <summary>
        /// 中文：添加高对比发布边界或正式声明；只改变边框和内边距，不改变传入文本的法律含义。
        /// English: Adds a high-contrast publication boundary or formal declaration; only border and padding change, never the legal meaning of supplied text.
        /// </summary>
        private static void AddNotice(VBoxContainer body, string text)
        {
            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color("101013"),
                BorderColor = new Color("bfc0c6"),
                ContentMarginLeft = 14,
                ContentMarginRight = 14,
                ContentMarginTop = 12,
                ContentMarginBottom = 12
            };
            style.SetBorderWidthAll(1);
            panel.AddThemeStyleboxOverride("panel", style);
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeColorOverride("font_color", Colors.White);
            panel.AddChild(label);
            body.AddChild(panel);
        }

        /// <summary>
        /// 中文：添加第三方资源的独立署名块和来源按钮，保证作者、修改说明与许可不会混入核心团队。
        /// English: Adds an independent third-party attribution block and source button so authors, modifications and licences never merge into the core team.
        /// </summary>
        private void AddAsset(VBoxContainer body, string title, string details, string url)
        {
            AddBody(body, title + "\n" + details);
            AddLink(body, "来源 / SOURCE", url);
        }

        /// <summary>
        /// 中文：创建显示类别与完整网址的按钮，并交由 AudioManager 绑定统一点击和焦点反馈；网址本身不被缩写。
        /// English: Creates a button showing its category and complete URL and delegates click/focus feedback to AudioManager; the URL itself is never shortened.
        /// </summary>
        private void AddLink(VBoxContainer body, string category, string url)
        {
            var button = new Button
            {
                Text = category + "  ·  " + url,
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 38),
                TooltipText = "使用系统默认浏览器打开 / Open with the system default browser"
            };
            _audio.BindButton(button);
            button.Pressed += () => OpenUrl(url);
            body.AddChild(button);
        }

        /// <summary>
        /// 中文：请求操作系统以默认处理程序打开已确认 HTTPS 网址；返回值 Error.Ok 代表请求已交付，其他值在页脚报告。
        /// English: Asks the operating system to open a confirmed HTTPS URL with its default handler; Error.Ok means the request was handed off and other values are reported in the footer.
        /// </summary>
        private void OpenUrl(string url)
        {
            Error error = OS.ShellOpen(url);
            if (error != Error.Ok)
            {
                GD.PrintErr("Credits URL open failed: " + url + " error=" + error);
                _status.Text = "无法打开网址，请检查系统默认浏览器设置。";
            }
            else
            {
                _status.Text = "已交由系统打开：" + url;
            }
        }

        /// <summary>
        /// 中文：返回主标题独立场景；切换失败时保留当前档案页并显示可读错误，不卸载署名内容。
        /// English: Returns to the independent main-title scene; failure keeps this archive visible and shows a readable error without unloading credits.
        /// </summary>
        private void ReturnToMainTitle()
        {
            Error error = GetTree().ChangeSceneToFile(MainScenePath);
            if (error != Error.Ok)
            {
                GD.PrintErr("Credits main-title scene change failed: " + error);
                _status.Text = "无法返回主标题，请稍后重试。";
            }
        }

        /// <summary>
        /// 中文：创建支持简繁中文和英文的黑白主题；字体回退由系统字体列表处理，默认正文为 16 逻辑像素。
        /// English: Creates a monochrome theme supporting Simplified Chinese, Traditional Chinese and English; system-font fallback handles glyph coverage and body text defaults to 16 logical pixels.
        /// </summary>
        private static Theme CreateTheme()
        {
            var font = new SystemFont { FontNames = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Noto Sans CJK SC", "Noto Sans CJK TC" } };
            var theme = new Theme { DefaultFont = font, DefaultFontSize = 16 };
            theme.SetColor("font_color", "Label", new Color("d8d8dc"));
            theme.SetColor("font_color", "Button", new Color("d8d8dc"));
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_focus_color", "Button", Colors.White);
            theme.SetColor("font_pressed_color", "Button", new Color("a8a8ae"));
            return theme;
        }

        /// <summary>
        /// 中文：创建正文档案面板样式；边距单位为逻辑像素，单像素灰边在黑底上提供结构而不模拟彩色界面。
        /// English: Creates the archive body panel style; margins use logical pixels and a one-pixel grey border provides structure on black without introducing colour UI.
        /// </summary>
        private static StyleBoxFlat CreatePanelStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color("09090b"),
                BorderColor = new Color("4c4c52"),
                ContentMarginLeft = 18,
                ContentMarginRight = 18,
                ContentMarginTop = 16,
                ContentMarginBottom = 16
            };
            style.SetBorderWidthAll(1);
            return style;
        }
    }
}
