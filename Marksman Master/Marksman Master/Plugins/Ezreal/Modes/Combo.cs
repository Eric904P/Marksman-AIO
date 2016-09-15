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

namespace Marksman_Master.Plugins.Ezreal.Modes
{
    internal class Combo : Ezreal
    {
        public static void Execute()
        {
            if (E.IsReady() && Settings.Combo.UseE && Player.Instance.Mana - 90 > 130)
            {
                var killable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.Range + E.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() && x.HealthPercent < 50).ToList();

                if (killable.Any() && Player.Instance.HealthPercent > 10)
                {
                    foreach (var target in killable)
                    {
                        var endPos = Player.Instance.Position.Extend(target,
                            target.Distance(Player.Instance) > E.Range ? E.Range : target.Distance(Player.Instance));

                        if (endPos.CountEnemiesInRange(600) >= (Player.Instance.HealthPercent > 65 ? 2 : 1) || endPos.To3D().IsVectorUnderEnemyTower())
                            continue;

                        var qPrediction = Prediction.Manager.GetPrediction(new Prediction.Manager.PredictionInput
                        {
                            Range = Q.Range,
                            Target = target,
                            RangeCheckFrom = endPos.To3D(),
                            Speed = Q.Speed,
                            Delay = 250,
                            From = endPos.To3D(),
                            Radius = Q.Width,
                            Type = SkillShotType.Linear,
                            CollisionTypes = new HashSet<CollisionType> { CollisionType.AiHeroClient }
                        });

                        var damage = (Q.IsReady() && endPos.IsInRange(target, Q.Range) && qPrediction.HitChancePercent >= 65 ? Player.Instance.GetSpellDamage(target, SpellSlot.Q) : 0) +
                                     (endPos.IsInRange(target, 750) ? Player.Instance.GetSpellDamage(target, SpellSlot.E) : 0);

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
                    var melee = EntityManager.Heroes.Enemies.Where(x => !x.IsDead && x.IsValidTarget(400) && x.IsMelee).ToList();
                    var melee2 = EntityManager.Heroes.Enemies.Where(x => !x.IsDead && Player.Instance.IsInAutoAttackRange(x) && x.IsMelee).ToList();
                    if (melee.Any())
                    {
                        var firstOrDefault = melee2.OrderBy(x => x.Distance(Player.Instance)).First();

                        if (firstOrDefault != null)
                        {
                            if (!(melee.Count == 1 &&
                                  firstOrDefault.TotalHealthWithShields() < GetComboDamage(firstOrDefault)) || melee2.Count > 1)
                            {
                                var pos =
                                    Misc.SortVectorsByDistanceDescending(
                                        SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 900, 900, 500)
                                            .Where(x => !x.Key.To3D().IsVectorUnderEnemyTower())
                                            .Select(x => x.Key)
                                            .ToList(), firstOrDefault.Position.To2D())[0];

                                E.Cast(pos.Distance(Player.Instance) > E.Range
                                    ? Player.Instance.Position.Extend(pos, E.Range - 15).To3D()
                                    : pos.To3D());
                                return;
                            }
                        }
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
                                return;
                            }
                        }
                    }
                }
                else
                {
                    var possibleTargets =
                        EntityManager.Heroes.Enemies.Where(
                            x => x.IsValidTarget(Q.Range) && !EntityManager.Heroes.Enemies.Any(k =>
                                k.IsValidTarget(Q.Range - 80) &&
                                !k.HasSpellShield() &&
                                !k.HasUndyingBuffA() && (k.TotalHealthWithShields() < Player.Instance.GetSpellDamage(k, SpellSlot.Q))) && (Q.GetPrediction(x).HitChancePercent > 65) &&
                                 !x.HasSpellShield() && !x.HasUndyingBuffA()).ToList();

                    if (possibleTargets.Any())
                    {
                        var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                        if (target != null && !Player.Instance.HasSheenBuff() && !IsPreAttack)
                        {
                            Q.CastMinimumHitchance(target, 65);
                            return;
                        }
                    }
                }
            }
            
            if (W.IsReady() && Settings.Combo.UseW && Player.Instance.Mana - (50+10*(W.Level-1)) > 130 && !Player.Instance.HasSheenBuff())
            {
                var target = W.GetTarget();

                if (target != null && !target.HasUndyingBuffA() && !Player.Instance.HasSheenBuff())
                {
                    W.CastMinimumHitchance(target, 75);
                    return;
                }
            }
            
            if (R.IsReady() && Settings.Combo.UseR && !Player.Instance.Position.IsVectorUnderEnemyTower())
            {
                var killable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() && Player.Instance.GetSpellDamage(x, SpellSlot.Q) > x.TotalHealthWithShields()).ToList();

                if (killable.Any() && Q.IsReady())
                    return;

                if (Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()+100) == 0)
                {
                    foreach (var target in EntityManager.Heroes.Enemies.Where(x => x.Distance(Player.Instance) < 5000))
                    {
                        var rPrediction = R.GetPrediction(target);

                        if (rPrediction.HitChancePercent >= 70)
                        {
                            var collision = new Geometry.Polygon.Rectangle(Player.Instance.Position.To2D(), Player.Instance.Position.Extend(rPrediction.CastPosition, 6000), 160);

                            var count = EntityManager.Heroes.Enemies.Where(x => x.NetworkId != target.NetworkId).Count(x => x.IsValidTarget() && collision.IsInside(Prediction.Position.PredictUnitPosition(x, (int)(x.Distance(Player.Instance) / 2000) + 1000))) + 1;

                            if (count >= Settings.Combo.RMinEnemiesHit)
                            {
                                Misc.PrintInfoMessage("Casting R because it can hit <font color=\"#ff1493\">" + count + "</font>. enemies");

                                R.Cast(rPrediction.CastPosition);
                            }
                        }
                    }

                    var rKillable = EntityManager.Heroes.Enemies.Where(
                        x => x.IsValidTarget(3000) && !x.HasUndyingBuffA() && !x.HasSpellShield() && Player.Instance.GetSpellDamage(x, SpellSlot.R) > x.TotalHealthWithShields(true)).ToList();

                    if (rKillable.Any(x=> IncomingDamage.GetIncomingDamage(x) > 0))
                    {
                        foreach (var target in rKillable.Where(x => IncomingDamage.GetIncomingDamage(x) > 0))
                        {
                            R.CastMinimumHitchance(target, 70);
                        }
                    }
                }
            }
        }
    }
}