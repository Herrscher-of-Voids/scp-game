using System;
using System.Collections.Generic;
using Scp.Domain;

namespace Scp.Simulation
{
    public static class ProposalResolver
    {
        public static int RequiredVotes(ProposalThreshold threshold)
        {
            return threshold switch
            {
                ProposalThreshold.SimpleMajority => 7,
                ProposalThreshold.TwoThirds => 9,
                ProposalThreshold.Unanimous => 13,
                _ => throw new ArgumentOutOfRangeException(nameof(threshold))
            };
        }

        public static VoteRecord Resolve(WorldState world, ProposalState proposal)
        {
            var votes = new List<SeatVoteRecord>();
            var supportCount = 0;
            foreach (var seat in world.Council.Seats)
            {
                if (!seat.IsOccupied)
                {
                    continue;
                }

                var choice = seat.IsPlayer ? proposal.PlayerVote : ResolveNpcVote(world, seat, proposal);
                votes.Add(new SeatVoteRecord { SeatId = seat.Id, Choice = choice });
                if (choice == VoteChoice.Support)
                {
                    supportCount++;
                }
            }

            var passed = supportCount >= RequiredVotes(proposal.Threshold);
            proposal.IsResolved = true;
            proposal.Passed = passed;
            // 中文：失败议案从结案周期起锁定三个完整周期；同一议题最早在 CurrentCycle + 3 重提。实质修改由坐标变化表示，不受此记录限制。
            // English: A rejected proposal is locked for three complete cycles from resolution; the identical issue may return at CurrentCycle + 3. A material amendment is represented by changed axes and is not blocked by this record.
            proposal.ResubmitAvailableCycle = passed ? 0 : world.Council.CurrentCycle + 3;
            SettleVoteTrade(world, proposal);
            ApplyOutcome(world, proposal);
            var record = new VoteRecord
            {
                ProposalId = proposal.ProposalId,
                Kind = proposal.Kind,
                Threshold = proposal.Threshold,
                Cycle = world.Council.CurrentCycle,
                Passed = passed,
                Votes = votes.ToArray()
            };
            var records = new List<VoteRecord>(world.Council.VoteRecords) { record };
            world.Council.VoteRecords = records.ToArray();
            foreach (var seat in world.Council.Seats)
            {
                seat.LobbyBonus = 0;
            }

            return record;
        }

        public static VoteChoice ResolveNpcVote(WorldState world, CouncilSeatState seat, ProposalState proposal)
        {
            var distance = seat.Position.DistanceTo(proposal.Position);
            var crisis = (10000 - world.Veil.Global) / 100 + Math.Max(0, -world.Economy.LastCashFlow / 100000);
            var score = 120 - distance + seat.Relationship / 4 + seat.LobbyBonus + seat.Pressure + crisis;
            return score >= 0 ? VoteChoice.Support : VoteChoice.Oppose;
        }

        /// <summary>
        /// 中文：结算玩家对 NPC 提案人的交换支持承诺。玩家投赞成即偿还一票；弃权或反对视为违约，关系永久降低 30，债务仍清除以避免重复处罚。玩家提案和无债务席位不受影响。
        /// English: Settles the player's traded-support promise to an NPC proposer. Supporting repays one debt; abstaining or opposing breaches it, permanently reducing relationship by 30, while clearing the debt to avoid repeated punishment. Player proposals and seats without debt are unaffected.
        /// </summary>
        private static void SettleVoteTrade(WorldState world, ProposalState proposal)
        {
            foreach (var seat in world.Council.Seats)
            {
                if (seat.Id != proposal.SubmittedBy || seat.IsPlayer || seat.OwedSupportVotes <= 0)
                {
                    continue;
                }

                if (proposal.PlayerVote != VoteChoice.Support)
                {
                    seat.Relationship -= 30;
                }

                seat.OwedSupportVotes--;
                return;
            }
        }

        private static void ApplyOutcome(WorldState world, ProposalState proposal)
        {
            if (!proposal.Passed)
            {
                return;
            }

            if (proposal.Kind == ProposalKind.LiftContactRestriction)
            {
                world.Council.ContactRestrictionActive = false;
                world.EthicsScore -= 5;
                world.Failure.HiddenEthicsRemovalRisk += 8;
            }
            else if (proposal.Kind == ProposalKind.Impeachment)
            {
                world.Failure.IsEnded = true;
                world.Failure.EndReason = GameEndReason.Impeached;
            }
            else if (proposal.Kind == ProposalKind.WorldRestart)
            {
                world.Failure.IsEnded = true;
                world.Failure.EndReason = GameEndReason.WorldRestarted;
                world.Facts.WorldWasRestarted = true;
            }
            else if (proposal.Kind == ProposalKind.AlphaOneDeployment)
            {
                world.Council.AlphaOne.IsDeployed = true;
                world.Council.AlphaOne.Deployments++;
                world.Facts.AlphaOneDeployments++;
            }
        }
    }
}
