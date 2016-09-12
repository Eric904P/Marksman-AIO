#region Licensing
// ---------------------------------------------------------------------
// <copyright file="PermaActive.cs" company="EloBuddy">
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
    internal class PermaActive : Varus
    {
        public static void Execute()
        {
            if (Settings.Misc.EnableKillsteal)
            {
                if(EntityManager.Heroes.Enemies.Any(x=>x.IsValidTarget(Q.Range) && x.TotalHealthWithShields() <= Damage.GetQDamage(x) + Damage.GetWDamage(x)))
                {
                    foreach (var targ in EntityManager.Heroes.Enemies.Where(x=> !x.IsDead &&
                                x.IsValidTarget(Q.Range) &&
                                (x.TotalHealthWithShields() <= Damage.GetQDamage(x) + Damage.GetWDamage(x))))
                    {
                        if (!Q.IsCharging)
                        {
                            if (!IsPreAttack && 
                                Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) <= 1)
                            {
                                Q.StartCharging();
                                break;
                            }
                        }
                        if (Q.IsCharging)
                        {
                            Q.CastMinimumHitchance(targ, HitChance.Medium);
                        }
                    }

                    
                } else if (E.IsReady() && EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(E.Range) && x.TotalHealthWithShields() <= Player.Instance.GetSpellDamage(x, SpellSlot.E) + Damage.GetWDamage(x)))
                {
                    foreach (var targ in EntityManager.Heroes.Enemies.Where(x => !x.IsDead && x.IsValidTarget(E.Range) && (x.TotalHealthWithShields() <= Player.Instance.GetSpellDamage(x, SpellSlot.E) + Damage.GetWDamage(x))))
                    {
                        E.CastMinimumHitchance(targ, HitChance.Medium);
                    }
                }
            }

            if (Settings.Harass.AutoHarassWithQ && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && !Player.Instance.IsRecalling() &&
                !Player.Instance.Position.IsVectorUnderEnemyTower() && Q.IsReady() &&
                Player.Instance.ManaPercent >= Settings.Harass.MinManaQ)
            {
                if (!Q.IsCharging &&
                    EntityManager.Heroes.Enemies.Any(
                        x => Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) == 0 &&
                            x.IsValidTarget(Q.MaximumRange - 100) && Settings.Harass.IsAutoHarassEnabledFor(x) &&
                            Q.GetPrediction(x).HitChancePercent > 50) && !IsPreAttack &&
                    !EntityManager.Heroes.Enemies.Any(x =>
                        x.IsValidTarget(Settings.Combo.QMinDistanceToTarget)))
                {
                    Q.StartCharging();
                }
                else if (Q.IsCharging)
                {
                    foreach (
                        var target in
                            EntityManager.Heroes.Enemies.Where(
                                x =>
                                    x.IsValidTarget(Q.Range) && Settings.Harass.IsAutoHarassEnabledFor(x) &&
                                    Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) != 0 || Q.IsFullyCharged && Q.GetPrediction(x).HitChancePercent >= 60).TakeWhile(target => Q.IsReady()))
                    {
                        Q.CastMinimumHitchance(target, 60);
                    }
                }
            }

            if (!R.IsReady())
                return;

            var t = TargetSelector.GetTarget(R.Range, DamageType.Physical);

            if (t == null || !Settings.Combo.RKeybind)
                return;
            /*
            var rPrediction = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
            {
                CollisionTypes = new HashSet<CollisionType> { CollisionType.ObjAiMinion },
                Delay = 550,
                From = Player.Instance.Position,
                Radius = 115,
                Range = 1150,
                RangeCheckFrom = Player.Instance.Position,
                Speed = 1800,
                Target = t,
                Type = SkillShotType.Linear
            });*/

            var rPrediction = R.GetPrediction(t);

            if (rPrediction.HitChancePercent >= 60 && rPrediction.CollisionObjects.Where(x => x.NetworkId != t.NetworkId).All(x => x.GetType() != typeof (AIHeroClient)))
            {
                R.Cast(rPrediction.CastPosition);
            }
        }
    }
}