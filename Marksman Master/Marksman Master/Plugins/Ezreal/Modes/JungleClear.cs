#region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="JungleClear.cs" company="EloBuddy">
// // 
// //  Marksman AIO
// // 
// //  Copyright (C) 2016 Krystian Tenerowicz
// // 
// //  This program is free software: you can redistribute it and/or modify
// //  it under the terms of the GNU General Public License as published by
// //  the Free Software Foundation, either version 3 of the License, or
// //  (at your option) any later version.
// // 
// //  This program is distributed in the hope that it will be useful,
// //  but WITHOUT ANY WARRANTY; without even the implied warranty of
// //  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// //  GNU General Public License for more details.
// // 
// //  You should have received a copy of the GNU General Public License
// //  along with this program.  If not, see http://www.gnu.org/licenses/. 
// //  </copyright>
// //  <summary>
// // 
// //  Email: geroelobuddy@gmail.com
// //  PayPal: geroelobuddy@gmail.com
// //  </summary>
// //  ---------------------------------------------------------------------
#endregion

using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Ezreal.Modes
{
    internal class JungleClear : Ezreal
    {
        public static void Execute()
        {
            var jungleMinions = EntityManager.MinionsAndMonsters.GetJungleMonsters(Player.Instance.Position, Player.Instance.GetAutoAttackRange()).ToList();

            if (!jungleMinions.Any())
                return;

            string[] allowedMonsters =
            {
                "SRU_Gromp", "SRU_Blue", "SRU_Red", "SRU_Razorbeak", "SRU_Krug", "SRU_Murkwolf", "Sru_Crab",
                "SRU_RiftHerald", "SRU_Dragon_Fire", "SRU_Dragon_Earth", "SRU_Dragon_Air", "SRU_Dragon_Elder",
                "SRU_Dragon_Water", "SRU_Baron"
            };

            if (!Q.IsReady() || !Settings.LaneClear.UseQInJungleClear || Player.Instance.HasSheenBuff() ||
                jungleMinions.Count(x => allowedMonsters.Contains(x.BaseSkinName, StringComparer.CurrentCultureIgnoreCase)) < 1 ||
                !(Player.Instance.ManaPercent >= Settings.LaneClear.MinManaQ))
                return;

            {
                foreach (var minion in from minion in jungleMinions.Where(x => x.IsValidTarget(Q.Range) && allowedMonsters.Any(k=> k.Contains(x.BaseSkinName)) && Q.GetPrediction(x).HitChance == HitChance.High) let health = Prediction.Health.GetPrediction(minion, (int)((minion.Distance(Player.Instance) + Q.CastDelay) / Q.Speed * 1000)) where health > 10 select minion)
                {
                    if (Orbwalker.LastTarget != null && Orbwalker.LastTarget.NetworkId == minion.NetworkId &&
                        Player.Instance.GetAutoAttackDamage(minion, true) < minion.Health && !IsPreAttack)
                    {
                        Q.Cast(minion);
                        return;
                    }

                    if (IsPreAttack || (Orbwalker.LastTarget != null && Orbwalker.LastTarget.NetworkId == minion.NetworkId))
                        continue;

                    Q.Cast(minion);
                    return;
                }
            }
        }
    }
}
