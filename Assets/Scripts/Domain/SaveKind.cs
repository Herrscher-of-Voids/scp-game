namespace Scp.Domain
{
    /// <summary>
    /// 中文：描述存档的用途；Unknown 用于无法可靠还原类型的旧档，Manual 是本次最小闭环创建的玩家存档。
    /// English: Describes a save's purpose; Unknown is for legacy data whose kind cannot be recovered reliably, while Manual is the player save created by this minimal loop.
    /// </summary>
    public enum SaveKind
    {
        Unknown,
        Manual,
        Auto
    }
}
