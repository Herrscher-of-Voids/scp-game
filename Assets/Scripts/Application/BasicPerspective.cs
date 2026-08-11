using System;
using System.Collections.Generic;

using Scp.Domain;
using Scp.Simulation;

namespace Scp.Application
{
    public sealed class BasicPerspective : IPerspective
    {
        public BasicPerspective(IdentityRole role, ClearanceLevel clearance)
        {
            Role = role;
            Clearance = clearance;
        }

        public IdentityRole Role { get; }

        public ClearanceLevel Clearance { get; }

        public TViewModel Project<TViewModel>(WorldState world)
        {
            if (typeof(TViewModel) != typeof(WorldViewModel))
            {
                throw new NotSupportedException(typeof(TViewModel).FullName);
            }

            var visible = new List<AnomalyViewModel>();
            if (Clearance >= ClearanceLevel.Level1)
            {
                foreach (var anomaly in world.Anomalies)
                {
                    visible.Add(new AnomalyViewModel
                    {
                        Id = anomaly.Definition.Id,
                        Class = anomaly.Definition.Class,
                        SiteId = anomaly.SiteId,
                        Stability = Clearance >= ClearanceLevel.Level3 ? anomaly.Stability : 0,
                        AccumulatedResource = Clearance >= ClearanceLevel.Level3
                            ? anomaly.AccumulatedResource
                            : 0
                    });
                }
            }

            foreach (var anomaly in world.Anomalies)
            {
                if (!anomaly.HasTrait(ScpTrait.InfoAntimemetic))
                {
                    continue;
                }

                visible.RemoveAll(item => item.Id == anomaly.Definition.Id);
            }

            object result = new WorldViewModel
            {
                Tick = world.Tick,
                Funds = world.Funds,
                EthicsScore = Clearance >= ClearanceLevel.Level4 ? world.EthicsScore : 0,
                Anomalies = visible.ToArray()
            };
            return (TViewModel)result;
        }

        public IReadOnlyList<CommandDescriptor> AvailableCommands(WorldState world)
        {
            if (Clearance < ClearanceLevel.Level4)
            {
                return Array.Empty<CommandDescriptor>();
            }

            return new[]
            {
                new CommandDescriptor
                {
                    Kind = CommandKinds.AdjustFunds,
                    RequiredClearance = ClearanceLevel.Level4
                }
            };
        }
    }
}
