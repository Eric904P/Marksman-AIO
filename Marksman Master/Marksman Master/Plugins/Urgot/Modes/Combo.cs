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

using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;

namespace Marksman_Master.Plugins.Urgot.Modes
{
    using Utils;

    internal class Combo : Urgot
    {
        public static void Execute()
        {
            if (R.IsReady() && Settings.Combo.UseR && StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero).Any(x => x.IsValidTargetCached(R.Range)) && (Player.Instance.Mana >= 300))
            {
                var target = TargetSelector.GetTarget(R.Range, DamageType.Physical);

                if (target != null)
                {
                    var damage = Player.Instance.GetAutoAttackDamage(target, true) * 2;

                    if (IsInQRange(target))
                        damage += Player.Instance.GetSpellDamageCached(target, SpellSlot.Q)*2;

                    if ((damage > target.Health) && (target.HealthPercent > 40) && (target.Position.CountEnemiesInRange(600) < 2) && (Player.Instance.HealthPercent > target.HealthPercent) && !target.IsUnderTurret())
                    {
                        R.Cast(target);
                        return;
                    }
                    if (Player.Instance.IsUnderTurret() && (Player.Instance.HealthPercent > 25) && (Player.Instance.HealthPercent > target.HealthPercent))
                    {
                        R.Cast(target);
                        return;
                    }
                }
            }

            if (E.IsReady() && Settings.Combo.UseE)
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Physical);

                if (target != null)
                {
                    var ePrediction = E.GetPrediction(target);

                    if (ePrediction.HitChance >= HitChance.High)
                    {
                        if ((QCooldown < 1) || (target.Health < Player.Instance.GetSpellDamageCached(target, SpellSlot.E)))
                        {
                            E.Cast(ePrediction.CastPosition);
                            return;
                        }
                    }
                }
            }

            if (Q.IsReady() && Settings.Combo.UseQ)
            {
                foreach (
                    var corrosiveDebufTarget in
                        CorrosiveDebufTargets.Where(
                            unit => (unit.Type == GameObjectType.AIHeroClient) && unit.IsValidTargetCached(1300)))
                {
                    Player.CastSpell(SpellSlot.Q, corrosiveDebufTarget.Position);
                    return;
                }

                var target = TargetSelector.GetTarget(Q.Range, DamageType.Physical);

                if (target != null)
                {
                    var qPrediciton = Q.GetPrediction(target);

                    if (qPrediciton.HitChance >= HitChance.High)
                    {
                        Q.Cast(qPrediciton.CastPosition);

                        return;
                    }
                }
            }

            if (!W.IsReady() || !Settings.Combo.UseW || (Player.Instance.Mana - 50 + 5*(E.Level - 1) < 120 + (R.IsReady() ? 100 : 0)))
                return;
            {
                if ((Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange()) < 1) ||
                    !CorrosiveDebufTargets.Any(
                        unit => (unit.Type == GameObjectType.AIHeroClient) && unit.IsValidTargetCached(1300)))
                    return;

                W.Cast();
            }
        }
    }
}