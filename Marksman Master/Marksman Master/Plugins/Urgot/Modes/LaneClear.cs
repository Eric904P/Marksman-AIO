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

namespace Marksman_Master.Plugins.Urgot.Modes
{
    using Utils;

    internal class LaneClear : Urgot
    {
        public static bool CanILaneClear()
        {
            return !Settings.LaneClear.EnableIfNoEnemies || Player.Instance.CountEnemiesInRange(Settings.LaneClear.ScanRange) <= Settings.LaneClear.AllowedEnemies;
        }

        public static void Execute()
        {
            var laneMinions = StaticCacheProvider.GetMinions(CachedEntityType.EnemyMinion, x => x.IsValidTarget() && IsInQRange(x)).ToList();

            if (!laneMinions.Any())
            {
                return;
            }

            if (Q.IsReady() && (Player.Instance.ManaPercent >= Settings.LaneClear.MinManaQ))
            {
                if (CanILaneClear() && Settings.LaneClear.UseQInLaneClear && CorrosiveDebufTargets.Any(unit => unit is Obj_AI_Minion && unit.IsValidTarget(1300)))
                {
                    if (CorrosiveDebufTargets.Any(unit => unit is Obj_AI_Minion && unit.IsValidTarget(1300)))
                    {
                        foreach (
                            var minion in
                                from minion in
                                    CorrosiveDebufTargets.Where(
                                        unit => unit is Obj_AI_Minion && unit.IsValidTarget(1300))
                                let hpPrediction = Prediction.Health.GetPrediction(minion,
                                    (int) (minion.Distance(Player.Instance)/1550*1000 + 250))
                                where
                                    hpPrediction > 0 &&
                                    hpPrediction < Player.Instance.GetSpellDamage(minion, SpellSlot.Q)
                                select minion)
                        {
                            Q.Cast(minion.Position);
                            return;
                        }
                    }
                }
                else if (CanILaneClear() && Settings.LaneClear.UseQInLaneClear)
                {
                    foreach (var minion in (from minion in laneMinions let hpPrediction = Prediction.Health.GetPrediction(minion,
                        (int) (minion.Distance(Player.Instance)/1550*1000 + 250)) where (hpPrediction > 0) &&
                                                                                        (hpPrediction < Player.Instance.GetSpellDamage(minion, SpellSlot.Q)) let qPrediction = Q.GetPrediction(minion) where qPrediction.Collision == false select minion).Where(minion => !minion.IsDead))
                    {
                        Q.Cast(minion);
                        return;
                    }
                }
            }

            if (!E.IsReady() || !(Player.Instance.ManaPercent >= Settings.LaneClear.MinManaE))
                return;

            if (Settings.Combo.UseE && Settings.Misc.AutoHarass && EntityManager.Heroes.Enemies.Any(x => x.IsValidTarget(E.Range)))
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Physical);

                if (target == null)
                    return;

                var ePrediction = E.GetPrediction(target);

                if (ePrediction.HitChance < HitChance.High)
                    return;

                if (!(Player.Instance.Spellbook.GetSpell(SpellSlot.Q).CooldownExpires - Game.Time < 1) &&
                    !(target.Health < Player.Instance.GetSpellDamage(target, SpellSlot.E)))
                    return;

                E.Cast(ePrediction.CastPosition);
                return;
            }

            if (!CanILaneClear() || !Settings.LaneClear.UseEInLaneClear || (Player.Instance.CountEnemyMinionsInRangeCached(900) <= 3))
                return;

            E.CastOnBestFarmPosition(1);
        }
    }
}