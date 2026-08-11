using System;
using Newtonsoft.Json;

namespace Scp.Application
{
    /// <summary>
    /// 中文：终局档内可长期查看的结构化尾声；固定三部分分别记录结果、任期影响和封存状态。
    /// English: Structured epilogue retained for long-term viewing in ended saves; exactly three sections record outcome, tenure impact, and archive status.
    /// </summary>
    public sealed class EpilogueReport
    {
        [JsonProperty("isAvailable")]
        public bool IsAvailable { get; set; }

        [JsonProperty("sections")]
        public EpilogueSection[] Sections { get; set; } = Array.Empty<EpilogueSection>();
    }

    public sealed class EpilogueSection
    {
        [JsonProperty("kind")]
        public EpilogueSectionKind Kind { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("body")]
        public string Body { get; set; } = string.Empty;
    }

    public enum EpilogueSectionKind
    {
        Outcome,
        Legacy,
        Archive
    }
}
