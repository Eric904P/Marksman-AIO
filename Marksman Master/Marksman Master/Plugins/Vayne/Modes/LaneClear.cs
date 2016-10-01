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
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Vayne.Modes
{
    internal class LaneClear : Vayne
    {
        public static bool CanILaneClear()
        {
            return !Settings.LaneClear.EnableIfNoEnemies || Player.Instance.CountEnemiesInRange(Settings.LaneClear.ScanRange) <= Settings.LaneClear.AllowedEnemies;
        }

        public static void Execute()
        {
            var laneMinions = StaticCacheProvider.GetMinions(CachedEntityType.EnemyMinion,
                    x => x.IsValidTargetCached(Player.Instance.GetAutoAttackRange() + 300)).ToList();

            if (!laneMinions.Any() || !CanILaneClear())
                return;

            if (!Q.IsReady() || !IsPostAttack || !Settings.LaneClear.UseQToLaneClear ||
                !(Player.Instance.ManaPercent >= Settings.LaneClear.MinMana))
                return;

            var delay = (int)(Orbwalker.AttackCastDelay*1000);

            var minion = laneMinions.Where(x=>
                        (Prediction.Health.GetPrediction(x, 250 + delay) <
                        Player.Instance.GetAutoAttackDamageCached(x, true) +
                        Player.Instance.TotalAttackDamage*Damage.QBonusDamage[Q.Level]) && (Prediction.Health.GetPrediction(x, 350 + delay) > 10)).OrderBy(x=>x.DistanceCached(Player.Instance));

            if (!minion.Any())
                return;

            if (Player.Instance.Position.Extend(Game.CursorPos, 299)
                .IsInRangeCached(minion.First().Position.To2D(), Player.Instance.GetAutoAttackRange()) && StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, e => Player.Instance.Position.Extend(Game.CursorPos, 299).IsInRangeCached(e.Position, e.IsMelee ? e.GetAutoAttackRange() * 2 : e.GetAutoAttackRange())).Any() == false)
            {
                Q.Cast(Player.Instance.Position.Extend(Game.CursorPos, 285).To3D());
            }
        }
    }
}
