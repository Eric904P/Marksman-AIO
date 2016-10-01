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
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Jinx.Modes
{
    internal class Combo : Jinx
    {
        public static void Execute()
        {
            if (Q.IsReady() && Settings.Combo.UseQ)
            {
                var target = TargetSelector.GetTarget(GetRealRocketLauncherRange(), DamageType.Physical);

                if (target != null)
                {
                    if (target.Distance(Player.Instance) < GetRealMinigunRange() && HasRocketLauncher &&
                        target.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamage(target, true)*2.2f)
                    {
                        Q.Cast();
                        return;
                    }

                    if (target.Distance(Player.Instance) > GetRealMinigunRange() &&
                        target.Distance(Player.Instance) < GetRealRocketLauncherRange() && !HasRocketLauncher)
                    {
                        Q.Cast();
                        return;
                    }
                    if (HasMinigun && GetMinigunStacks >= 2 &&
                        target.TotalHealthWithShields() < Player.Instance.GetAutoAttackDamage(target, true)*2.2f && target.TotalHealthWithShields() > Player.Instance.GetAutoAttackDamage(target, true) * 2f)
                    {
                        Q.Cast();
                        return;
                    }
                }
            }

            if (W.IsReady() && Settings.Combo.UseW &&
                Player.Instance.CountEnemiesInRangeCached(Settings.Combo.WMinDistanceToTarget) == 0 &&
                !Player.Instance.Position.IsVectorUnderEnemyTower() &&
                (Player.Instance.Mana - (50 + 10*(W.Level - 1)) > (R.IsReady() ? 100 : 50)))
            {
                var target =
                    EntityManager.Heroes.Enemies.Where(
                        x =>
                            x.IsValidTarget(W.Range) && !x.HasUndyingBuffA() && !x.HasSpellShield() &&
                            x.Distance(Player.Instance) > Settings.Combo.WMinDistanceToTarget)
                        .OrderByDescending(x => Player.Instance.GetSpellDamage(x, SpellSlot.W)).FirstOrDefault();

                var orbwalkerTarget = Orbwalker.GetTarget();

                if (orbwalkerTarget != null && orbwalkerTarget.GetType() == typeof (AIHeroClient))
                {
                    var wt = orbwalkerTarget as AIHeroClient;
                    if (wt != null && wt.IsValidTarget(W.Range) && !wt.HasUndyingBuffA() && !wt.HasSpellShield() &&
                        wt.Distance(Player.Instance) > Settings.Combo.WMinDistanceToTarget)
                    {
                        var wPrediction = W.GetPrediction(wt);
                        if (wPrediction.HitChance == HitChance.High)
                        {
                            W.Cast(wPrediction.CastPosition);
                            return;
                        }
                    }
                }
                else if (target != null)
                {
                    var wPrediction = W.GetPrediction(target);
                    if (wPrediction.HitChance == HitChance.High)
                    {
                        W.Cast(wPrediction.CastPosition);
                        return;
                    }
                }
            }

            if (E.IsReady() && Settings.Combo.UseE && Player.Instance.Mana - 50 > 100)
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Physical);

                if (target != null)
                {
                    var ePrediction = E.GetPrediction(target);
                    if (ePrediction.HitChance == HitChance.High && ePrediction.CastPosition.Distance(target) > 125)
                    {
                        E.Cast(ePrediction.CastPosition);
                        return;
                    }
                }
            }

            if (!R.IsReady() || !Settings.Combo.UseR || Player.Instance.Position.IsVectorUnderEnemyTower())
                return;

            var t = TargetSelector.GetTarget(4500, DamageType.Physical);

            if (t == null || t.HasUndyingBuffA() || (Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange() + 50) > 0) || ((t.Health < Player.Instance.GetAutoAttackDamageCached(t, true)*1.8f) && Player.Instance.IsInAutoAttackRange(t)))
                return;

            var health = t.TotalHealthWithShields() - IncomingDamage.GetIncomingDamage(t);

            if (health > 0 && (health < Damage.GetRDamage(t)) && R.GetHealthPrediction(t) > 0)
            {
                var rPrediction = R.GetPrediction(t);

                if (rPrediction.HitChancePercent < 65)
                    return;

                R.Cast(rPrediction.CastPosition);
                Misc.PrintDebugMessage("KS ULT");
            }
            else
            {
                R.CastIfItWillHit(4, 60);

                //var rPrediction = R.GetPrediction(t);

                //if (t.CountEnemiesInRange(225) < 5 || rPrediction.HitChancePercent < 65)
                //    return;

                //R.Cast(rPrediction.CastPosition);
                Misc.PrintDebugMessage("AOE ULT");
            }
        }
    }
}
