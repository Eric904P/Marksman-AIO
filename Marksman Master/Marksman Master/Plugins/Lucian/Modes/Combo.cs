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
using SharpDX;

namespace Marksman_Master.Plugins.Lucian.Modes
{
    internal class Combo : Lucian
    {
        public static void Execute()
        {
            if (E.IsReady() && Settings.Combo.UseE && !IsCastingR && Settings.Misc.EUsageMode == 0 && !HasPassiveBuff &&
                !Player.Instance.HasSheenBuff())
            {
                var heroClient = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 420, DamageType.Physical);
                var position = Vector3.Zero;

                if (heroClient == null)
                    return;

                var damage = Player.Instance.GetAutoAttackDamage(heroClient, true) * 2;

                if (Q.IsReady())
                    damage += Player.Instance.GetSpellDamage(heroClient, SpellSlot.Q);
                if (W.IsReady())
                    damage += Player.Instance.GetSpellDamage(heroClient, SpellSlot.W);

                if (!((damage < heroClient.TotalHealthWithShields()) || (Q.IsReady() && W.IsReady())))
                    return;
                
                if (Settings.Misc.EMode == 0)
                {
                    if (Player.Instance.HealthPercent > heroClient.HealthPercent+5 && heroClient.CountEnemiesInRange(600) <= 2)
                    {
                        if (!Player.Instance.Position.Extend(Game.CursorPos, 420)
                            .To3D()
                            .IsVectorUnderEnemyTower() &&
                            (!heroClient.IsMelee ||
                             Player.Instance.Position.Extend(Game.CursorPos, 420)
                                 .IsInRange(heroClient, heroClient.GetAutoAttackRange()*1.5f)))
                        {
                            Console.WriteLine("[DEBUG] 1v1 Game.CursorPos");
                            position = Game.CursorPos.Distance(Player.Instance) > 450
                                ? Player.Instance.Position.Extend(Game.CursorPos, 450).To3D()
                                : Game.CursorPos;
                        }
                    }
                    else
                    {
                        var closest =
                            EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(1300))
                                .OrderBy(x => x.Distance(Player.Instance)).ToArray()[0];

                        var list =
                            SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 900,
                                1300,
                                heroClient.IsMelee ? heroClient.GetAutoAttackRange()*2 : heroClient.GetAutoAttackRange())
                                .Where(
                                    x =>
                                        !x.Key.To3D().IsVectorUnderEnemyTower() &&
                                        x.Key.IsInRange(Prediction.Position.PredictUnitPosition(closest, 850),
                                            Player.Instance.GetAutoAttackRange() - 50))
                                .Select(source => source.Key)
                                .ToList();

                        if (list.Any())
                        {
                            var paths =
                                EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(1300))
                                    .Select(x => x.Path)
                                    .Count(result => result != null && result.Last().Distance(Player.Instance) < 300);

                            var asc = Misc.SortVectorsByDistance(list, heroClient.Position.To2D())[0].To3D();
                            if (Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) == 0 &&
                                !EntityManager.Heroes.Enemies.Where(x => x.Distance(Player.Instance) < 1000).Any(
                                    x => Prediction.Position.PredictUnitPosition(x, 800)
                                        .IsInRange(asc,
                                            x.IsMelee ? x.GetAutoAttackRange()*2 : x.GetAutoAttackRange())))
                            {
                                position = asc;

                                Console.WriteLine("[DEBUG] Paths low sorting Ascending");
                            }
                            else if (Player.Instance.CountEnemiesInRange(1000) <= 2 && (paths == 0 || paths == 1) &&
                                     ((closest.Health < Player.Instance.GetAutoAttackDamage(closest, true)*2) ||
                                      (Orbwalker.LastTarget is AIHeroClient &&
                                       Orbwalker.LastTarget.Health <
                                       Player.Instance.GetAutoAttackDamage(closest, true)*2)))
                            {
                                position = asc;
                            }
                            else
                            {
                                position =
                                    Misc.SortVectorsByDistanceDescending(list, heroClient.Position.To2D())[0].To3D();
                                Console.WriteLine("[DEBUG] Paths high sorting Descending");
                            }
                        }
                        else Console.WriteLine("[DEBUG] 1v1 not found positions...");
                    }

                    if (position != Vector3.Zero && EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(900)))
                    {
                        E.Cast(position.Distance(Player.Instance) > E.Range
                            ? Player.Instance.Position.Extend(position, E.Range).To3D()
                            : position);
                        return;
                    }
                }
                else if (Settings.Misc.EMode == 1)
                {
                    var enemies = Player.Instance.CountEnemiesInRange(1300);

                    var pos = Game.CursorPos.Distance(Player.Instance) > 450 ? Player.Instance.Position.Extend(Game.CursorPos, 450).To3D() : Game.CursorPos;

                    if (!pos.IsVectorUnderEnemyTower())
                    {
                        if (enemies == 1)
                        {
                            if (heroClient.IsMelee &&
                                !pos.IsInRange(Prediction.Position.PredictUnitPosition(heroClient, 850),
                                    heroClient.GetAutoAttackRange() + 150))
                            {
                                E.Cast(pos);
                                return;
                            }
                            if (!heroClient.IsMelee)
                            {
                                E.Cast(pos);
                                return;
                            }
                        }
                        else if (enemies == 2 && Player.Instance.CountAlliesInRange(850) >= 1)
                        {
                            E.Cast(pos);
                            return;
                        }
                        else if (enemies >= 2)
                        {
                            if (
                                !EntityManager.Heroes.Enemies.Any(
                                    x =>
                                        pos.IsInRange(Prediction.Position.PredictUnitPosition(x, 400),
                                            x.IsMelee ? x.GetAutoAttackRange() + 150 : x.GetAutoAttackRange())))
                            {
                                E.Cast(pos);
                                return;
                            }
                        }
                    }
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ && !IsCastingR && !HasPassiveBuff && !Player.Instance.HasSheenBuff())
            {
                var target = TargetSelector.GetTarget(Q.Range, DamageType.Physical);
                var target2 = TargetSelector.GetTarget(900, DamageType.Physical);

                if (target != null && target.IsValidTarget(Q.Range) &&
                    ((Player.Instance.Mana - 50 + 5*(Q.Level - 1) > 40 - 10*(E.Level - 1) + (R.IsReady() ? 100: 0)) ||
                     (Player.Instance.GetSpellDamage(target, SpellSlot.Q) > target.TotalHealthWithShields())))
                {
                    Q.Cast(target);
                    return;
                }
                if (Settings.Combo.ExtendQOnMinions && target2 != null &&
                    ((Player.Instance.Mana - 50 + 5*(Q.Level - 1) > 40 - 10*(E.Level - 1) + (R.IsReady() ? 100 : 0)) ||
                     (Player.Instance.GetSpellDamage(target2, SpellSlot.Q) > target2.TotalHealthWithShields())))
                {
                    foreach (
                        var entity in
                            from entity in
                                EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                                    x => x.IsValidTarget(Q.Range))
                            let pos =
                                Player.Instance.Position.Extend(entity, 900 - Player.Instance.Distance(entity))
                            let targetpos = Prediction.Position.PredictUnitPosition(target2, 250)
                            let rect = new Geometry.Polygon.Rectangle(entity.Position.To2D(), pos, 10)
                            where
                                new Geometry.Polygon.Circle(targetpos, target2.BoundingRadius).Points.Any(
                                    rect.IsInside)
                            select entity)
                    {
                        Q.Cast(entity);
                        return;
                    }
                }
            }

            if (W.IsReady() && Settings.Combo.UseW && !IsCastingR && !HasPassiveBuff && !Player.Instance.HasSheenBuff())
            {
                var target = TargetSelector.GetTarget(W.Range, DamageType.Physical);

                if (target != null && ((Player.Instance.Mana - 50 > (R.IsReady() ? 100 : 0)) ||
                                       Player.Instance.GetSpellDamage(target, SpellSlot.W) >
                                       target.TotalHealthWithShields()))
                {
                    if (Settings.Combo.IgnoreCollisionW && Player.Instance.IsInAutoAttackRange(target) && Orbwalker.LastTarget != null && Orbwalker.LastTarget.NetworkId == target.NetworkId)
                    {
                        W.Cast(target);
                        return;
                    }
                    var wPrediction = W.GetPrediction(target);
                    if (wPrediction.HitChance == HitChance.Medium)
                    {
                        W.Cast(wPrediction.CastPosition);
                        return;
                    }
                }
            }

            if (!R.IsReady() || !Settings.Combo.UseR)
                return;
            
            var t = TargetSelector.GetTarget(R.Range, DamageType.Physical);

            if (t == null || !(t.Distance(Player.Instance) > Player.Instance.GetAutoAttackRange()) ||
                !(Damage.GetSingleRShotDamage(t)*10 >
                  t.TotalHealthWithShields()) ||
                Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) != 0)
                return;

            var rPrediciton2 = R.GetPrediction(t);
            if (rPrediciton2.HitChance >= HitChance.Medium &&
                Player.Instance.Spellbook.GetSpell(SpellSlot.R).Name == "LucianR")
            {
                R.Cast(rPrediciton2.CastPosition);
            }
        }
    }
}