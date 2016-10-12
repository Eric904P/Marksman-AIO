#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Combo.cs" company="EloBuddy">
// 
// Marksman Master
// Copyright (C) 2016 by gero
// All rights reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/. 
// </copyright>
// <summary>
// 
// Email: geroelobuddy@gmail.com
// PayPal: geroelobuddy@gmail.com
// </summary>
// ---------------------------------------------------------------------
#endregion

using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Spells;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Varus.Modes
{
    internal class Combo : Varus
    {
        public static void Execute()
        {
            if (Settings.Combo.UseR && R.IsReady() && !IsPreAttack)
            {
                var possibleTargets =
                    EntityManager.Heroes.Enemies.Where(
                        x =>
                        {
                            if (x.IsValidTargetCached(R.Range) && (x.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamageCached(x, true)*2) && Player.Instance.IsInAutoAttackRange(x))
                                return false;

                            return x.IsValidTargetCached(R.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() &&
                                   (x.TotalHealthWithShields() < GetComboDamage(x));
                        }).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    var rPrediciton = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                    {
                        CollisionTypes = Prediction.Manager.PredictionSelected == "ICPrediction" ? new HashSet<CollisionType> { CollisionType.YasuoWall, CollisionType.AiHeroClient } : new HashSet<CollisionType> { CollisionType.ObjAiMinion },
                        Delay = .25f,
                        From = Player.Instance.Position,
                        Radius = R.Width,
                        Range = R.Range,
                        RangeCheckFrom = Player.Instance.Position,
                        Speed = R.Speed,
                        Target = target,
                        Type = SkillShotType.Linear
                    });
                    
                    if (rPrediciton.HitChancePercent >= 60)
                    {
                        R.Cast(rPrediciton.CastPosition);
                    }
                }
                else
                {
                    var t = StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero).FirstOrDefault(
                        x => x.IsValidTargetCached(R.Range) && !x.HasSpellShield() && (x.CountEnemiesInRangeCached(850) >= 3));

                    if (t != null)
                    {
                        var rPrediction = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                        {
                            CollisionTypes =
                                Prediction.Manager.PredictionSelected == "ICPrediction"
                                    ? new HashSet<CollisionType> {CollisionType.YasuoWall, CollisionType.AiHeroClient}
                                    : new HashSet<CollisionType> {CollisionType.ObjAiMinion},
                            Delay = .25f,
                            From = Player.Instance.Position,
                            Radius = R.Width,
                            Range = R.Range,
                            RangeCheckFrom = Player.Instance.Position,
                            Speed = R.Speed,
                            Target = t,
                            Type = SkillShotType.Linear
                        });

                        if (rPrediction.HitChancePercent >= 60)
                        {
                            R.Cast(rPrediction.CastPosition);
                        }
                    }
                }
            }

            if (Settings.Combo.UseE && E.IsReady() && !IsPreAttack)
            {
                if (StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero).Count(x => x.IsValidTargetCached(E.Range)) >= 2)
                {
                    E.CastIfItWillHit();
                }

                var possibleTargets = StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                    x => x.IsValidTargetCached(E.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() &&
                         (!Settings.Combo.UseEToProc || HasWDebuff(x) && GetWDebuff(x).Count == 3)).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    E.CastMinimumHitchance(target, 60);
                }
            }

            if (!Q.IsReady() || !Settings.Combo.UseQ)
                return;

            {
                var possibleTargets =
                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                        x =>
                            x.IsValidTargetCached(Q.IsCharging ? Q.Range : Q.MaximumRange) && !x.HasSpellShield() &&
                            !x.HasUndyingBuffA()).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    if (!Q.IsCharging && !IsPreAttack &&
                        (possibleTargets.Any(
                            x => (x.TotalHealthWithShields() < Damage.GetQDamage(x) + Damage.GetWDamage(x)) &&
                                 (Player.Instance.CountEnemyHeroesInRangeWithPrediction((int)Player.Instance.GetAutoAttackRange(), 350) <= 1)) ||
                         (Player.Instance.CountEnemyHeroesInRangeWithPrediction(Settings.Combo.QMinDistanceToTarget, 350) == 0)))
                    {
                        Q.StartCharging();
                        return;
                    }
                }

                if (!Q.IsCharging)
                    return;

                if (target != null)
                {
                    var damage = Damage.GetQDamage(target);

                    if (HasWDebuff(target) &&
                        (GetWDebuff(target).EndTime - Game.Time > 0.25f + Player.Instance.DistanceCached(target)/Q.Speed))
                        damage += Damage.GetWDamage(target);

                    var qPrediction = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                    {
                        CollisionTypes =
                            Prediction.Manager.PredictionSelected == "ICPrediction"
                                ? new HashSet<CollisionType> {CollisionType.YasuoWall}
                                : null,
                        Delay = 0,
                        From = Player.Instance.Position,
                        Radius = 70,
                        Range = Q.Range,
                        RangeCheckFrom = Player.Instance.Position,
                        Speed = Q.Speed,
                        Target = target,
                        Type = SkillShotType.Linear
                    });

                    if ((damage >= target.TotalHealthWithShields()) && (qPrediction.HitChance >= HitChance.Medium))
                    {
                        Q.Cast(qPrediction.CastPosition);
                    }
                    else if ((Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) != 0) ||
                             (Q.IsFullyCharged && (qPrediction.HitChancePercent >= 60)))
                    {
                        Q.Cast(qPrediction.CastPosition);
                    }
                }
                else if (Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange()) >= 1)
                {
                    var t =
                        StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTargetCached())
                            .OrderBy(x => x.DistanceCached(Player.Instance))
                            .FirstOrDefault();

                    if (t != null)
                    {
                        Q.CastMinimumHitchance(t, 50);
                    }
                }
            }
        }
    }
}