using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class CouncilSeatState
    {
        public SeatId Id { get; set; }

        public bool IsOccupied { get; set; }

        public bool IsPlayer { get; set; }

        public AxisPosition Position { get; set; }

        public int Relationship { get; set; }

        public int Pressure { get; set; }

        public int LobbyBonus { get; set; }

        /// <summary>
        /// 中文：玩家以交换支持方式欠该席位的票数。每当玩家在该席位提交的议案上投赞成票时偿还一票；违约结算会降低关系。单位为票，最小值为 0。
        /// English: Number of support votes the player owes this seat through vote trading. One debt is repaid whenever the player supports a proposal submitted by this seat; settlement of a broken promise lowers relationship. Unit is votes, minimum zero.
        /// </summary>
        public int OwedSupportVotes { get; set; }

        public int VetoCooldown { get; set; }
    }
}
