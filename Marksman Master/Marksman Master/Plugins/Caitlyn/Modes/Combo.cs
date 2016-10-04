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
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Caitlyn.Modes
{
    internal class Combo : Caitlyn
    {
        public static void Execute()
        {
            if (Settings.Combo.UseQ && !IsPreAttack && Q.IsReady() && !Player.Instance.Position.IsVectorUnderEnemyTower() && !HasAutoAttackRangeBuffOnChamp)
            {
                if (!EntityManager.Heroes.Enemies.Any(x=> x.IsValidTarget() && Player.Instance.IsInRangeCached(x, BasicAttackRange)))
                {
                    var possibleTargets =
                        EntityManager.Heroes.Enemies.Where(
                            x => x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield());
                    
                    var qTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                    if (qTarget != null)
                    {
                        Q.CastMinimumHitchance(qTarget, HitChance.High);
                    }
                }
            }

            if (Settings.Combo.UseW && W.IsReady() && !IsPreAttack)
            {
                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(
                           x => x.IsValidTarget(700) && !x.HasUndyingBuffA() && !x.HasSpellShield() && !x.Position.IsVectorUnderEnemyTower());

                if (EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget() && x.IsMelee && Player.Instance.IsInRangeCached(x, BasicAttackRange) && x.IsMovingTowards(Player.Instance, 400)))
                {
                    W.Cast(Player.Instance.ServerPosition);
                }

                var wTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (wTarget != null)
                {
                    var wPrediction = W.GetPrediction(wTarget);

                    if (wPrediction.HitChancePercent >= Settings.Combo.WHitChancePercent && (wPrediction.CastPosition.Distance(wTarget) > 100))
                    {
                        W.Cast(wPrediction.CastPosition);
                    }
                }
            }

            if (Settings.Combo.UseE && E.IsReady() && !HasAutoAttackRangeBuffOnChamp)
            {
                if (EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(E.Range) && x.IsMovingTowards(Player.Instance)))
                {
                    foreach (var target in EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(E.Range) && Player.Instance.IsInAutoAttackRange(x) && x.IsMovingTowards(Player.Instance)))
                    {
                        if (target.DistanceCached(Player.Instance) < 500)
                        {
                            var ePrediciton = E.GetPrediction(target);

                            if (ePrediciton.HitChancePercent >= Settings.Combo.EHitChancePercent && !GetDashEndPosition(ePrediciton.CastPosition).IsVectorUnderEnemyTower())
                            {
                                E.Cast(ePrediciton.CastPosition);
                                return;
                            }
                        }
                    }
                }

                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(
                           x => x.IsValidTarget(E.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield());

                var eTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (eTarget != null)
                {
                    var ePrediciton = E.GetPrediction(eTarget);

                    if (ePrediciton.HitChancePercent >= Settings.Combo.EHitChancePercent && !GetDashEndPosition(ePrediciton.CastPosition).IsVectorUnderEnemyTower())
                    {
                        var damage = Player.Instance.GetSpellDamage(eTarget, SpellSlot.E);

                        var endPos = GetDashEndPosition(ePrediciton.CastPosition);

                        var predictiedUnitPosition = eTarget.Position.Extend(eTarget.Path.Last(), (eTarget.MoveSpeed * 0.5f)*0.35f);
                        var unitPosafterAfter = predictiedUnitPosition.Extend(eTarget.Path.Last(), eTarget.MoveSpeed * 0.25f);

                        if (endPos.IsInRange(predictiedUnitPosition, 1300))
                            damage += Damage.GetHeadShotDamage(eTarget);
                        
                        if (Q.IsReady() && endPos.IsInRange(unitPosafterAfter, 1200))
                            damage += Player.Instance.GetSpellDamage(eTarget, SpellSlot.Q);

                        if (damage > eTarget.TotalHealthWithShields() || endPos.IsInRangeCached(eTarget, BasicAttackRange - 100) || (eTarget.IsMelee && eTarget.IsValidTarget(400)))
                        {
                            E.Cast(ePrediciton.CastPosition);
                            return;
                        }
                    }
                }
            }

            if (Settings.Combo.UseR && !IsPreAttack && R.IsReady() && !Player.Instance.Position.IsVectorUnderEnemyTower())
            {
                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(R.Range) && x.Distance(Player.Instance) > (IsUnitNetted(x) ? 1300 : Player.Instance.GetAutoAttackRange()) && !EntityManager.Heroes.Enemies.Where(b => b.NetworkId != x.NetworkId).Any(c => c.IsValidTarget() && new Geometry.Polygon.Rectangle(Player.Instance.Position, x.Position, 200).IsInside(c.ServerPosition)) && (x.TotalHealthWithShields() < Player.Instance.GetSpellDamage(x, SpellSlot.R)) && !x.HasUndyingBuffA() && !x.HasSpellShield());

                var rTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (rTarget != null)
                {
                    if(Q.IsReady() && rTarget.IsValidTargetCached(Q.Range) && (rTarget.TotalHealthWithShields() < Player.Instance.GetSpellDamage(rTarget, SpellSlot.Q)))
                        return;
                    
                    if (Player.Instance.CountEnemyHeroesInRangeWithPrediction(1350, (int)BasicAttackRange) == 0 && !IsPreAttack)
                    {
                        R.Cast(rTarget);
                    }
                }
            }
        }
    }
}