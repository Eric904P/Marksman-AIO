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
                var possibleTargets = EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(R.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() && x.TotalHealthWithShields() < GetComboDamage(x) && x.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamage(x, true) * 2 && R.GetPrediction(x).HitChancePercent >= 70).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    var rPrediciton = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                    {
                        CollisionTypes = new HashSet<CollisionType> { CollisionType.ObjAiMinion },
                        Delay = 250,
                        From = Player.Instance.Position,
                        Radius = 115,
                        Range = R.Range,
                        RangeCheckFrom = Player.Instance.Position,
                        Speed = R.Speed,
                        Target = target,
                        Type = SkillShotType.Linear
                    });

                    if (rPrediciton.HitChancePercent >= 70)
                    {
                        R.Cast(rPrediciton.CastPosition);
                    }
                }
                else
                {
                    var t = EntityManager.Heroes.Enemies.FirstOrDefault(
                            x => x.IsValidTarget(R.Range) && !x.HasSpellShield() && x.CountEnemiesInRange(850) >= 3);

                    if (t != null)
                    {
                        var rPrediciton = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                        {
                            CollisionTypes = new HashSet<CollisionType> { CollisionType.ObjAiMinion },
                            Delay = 250,
                            From = Player.Instance.Position,
                            Radius = 115,
                            Range = R.Range,
                            RangeCheckFrom = Player.Instance.Position,
                            Speed = R.Speed,
                            Target = t,
                            Type = SkillShotType.Linear
                        });

                        if (rPrediciton.HitChancePercent >= 70)
                        {
                            R.Cast(rPrediciton.CastPosition);
                        }
                    }
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ)
            {
                var possibleTargets =
                    EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.MaximumRange) && !x.HasSpellShield() && !x.HasUndyingBuffA() && Q.GetPrediction(x).HitChance != HitChance.Impossible).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    if (!Q.IsCharging &&
                        (possibleTargets.Any(
                            x =>
                                x.IsValidTarget(Settings.Combo.QMinDistanceToTarget) &&
                                x.TotalHealthWithShields() < Damage.GetQDamage(x) + Damage.GetWDamage(x)) ||
                         !possibleTargets.Any(x => x.IsValidTarget(Settings.Combo.QMinDistanceToTarget))) && !IsPreAttack)
                    {
                        Q.StartCharging();
                        return;
                    }

                    if (Q.IsCharging)
                    {
                        var damage = Damage.GetQDamage(target);

                        if(HasWDebuff(target) && (GetWDebuff(target).EndTime - Game.Time > 0.25 + Player.Instance.Distance(target)/Q.Speed))
                            damage += Damage.GetWDamage(target);

                        if (damage >= target.TotalHealthWithShields())
                        {
                            Q.CastMinimumHitchance(target, HitChance.Medium);
                        }
                        else if (Q.IsFullyCharged)
                        {
                            var qPrediction = Q.GetPrediction(target);
                            if (qPrediction.HitChance == HitChance.Impossible &&
                                Player.Instance.CountEnemiesInRange(400) > 1)
                            {
                                Q.Cast(
                                    EntityManager.Heroes.Enemies.OrderBy(x => x.Distance(Player.Instance))
                                        .FirstOrDefault());
                                return;
                            }
                            Q.CastMinimumHitchance(target, 70);
                        }
                    }
                }
            }

            if (Settings.Combo.UseE && E.IsReady() && !IsPreAttack)
            {
                if (EntityManager.Heroes.Enemies.Count(x => x.IsValidTarget(E.Range)) >= 2)
                {
                    E.CastIfItWillHit();
                }

                var possibleTargets =
                    EntityManager.Heroes.Enemies.Where(
                        x => !x.IsDead && x.IsValidTarget(E.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() && (!Settings.Combo.UseEToProc || HasWDebuff(x) && GetWDebuff(x).Count == 3)).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    E.CastMinimumHitchance(target, 70);
                }
            }
        }
    }
}