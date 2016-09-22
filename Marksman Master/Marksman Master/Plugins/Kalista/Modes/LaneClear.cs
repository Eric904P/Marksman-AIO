#region Licensing
// ---------------------------------------------------------------------
// <copyright file="LaneClear.cs" company="EloBuddy">
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
using EloBuddy.SDK.Events;

namespace Marksman_Master.Plugins.Kalista.Modes
{
    internal class LaneClear : Kalista
    {
        public static void Execute()
        {
            if (Q.IsReady() && Settings.JungleLaneClear.UseQ && !Player.Instance.IsDashing() &&
                Player.Instance.ManaPercent >= Settings.JungleLaneClear.MinManaForQ)
            {
                var minions =
                    EntityManager.MinionsAndMonsters.EnemyMinions.Where(
                        x => x.Health < Player.Instance.GetSpellDamage(x, SpellSlot.Q)).ToList();

                if (!minions.Any() || Player.Instance.IsDashing())
                    return;

                foreach (var minion in minions.Where(x=> x.Health < Player.Instance.GetSpellDamage(x, SpellSlot.Q) && Q.GetPrediction(x).HitChance >= HitChance.Medium))
                {
                    if (Settings.JungleLaneClear.MinMinionsForQ == 1)
                    {
                        Q.Cast(minion.ServerPosition);
                        break;
                    }

                    var collisionableObjects =
                        EntityManager.MinionsAndMonsters.EnemyMinions.Where(
                            x => x.IsValidTarget(Q.Range) &&
                                 new Geometry.Polygon.Circle(x.Position, x.BoundingRadius).Points.Any(
                                     b => new Geometry.Polygon.Rectangle(Player.Instance.Position,
                                         Player.Instance.Position.Extend(minion.Position,
                                             minion.Distance(Player.Instance) >= Q.Range ? 0 : Q.Range).To3D(), 40)
                                         .IsInside(b)) && x.NetworkId != minion.NetworkId)
                            .OrderBy(x => x.Distance(Player.Instance))
                            .ToList();

                    var count = 1;

                    for (int i = 0, lenght = collisionableObjects.Count; i < lenght; i++)
                    {
                        if (collisionableObjects[i].Health <
                            Player.Instance.GetSpellDamage(collisionableObjects[i], SpellSlot.Q))
                        {
                            count++;

                            if (i + 1 < lenght && collisionableObjects[i + 1].Health >
                                Player.Instance.GetSpellDamage(collisionableObjects[i + 1], SpellSlot.Q))
                            {
                                break;
                            }
                        }
                    }

                    if (count < Settings.JungleLaneClear.MinMinionsForQ)
                        continue;

                    Q.Cast(Q.GetPrediction(minion).CastPosition);
                    break;
                }
            }

            if (!E.IsReady() || !Settings.JungleLaneClear.UseE || !(Player.Instance.ManaPercent >= Settings.JungleLaneClear.MinManaForE))
                return;

            {
                var minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                    Player.Instance.Position, E.Range).Where(Damage.IsTargetKillableByRend);

                if (minions.Count() >= Settings.JungleLaneClear.MinMinionsForE)
                {
                    E.Cast();
                }
            }
        }
    }
}