#region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="Combo.cs" company="EloBuddy">
// // 
// //  Marksman AIO
// // 
// //  Copyright (C) 2016 Krystian Tenerowicz
// // 
// //  This program is free software: you can redistribute it and/or modify
// //  it under the terms of the GNU General Public License as published by
// //  the Free Software Foundation, either version 3 of the License, or
// //  (at your option) any later version.
// // 
// //  This program is distributed in the hope that it will be useful,
// //  but WITHOUT ANY WARRANTY; without even the implied warranty of
// //  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// //  GNU General Public License for more details.
// // 
// //  You should have received a copy of the GNU General Public License
// //  along with this program.  If not, see http://www.gnu.org/licenses/. 
// //  </copyright>
// //  <summary>
// // 
// //  Email: geroelobuddy@gmail.com
// //  PayPal: geroelobuddy@gmail.com
// //  </summary>
// //  ---------------------------------------------------------------------
#endregion

using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using Simple_Marksmans.Utils;

namespace Simple_Marksmans.Plugins.Caitlyn.Modes
{
    internal class Combo : Caitlyn
    {
        public static void Execute()
        {
            if (Settings.Combo.UseQ && Q.IsReady() && !Player.Instance.Position.IsVectorUnderEnemyTower() && !HasAutoAttackRangeBuffOnChamp)
            {
                if (Player.Instance.CountEnemiesInRange(650) == 0)
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

            if (Settings.Combo.UseW && W.IsReady())
            {
                var immobileEnemies =
                   EntityManager.Heroes.Enemies.Where(
                       x => x.IsValidTarget(W.Range) && !x.HasSpellShield() && x.GetMovementBlockedDebuffDuration() > 1.5f).ToList();

                foreach (var immobileEnemy in immobileEnemies)
                {
                    W.Cast(immobileEnemy.ServerPosition);
                    break;
                }

                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(
                           x => x.IsValidTarget(500) && !x.HasUndyingBuffA() && !x.HasSpellShield());

                if (EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget() && x.Distance(Player.Instance.ServerPosition) < 300 && x.IsMelee && x.Path.Last().Distance(Player.Instance) < 400))
                {
                    W.Cast(Player.Instance.ServerPosition);
                }

                var wTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (wTarget != null)
                {
                    var wPrediction = W.GetPrediction(wTarget);

                    if (wPrediction.HitChance >= HitChance.High && wPrediction.CastPosition.Distance(wTarget) > 150)
                    {
                        W.Cast(wPrediction.CastPosition);
                    }
                }
            }

            if (Settings.Combo.UseE && E.IsReady() && !HasAutoAttackRangeBuffOnChamp)
            {
                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(
                           x => x.IsValidTarget(E.Range) && x.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamage(x, true) * 2 && !x.HasUndyingBuffA() && !x.HasSpellShield());

                var eTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (eTarget != null)
                {
                    var ePrediciton = E.GetPrediction(eTarget);

                    if (ePrediciton.HitChance >= HitChance.High && GetDashEndPosition(ePrediciton.CastPosition).CountEnemiesInRange(500) <= 1)
                    {
                        var damage = Player.Instance.GetSpellDamage(eTarget, SpellSlot.E);

                        var endPos = GetDashEndPosition(ePrediciton.CastPosition);

                        var predictiedUnitPosition = eTarget.Position.Extend(eTarget.Path.Last(), (eTarget.MoveSpeed * 0.5f)*0.35f);
                        var unitPosafterAfter = predictiedUnitPosition.Extend(eTarget.Path.Last(), eTarget.MoveSpeed * 0.25f);

                        if (endPos.IsInRange(predictiedUnitPosition, 1300))
                            damage += Damage.GetHeadShotDamage(eTarget);
                        
                        if (Q.IsReady() && endPos.IsInRange(unitPosafterAfter, 1200))
                            damage += Player.Instance.GetSpellDamage(eTarget, SpellSlot.Q);

                        if (damage > eTarget.TotalHealthWithShields() || (endPos.Distance(eTarget) < Player.Instance.GetAutoAttackRange()/1.35f) || (eTarget.IsMelee && eTarget.IsValidTarget(400)))
                        {
                            E.Cast(ePrediciton.CastPosition);
                            return;
                        }
                    }
                }
            }

            if (Settings.Combo.UseR && R.IsReady() && !Player.Instance.Position.IsVectorUnderEnemyTower())
            {
                var possibleTargets =
                       EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(R.Range) && x.Distance(Player.Instance) > (IsUnitNetted(x) ? 1300 : Player.Instance.GetAutoAttackRange()) && !EntityManager.Heroes.Enemies.Where(b => b.NetworkId != x.NetworkId).Any(c => c.IsValidTarget() && new Geometry.Polygon.Rectangle(Player.Instance.Position, x.Position, 60).IsInside(c.ServerPosition)) && (x.TotalHealthWithShields() < Player.Instance.GetSpellDamage(x, SpellSlot.R)) && !x.HasUndyingBuffA() && !x.HasSpellShield());

                var rTarget = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                if (rTarget != null)
                {
                    if(Q.IsReady() && rTarget.TotalHealthWithShields() < Player.Instance.GetSpellDamage(rTarget, SpellSlot.Q))
                        return;

                    var enemies =
                        EntityManager.Heroes.Enemies.Count(
                            x =>
                                x.IsValidTarget() &&
                                Prediction.Position.PredictUnitPosition(x, 1300).Distance(Player.Instance) < Player.Instance.GetAutoAttackRange());

                    if (enemies == 0)
                    {
                        R.Cast(rTarget);
                    }
                }
            }
        }
    }
}