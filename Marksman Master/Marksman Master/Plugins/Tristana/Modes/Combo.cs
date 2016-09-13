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
using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Tristana.Modes
{
    internal class Combo : Tristana
    {
        public static void Execute()
        {
            if (Q.IsReady() && IsPreAttack && Settings.Combo.UseQ)
            {
                if (EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(Player.Instance.GetAutoAttackRange() - 50)))
                {
                    Q.Cast();
                }
            }

            if (WTarget != null && W.IsReady() && Settings.Combo.DoubleWKeybind)
            {
                var target = EntityManager.Heroes.Enemies.FirstOrDefault(x => x.NetworkId == WTarget.NetworkId);

                if (target != null)
                {
                    var wPrediction = W.GetPrediction(target);

                    if (wPrediction.HitChance >= HitChance.Medium)
                    {
                        WTarget = null;

                        W.Cast(wPrediction.CastPosition);
                    }
                }
            }

            if (W.IsReady() && IsCatingW)
            {
                W.Cast(Player.Instance.Position.Extend(WStartPos, WStartPos.Distance(Player.Instance) > 850 ? 850 : WStartPos.Distance(Player.Instance)).To3D());
                IsCatingW = false;
            }
            
            if (W.IsReady() && Settings.Combo.UseW && R.IsReady() && Settings.Combo.UseR && Player.Instance.Mana - 160 > 90 && Player.Instance.HealthPercent > 25)
            {
                var target = TargetSelector.GetTarget(900, DamageType.Physical);

                if (target != null && target.CountEnemiesInRange(500) == 1 && target.Distance(Player.Instance) > R.Range)
                {
                    var damage = IncomingDamage.GetIncomingDamage(target) + Damage.GetRDamage(target) +
                                 Damage.GetEPhysicalDamage(target);

                    if (HasExplosiveChargeBuff(target) && target.Health < damage)
                    {
                        var wPrediction = W.GetPrediction(target);
                        if (wPrediction.HitChance >= HitChance.Medium)
                        {
                            IsCatingW = true;
                            Core.DelayAction(() => IsCatingW = false, 2000);
                            WStartPos = Player.Instance.Position;

                            W.Cast(wPrediction.CastPosition);
                        }
                    }
                }
            }

            if (E.IsReady() && Settings.Combo.UseE && IsPreAttack)
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Physical);

                if (target != null && Settings.Combo.IsEnabledFor(target))
                {
                    E.Cast(target);
                }
            }

            if (Settings.Combo.FocusE && IsPreAttack && EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(Player.Instance.GetAutoAttackRange()) && HasExplosiveChargeBuff(x)))
            {
                foreach (
                    var enemy in
                        EntityManager.Heroes.Enemies.Where(
                            x => x.IsValidTarget(Player.Instance.GetAutoAttackRange()) && HasExplosiveChargeBuff(x)))
                {
                    if (!EntityManager.Heroes.Enemies.Any(
                        x =>
                            x.IsValidTarget(Player.Instance.GetAutoAttackRange()) &&
                            x.TotalHealthWithShields() < Player.Instance.GetAutoAttackDamage(x, true)*2 &&
                            x.NetworkId != enemy.NetworkId))
                    {
                        Orbwalker.ForcedTarget = enemy;
                    }
                    else
                    {
                        Orbwalker.ForcedTarget = null;
                    }
                } 
            } else if(!Settings.Combo.FocusE || !EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(Player.Instance.GetAutoAttackRange()) && HasExplosiveChargeBuff(x))) { Orbwalker.ForcedTarget = null; }

            if (R.IsReady() && Settings.Combo.UseR && Settings.Combo.UseRVsMelees && Player.Instance.HealthPercent < 20 && EntityManager.Heroes.Enemies.Any(x => x.IsMelee && x.IsValidTarget(300) && x.HealthPercent > 50))
            {
                foreach (var enemy in EntityManager.Heroes.Enemies.Where(x => x.IsMelee && x.IsValidTarget(300) && x.HealthPercent > 50).OrderByDescending(TargetSelector.GetPriority).ThenBy(x=>x.Distance(Player.Instance)))
                {
                    R.Cast(enemy);
                }
            }
        }
    }
}