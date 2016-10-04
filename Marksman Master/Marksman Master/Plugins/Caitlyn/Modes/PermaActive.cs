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
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Caitlyn.Modes
{
    internal class PermaActive : Caitlyn
    {
        public static void Execute()
        {
            if (Settings.Misc.EnableKillsteal && (Player.Instance.CountEnemyHeroesInRangeWithPrediction((int)Player.Instance.GetAutoAttackRange(), 1000) <= 1) && !IsPreAttack && Q.IsReady() && !Player.Instance.Position.IsVectorUnderEnemyTower())
            {
                foreach (
                    var target in
                        EntityManager.Heroes.Enemies.Where(
                            x =>
                                x.IsValidTarget(Q.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() &&
                                (x.TotalHealthWithShields() < Player.Instance.GetSpellDamage(x, SpellSlot.Q)) &&
                                !(x.TotalHealthWithShields() < Player.Instance.GetAutoAttackDamage(x, true) && Player.Instance.IsInAutoAttackRange(x))))
                {
                    Q.CastMinimumHitchance(target, 60);
                    break;
                }
            }

            if (!Settings.Combo.UseWOnImmobile || !W.IsReady())
                return;

            var immobileEnemies =
                EntityManager.Heroes.Enemies.Where(
                    x =>
                        x.IsValidTarget(W.Range) && !x.HasSpellShield() &&
                        x.GetMovementBlockedDebuffDuration() > 1.5f).ToList();

            foreach (var immobileEnemy in immobileEnemies)
            {
                W.Cast(immobileEnemy.ServerPosition);
                break;
            }

            foreach (
                var enemy in
                    EntityManager.Heroes.Enemies.Where(
                        x =>
                            x.IsValidTarget(W.Range) && x.Buffs.Any(
                                m =>
                                    m.Name.ToLowerInvariant() == "zhonyasringshield" ||
                                    m.Name.ToLowerInvariant() == "bardrstasis")))
            {

                var buffTime = enemy.Buffs.FirstOrDefault(m => m.Name.ToLowerInvariant() == "zhonyasringshield" ||
                                                               m.Name.ToLowerInvariant() == "bardrstasis");
                if (buffTime != null && buffTime.EndTime - Game.Time > 1.45f)
                {
                    W.Cast(enemy.ServerPosition);
                    break;
                }

                var ga =
                    ObjectManager.Get<Obj_GeneralParticleEmitter>()
                        .Where(
                            x =>
                                x.Name == "LifeAura.troy" && W.IsInRange(x.Position) &&
                                x.Team != Player.Instance.Team)
                        .ToList();

                if (!ga.Any())
                    continue;

                foreach (var objGeneralParticleEmitter in ga)
                {
                    W.Cast(objGeneralParticleEmitter.Position);
                    break;
                }
            }
        }
    }
}