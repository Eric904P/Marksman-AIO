#region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="PermaActive.cs" company="EloBuddy">
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
using Simple_Marksmans.Utils;

namespace Simple_Marksmans.Plugins.Ezreal.Modes
{
    internal class PermaActive : Ezreal
    {
        public static void Execute()
        {
            if (Settings.Misc.EnableKillsteal)
            {
                var enemies = EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(Q.Range) && x.HealthPercent < 20 && !x.HasUndyingBuffA() &&
                        !x.HasSpellShield()).ToList();

                if (enemies.Any())
                {
                    if (Q.IsReady())
                    {
                        foreach (var enemy in enemies.Where(x=> x.TotalHealthWithShields() < Player.Instance.GetSpellDamage(x, SpellSlot.Q)))
                        {
                            Q.CastMinimumHitchance(enemy, 65);
                        }
                    } else if (W.IsReady())
                    {
                        foreach (var enemy in enemies.Where(x => x.TotalHealthWithShields() < Player.Instance.GetSpellDamage(x, SpellSlot.W)))
                        {
                            W.CastMinimumHitchance(enemy, 65);
                        }
                    }
                }
            }

            if (Q.IsReady() && !Player.Instance.IsRecalling() && Settings.Misc.KeepPassiveStacks && GetPassiveBuffAmount >= 4 && GetPassiveBuff.EndTime - Game.Time < 1.5f && GetPassiveBuff.EndTime - Game.Time > 0.3f && Player.Instance.Mana > 350 && !EntityManager.Heroes.Enemies.Any(x=>x.IsValidTarget(Q.Range)))
            {
                foreach (var minion in EntityManager.MinionsAndMonsters.CombinedAttackable.Where(x=>x.IsValidTarget(Q.Range)))
                {
                    Q.Cast(minion);
                    return;
                }
            }

            if (Q.IsReady() && Settings.Harass.UseQ && Player.Instance.ManaPercent >= Settings.Harass.MinManaQ &&
                !Player.Instance.HasSheenBuff() && Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) == 0)
            {
                var immobileEnemies = EntityManager.Heroes.Enemies.Where(
                    x =>
                        Settings.Harass.IsAutoHarassEnabledFor(x) && x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() &&
                        !x.HasSpellShield() && x.GetMovementBlockedDebuffDuration() > 0.3f).ToList();

                if (immobileEnemies.Any())
                {
                    foreach (
                        var immobileEnemy in
                            immobileEnemies.OrderByDescending(x => Player.Instance.GetSpellDamage(x, SpellSlot.Q)))
                    {
                        if ((immobileEnemy.GetMovementBlockedDebuffDuration() >
                             Player.Instance.Distance(immobileEnemy)/Q.Speed + 0.25f) && !Player.Instance.HasSheenBuff())
                        {
                            var qPrediction = Q.GetPrediction(immobileEnemy);
                            if (qPrediction.HitChancePercent > 60)
                            {
                                Q.Cast(qPrediction.CastPosition);
                            }
                        }

                    }
                }
                else
                {
                    foreach (var target in
                        EntityManager.Heroes.Enemies.Where(x =>
                            Settings.Harass.IsAutoHarassEnabledFor(x) && x.IsValidTarget(Q.Range) &&
                            !x.HasUndyingBuffA() &&
                            !x.HasSpellShield()).OrderByDescending(x => Player.Instance.GetSpellDamage(x, SpellSlot.Q)))
                    {
                        Q.CastMinimumHitchance(target, 75);
                    }
                }
            }

            if (!R.IsReady() || !Settings.Combo.UseR)
                return;

            var t = TargetSelector.GetTarget(1500, DamageType.Physical);

            if (t == null || !Settings.Combo.RKeybind)
                return;

            var rPrediciton = R.GetPrediction(t);
            if (rPrediciton.HitChancePercent >= 65)
            {
                R.Cast(rPrediciton.CastPosition);
            }
        }
    }
}