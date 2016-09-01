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

namespace Simple_Marksmans.Plugins.Ezreal.Modes
{
    internal class Combo : Ezreal
    {
        public static void Execute()
        {
            if (E.IsReady() && Settings.Combo.UseE && Player.Instance.Mana - 90 > 130)
            {
                var killable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.Range + E.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() && x.HealthPercent < 50).ToList();

                if (killable.Any() && Player.Instance.HealthPercent > 25)
                {
                    foreach (var target in killable)
                    {
                        var endPos = Player.Instance.Position.Extend(target,
                            target.Distance(Player.Instance) > E.Range ? E.Range : target.Distance(Player.Instance));

                        if (endPos.CountEnemiesInRange(600) >= 2 || endPos.To3D().IsVectorUnderEnemyTower())
                            continue;

                        var damage = Player.Instance.GetSpellDamage(target, SpellSlot.Q) +
                                     Player.Instance.GetSpellDamage(target, SpellSlot.E);

                        if (endPos.IsInRange(target, Player.Instance.GetAutoAttackRange()))
                            damage += Player.Instance.GetAutoAttackDamage(target, true);

                        if (!(damage > target.TotalHealthWithShields()))
                            continue;

                        E.Cast(endPos.To3D());
                        return;
                    }
                }
                else if (Settings.Misc.EAntiMelee)
                {
                    var melee =
                        EntityManager.Heroes.Enemies.Where(x => x.Distance(Player.Instance) < 350 && x.IsMelee).ToList();

                    if (melee.Any() && !(melee.Count == 1 && melee.FirstOrDefault().TotalHealthWithShields() < GetComboDamage(melee.FirstOrDefault())))
                    {
                        var firstOrDefault = melee.OrderBy(x => x.Distance(Player.Instance)).ToArray()[0];

                        if (firstOrDefault != null)
                        {
                            var pos = Misc.SortVectorsByDistanceDescending(SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 900, 900, 500).Where(x => x.Value < 2 && !x.Key.To3D().IsVectorUnderEnemyTower()).Select(x => x.Key).ToList(), firstOrDefault.Position.To2D())[0];

                            E.Cast(pos.Distance(Player.Instance) > E.Range ? Player.Instance.Position.Extend(pos, E.Range).To3D() : pos.To3D());
                        }
                        return;
                    }
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ && !Player.Instance.HasSheenBuff())
            {
                var immobileEnemies =
                    EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() && x.GetMovementBlockedDebuffDuration() > 0.3f).ToList();

                if (Settings.Combo.UseQOnImmobile && immobileEnemies.Any())
                {
                    foreach (
                        var immobileEnemy in
                            immobileEnemies.OrderByDescending(x => Player.Instance.GetSpellDamage(x, SpellSlot.Q)))
                    {
                        if ((immobileEnemy.GetMovementBlockedDebuffDuration() >
                            Player.Instance.Distance(immobileEnemy)/Q.Speed + 0.25f) && !Player.Instance.HasSheenBuff())
                        {
                            var qPrediction = Q.GetPrediction(immobileEnemy);
                            if (qPrediction.HitChancePercent > 60 && !IsPreAttack)
                            {
                                Q.Cast(qPrediction.CastPosition);
                            }
                        }
                    }
                }
                else
                {
                    var target = Q.GetTarget();

                    if (target != null && !target.HasUndyingBuffA() && !target.HasSpellShield() && !Player.Instance.HasSheenBuff() && !IsPreAttack)
                    {
                        Q.CastMinimumHitchance(target, 75);
                    }
                }
            }
            
            if (W.IsReady() && Settings.Combo.UseW && Player.Instance.Mana - (50+10*(W.Level-1)) > 130 && !Player.Instance.HasSheenBuff())
            {
                var target = W.GetTarget();

                if (target != null && !target.HasUndyingBuffA() && !Player.Instance.HasSheenBuff())
                {
                    W.CastMinimumHitchance(target, 75);
                }
            }

            if (R.IsReady() && Settings.Combo.UseR && !Player.Instance.Position.IsVectorUnderEnemyTower())
            {
                var killable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() && Player.Instance.GetSpellDamage(x, SpellSlot.Q) > x.TotalHealthWithShields()).ToList();

                if (killable.Any() && Q.IsReady())
                    return;

                if (Settings.Combo.RMinEnemiesHit > 0 && Player.Instance.CountEnemiesInRange(600) < 2)
                {
                    foreach (var source in EntityManager.Heroes.Enemies.Where(x=>x.IsValidTarget(3000)))
                    {
                        var rPred = R.GetPrediction(source);
                        if (rPred.HitChancePercent > 60 &&
                            rPred.GetCollisionObjects<AIHeroClient>().Length >= Settings.Combo.RMinEnemiesHit)
                        {
                            R.Cast(rPred.CastPosition);
                            return;
                        }
                    }
                }

                if (Player.Instance.CountEnemiesInRange(600) < 2)
                {
                    var rKillable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(3000) && !x.HasUndyingBuffA() && !x.HasSpellShield() && Player.Instance.GetSpellDamage(x, SpellSlot.R) > x.TotalHealthWithShields()).ToList();

                    if (rKillable.Any())
                    {
                        foreach (var target in rKillable)
                        {
                            R.CastMinimumHitchance(target, HitChance.Medium);
                        }
                    }
                }
            }
        }
    }
}