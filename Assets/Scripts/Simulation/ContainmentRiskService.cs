using System;

using Scp.Domain;

namespace Scp.Simulation
{
    /// <summary>
    /// 中文：M1 的引擎无关收容失效概率抽象。它不演算站点内部人员或战术路径，只把设施稳定度、收容预算覆盖、通用 trait 与世界确定性种子合成为每 Tick 的万分比风险，并在命中时推进失效阶段及产生战略事件。
    /// English: Engine-independent M1 containment-failure probability abstraction. It does not simulate site personnel or tactical paths; it combines facility stability, containment budget coverage, generic traits, and the world's deterministic seed into a per-tick per-ten-thousand risk, then advances the breach stage and emits a strategic event on a hit.
    /// </summary>
    public static class ContainmentRiskService
    {
        /// <summary>
        /// 中文：按固定的异常数组顺序处理一次 Tick。world 与 events 必须非 null；异常缺少设施时跳过，已到区域级时不再重复升级。随机流只在风险大于零时推进，保证相同输入与种子严格复现。
        /// English: Processes one tick in stable anomaly-array order. world and events must be non-null; anomalies without a site are skipped and regional breaches do not advance again. The random stream advances only for positive risk, guaranteeing exact replay for identical input and seed.
        /// </summary>
        public static void Process(WorldState world, IEventSink events)
        {
            foreach (var anomaly in world.Anomalies)
            {
                var site = FindSite(world, anomaly.SiteId);
                if (site == null || anomaly.BreachStage >= BreachStage.Regional)
                {
                    continue;
                }

                var risk = CalculatePerTickRisk(world, site, anomaly);
                if (risk == 0 || !world.RandomChance(risk))
                {
                    continue;
                }

                anomaly.BreachStage++;
                anomaly.Stability = Math.Max(1000, anomaly.Stability / 2);
                site.TrueStability = Math.Max(0, site.TrueStability - 500);
                events.Emit(new DomainEvent
                {
                    Kind = DomainEventKind.ContainmentBreach,
                    Tick = world.Tick,
                    ScpId = anomaly.Definition.Id,
                    Amount = (long)anomaly.BreachStage,
                    Detail = "Containment failure advanced to " + anomaly.BreachStage + " at site " + site.Id + "."
                });

                // 中文：M1 没有区域级战术演算，因此区域失效是概率抽象的真实终局，不允许事件发生后仍继续经营。
                // English: M1 has no regional tactical simulation, so a regional breach is the probability abstraction's real terminal state and play cannot continue after the event.
                if (anomaly.BreachStage == BreachStage.Regional)
                {
                    world.Failure.IsEnded = true;
                    world.Failure.EndReason = GameEndReason.KClassScenario;
                    return;
                }
            }
        }

        /// <summary>
        /// 中文：计算单个异常每 Tick 的失效概率，返回 0..10000 万分比。设施稳定度和异常自身稳定度越低风险越高；设施运营预算按设施均分后与该异常月维护费比较；高危/自适应/再生/升级 trait 提高风险，观察锁定且观察员充足时降低风险。该函数不取随机数，便于测试和投影解释。
        /// English: Calculates one anomaly's per-tick breach probability as 0..10000 per ten thousand. Lower facility and anomaly stability increase risk; site operations are split across sites and compared with the anomaly monthly cost; hazardous/adaptive/regenerating/escalating traits increase risk, while a properly staffed observation lock reduces it. This function draws no randomness, enabling tests and projection explanations.
        /// </summary>
        public static int CalculatePerTickRisk(WorldState world, SiteState site, AnomalyInstance anomaly)
        {
            // 中文：概率按小时 Tick 计，因此常态修正保持在个位万分比；严重失稳和设施损毁才提升到可快速兑现的量级。
            // English: Probability is evaluated hourly, so normal modifiers stay in single per-ten-thousand points; only severe instability and facility loss reach quickly realized levels.
            var siteDeficit = Math.Max(0, 7000 - site.TrueStability) / 700;
            var anomalyDeficit = Math.Max(0, 7000 - anomaly.Stability) / 700;
            var perSiteBudget = world.Sites.Length == 0 ? 0 : world.Economy.Budget.SiteOperations / world.Sites.Length;
            var requiredBudget = Math.Max(1, anomaly.Definition.Requirement.MonthlyCost);
            var budgetDeficit = perSiteBudget >= requiredBudget
                ? 0
                : (int)Math.Min(8L, (requiredBudget - perSiteBudget) * 8L / requiredBudget);
            var classRisk = anomaly.Definition.Class switch
            {
                ObjectClass.Safe => 0,
                ObjectClass.Euclid => 1,
                ObjectClass.Keter => 4,
                ObjectClass.Thaumiel => 2,
                ObjectClass.Apollyon => 12,
                _ => 0
            };
            var traitRisk = TraitRisk(anomaly, site);
            var facilityPenalty = anomaly.IsFacilityIntact && site.IsOperational ? 0 : 1000;
            return Clamp(siteDeficit + anomalyDeficit + budgetDeficit + classRisk + traitRisk + facilityPenalty);
        }

        /// <summary>
        /// 中文：把通用 trait 映射为收容风险修正，不按 SCP 编号硬编码。返回值单位为万分比点；负值只由满足观察条件的观察锁定 trait 产生，最终概率由调用者夹紧。
        /// English: Maps generic traits to containment-risk modifiers without SCP-number hardcoding. The result is in per-ten-thousand points; only a satisfied observation-lock trait yields a negative value, and the caller clamps the final probability.
        /// </summary>
        private static int TraitRisk(AnomalyInstance anomaly, SiteState site)
        {
            var risk = 0;
            foreach (var trait in anomaly.Definition.Traits)
            {
                switch (trait.Trait)
                {
                    case ScpTrait.ContAdaptive:
                    case ScpTrait.ContRegenerating:
                    case ScpTrait.ContReviving:
                    case ScpTrait.ContEscalating:
                    case ScpTrait.InfoCognitiveHazard:
                    case ScpTrait.InfoPropagating:
                        risk += 4;
                        break;
                    case ScpTrait.ActObservationLocked:
                        risk += site.AvailableObservers >= anomaly.ObserverCount && anomaly.IsObserved ? -3 : 8;
                        break;
                }
            }

            return risk;
        }

        private static SiteState? FindSite(WorldState world, SiteId siteId)
        {
            foreach (var site in world.Sites)
            {
                if (site.Id == siteId)
                {
                    return site;
                }
            }

            return null;
        }

        private static int Clamp(int value)
        {
            return value < 0 ? 0 : value > 10000 ? 10000 : value;
        }
    }
}
