namespace Scp.Domain
{
    /// <summary>
    /// 中文：每个存档持久化的难度选择；Unknown 仅表示旧档没有记录玩家原始选择，不能被解释为任一实际难度。
    /// English: Difficulty persisted per save; Unknown only means a legacy save did not record the player's original choice and must not be interpreted as a real difficulty.
    /// </summary>
    public enum GameDifficulty
    {
        Unknown,
        Easy,
        Normal,
        Hard,
        Realistic
    }
}
