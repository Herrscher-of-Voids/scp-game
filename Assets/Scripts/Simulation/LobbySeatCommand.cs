using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class LobbySeatCommand : ICommand
    {
        public SeatId SeatId { get; set; }

        public int SupportBonus { get; set; } = 40;

        /// <summary>
        /// 中文：为 true 时本次游说属于交换支持，立即获得支持修正并欠目标席位一票；为 false 时仅代表普通说服。债务在该 NPC 的提案表决时结算。
        /// English: When true, this lobby is a vote trade that grants the immediate support modifier and creates one vote debt to the target seat; false represents ordinary persuasion. The debt is settled when that NPC's proposal is voted on.
        /// </summary>
        public bool ExchangeSupport { get; set; }

        public ClearanceLevel RequiredClearance => ClearanceLevel.Level5;

        public ValidationResult Validate(IWorldQuery world)
        {
            var access = O5CommandValidation.Validate(world);
            if (!access.IsValid)
            {
                return access;
            }

            return world.IsNpcSeat(SeatId) && SupportBonus > 0
                ? ValidationResult.Success()
                : ValidationResult.Failure("An occupied NPC seat is required.");
        }

        public void Apply(WorldState world, IEventSink events)
        {
            foreach (var seat in world.Council.Seats)
            {
                if (seat.Id == SeatId)
                {
                    seat.LobbyBonus += SupportBonus;
                    // 中文：交换支持只建立一票债务；普通游说不产生未来承诺，避免同一命令隐式改变玩家义务。
                    // English: Vote trading creates exactly one vote debt; ordinary lobbying creates no future promise, avoiding implicit obligations from the same command.
                    if (ExchangeSupport)
                    {
                        seat.OwedSupportVotes++;
                    }

                    break;
                }
            }
        }
    }
}
