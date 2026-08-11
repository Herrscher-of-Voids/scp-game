namespace Scp.Domain
{
    public enum IdentityRole
    {
        Overseer,
        SiteDirector,
        ResearchDirector,
        MtfCommander,
        EthicsMember,
        ClassD,
        Anomaly,
        // 中文：仅表示旧存档未记录身份，不授予任何身份权限；追加在末尾以保持现有枚举数值兼容。
        // English: Represents only a legacy save with no recorded identity and grants no role permissions; appended to preserve existing numeric values.
        Unknown
    }
}
