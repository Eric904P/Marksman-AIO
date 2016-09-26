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
                            x.IsValidTarget(R.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() &&
                            x.TotalHealthWithShields() < GetComboDamage(x) &&
                            x.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamage(x, true)*2 &&
                            R.GetPrediction(x).HitChancePercent >= 60).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    var rPrediciton = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                    {
                        CollisionTypes = new HashSet<CollisionType> { Prediction.Manager.PredictionSelected == "ICPrediction" ? CollisionType.AiHeroClient : CollisionType.ObjAiMinion },
                        Delay = 0.55f,
                        From = Player.Instance.Position,
                        Radius = 115,
                        Range = 1150,
                        RangeCheckFrom = Player.Instance.Position,
                        Speed = 1800,
                        Target = target,
                        Type = SkillShotType.Linear
                    }); 

                   // var rPrediction = R.GetPrediction(target);

                    if (rPrediciton.HitChancePercent >= 60)
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
                        var rPrediction = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                        {
                            CollisionTypes =
                                new HashSet<CollisionType>
                                {
                                    Prediction.Manager.PredictionSelected == "ICPrediction"
                                        ? CollisionType.AiHeroClient
                                        : CollisionType.ObjAiMinion
                                },
                            Delay = 0.55f,
                            From = Player.Instance.Position,
                            Radius = 115,
                            Range = 1150,
                            RangeCheckFrom = Player.Instance.Position,
                            Speed = 1800,
                            Target = t,
                            Type = SkillShotType.Linear
                        });

                        // var rPrediction = R.GetPrediction(t);

                        if (rPrediction.HitChancePercent >= 60)
                        {
                            R.Cast(rPrediction.CastPosition);
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
                        x =>
                            !x.IsDead && x.IsValidTarget(E.Range) && !x.HasSpellShield() && !x.HasUndyingBuffA() &&
                            (!Settings.Combo.UseEToProc || HasWDebuff(x) && GetWDebuff(x).Count == 3)).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    E.CastMinimumHitchance(target, 60);
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ)
            {
                var possibleTargets =
                    EntityManager.Heroes.Enemies.Where(
                        x =>
                            x.IsValidTarget(Q.IsCharging ? Q.Range : Q.MaximumRange) && !x.HasSpellShield() && !x.HasUndyingBuffA()).ToList();

                var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (target != null)
                {
                    if (!Q.IsCharging && !IsPreAttack &&
                        (possibleTargets.Any(
                            x => (x.TotalHealthWithShields() < Damage.GetQDamage(x) + Damage.GetWDamage(x)) &&
                                Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) <= 1) ||
                         Player.Instance.CountEnemiesInRange(Settings.Combo.QMinDistanceToTarget) == 0) && !IsPreAttack)
                    {
                        Q.StartCharging();
                        return;
                    }
                }

                if (Q.IsCharging)
                {
                    if (target != null)
                    {
                        var damage = Damage.GetQDamage(target);

                        if (HasWDebuff(target) &&
                            (GetWDebuff(target).EndTime - Game.Time > 0.25 + Player.Instance.Distance(target)/Q.Speed))
                            damage += Damage.GetWDamage(target);

                        var qPrediction = Q.GetPrediction(target);

                        if (damage >= target.TotalHealthWithShields() && qPrediction.HitChance >= HitChance.Medium)
                        {
                            Q.Cast(qPrediction.CastPosition);
                        }
                        else if (Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) != 0 ||
                                 Q.IsFullyCharged && qPrediction.HitChancePercent >= 60)
                        {
                            Q.Cast(qPrediction.CastPosition);
                        }
                    }
                    else if(Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) >= 1)
                    {
                        var t = EntityManager.Heroes.Enemies.OrderBy(x => x.Distance(Player.Instance)).FirstOrDefault();
                        if (t != null)
                        {
                            Q.CastMinimumHitchance(t, 50);
                        }
                    }
                }
            }
        }
    }
}