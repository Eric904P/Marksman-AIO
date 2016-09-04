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

using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using Simple_Marksmans.Utils;

namespace Simple_Marksmans.Plugins.MissFortune.Modes
{
    internal class Combo : MissFortune
    {
        public static void Execute()
        {
            if(RCasted)
                return;

            if (Settings.Combo.UseW && W.IsReady() && IsPreAttack &&
                Player.Instance.Mana - WMana > (R.IsReady() ? RMana + QMana[Q.Level] : QMana[Q.Level]))
            {
                if (Orbwalker.LastTarget != null && Orbwalker.LastTarget.GetType() == typeof (AIHeroClient) && !HasWBuff)
                {
                    W.Cast();
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ && !IsPreAttack &&
                Player.Instance.Mana - QMana[Q.Level] > (R.IsReady() ?  RMana + WMana : WMana))
            {
                var qTarget = Q.GetTarget();

                if (qTarget != null)
                {
                    if (Settings.Misc.BounceQFromMinions)
                    {
                        var minion = GetQMinion(qTarget);
                        Q.Cast(minion ?? qTarget);
                    } else Q.Cast(qTarget);
                }
            }

            if (Settings.Combo.UseE && E.IsReady() && !IsPreAttack &&
                Player.Instance.Mana - EMana > (R.IsReady() ? RMana + WMana + QMana[Q.Level] : WMana + QMana[Q.Level]))
            {
                E.CastIfItWillHit(3);

                var target = E.GetTarget();
                if (target != null)
                {
                    E.CastMinimumHitchance(target, HitChance.High);
                }
            }


            if (R.IsReady() && Settings.Combo.UseR && !IsPreAttack && !IsAfterAttack && !new Geometry.Polygon.Circle(Player.Instance.Position,  Player.Instance.BoundingRadius).Points.Any(x=>x.To3D().IsVectorUnderEnemyTower()))
            {
                if (Player.Instance.CountEnemiesInRange(800) == 1)
                {
                    var target = R.GetTarget();

                    if (target != null && target.Distance(Player.Instance) < 600)
                    {
                        var waves = (int) Math.Floor(target.Health/Player.Instance.GetSpellDamage(target, SpellSlot.R));
                        if (waves < RWaves[R.Level])
                        {
                            R.Cast(target.ServerPosition);
                            return;
                        }
                    }
                }

                if (Player.Instance.CountEnemiesInRange(725) > 0)
                    return;

                if (EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(R.Range)))
                {
                    var wavesNeeded = new Dictionary<int, Tuple<int, int>>();

                    foreach (var target in EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(R.Range)))
                    {
                        wavesNeeded[target.NetworkId] =
                            new Tuple<int, int>(
                                (int) Math.Floor(target.Health/Player.Instance.GetSpellDamage(target, SpellSlot.R)),
                                GetObjectsWithinRRange<AIHeroClient>(target.Position)
                                    .Count(x => x.IsValidTarget(R.Range)));
                    }

                    if (wavesNeeded.Any(x => x.Value.Item1 < RWaves[R.Level])) // can be killed by x amount of waves
                    {
                        foreach (var tuple in wavesNeeded)
                        {
                            var enemy = EntityManager.Heroes.Enemies.First(x => x.NetworkId == tuple.Key);

                            if (!enemy.IsValidTarget(1280))
                                continue;

                            var dist = enemy.Distance(Player.Instance);

                            if (dist < 800 && tuple.Value.Item1 < 10)
                            {
                                R.CastMinimumHitchance(enemy, HitChance.High);
                                break;
                            }
                            if (dist < 1000 && dist > 800 && tuple.Value.Item1 < 8)
                            {
                                R.CastMinimumHitchance(enemy, HitChance.High);
                                break;
                            }
                            if (dist < 1200 && dist > 1000 && tuple.Value.Item1 <= 4)
                            {
                                R.CastMinimumHitchance(enemy, HitChance.High);
                                break;
                            }

                            if (!(dist < 1300) || !(dist > 1200) || tuple.Value.Item1 > 2)
                                continue;

                            R.CastMinimumHitchance(enemy, HitChance.High);
                            break;
                        }


                    } else if (R.IsReady() && wavesNeeded.Any(x => x.Value.Item2 >= Settings.Combo.RWhenXEnemies))
                    {
                        var enemy =
                            EntityManager.Heroes.Enemies.First(
                                x =>
                                    x.NetworkId ==
                                    wavesNeeded.FirstOrDefault(l => l.Value.Item2 >= Settings.Combo.RWhenXEnemies).Key);

                        if (enemy.IsValidTarget(1280))
                        {
                            R.CastMinimumHitchance(enemy, HitChance.High);
                        }
                    }
                }
            }
        }
    }
}