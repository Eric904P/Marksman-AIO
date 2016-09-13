#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Harass.cs" company="EloBuddy">
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
using EloBuddy;
using EloBuddy.SDK;

namespace Marksman_Master.Plugins.MissFortune.Modes
{
    internal class Harass : MissFortune
    {
        public static void Execute()
        {
            if (!Q.IsReady() || !Settings.Harass.UseQ || IsPreAttack ||
                !(Player.Instance.ManaPercent >= Settings.Harass.MinManaQ))
                return;

            var qTarget = Q.GetTarget();

            if (qTarget == null)
                return;

            if (Settings.Misc.BounceQFromMinions)
            {
                var minion = GetQMinion(qTarget);
                if (minion != null)
                {
                    Q.Cast(minion);
                } else if (qTarget.IsValidTarget(Q.Range)) Q.Cast(qTarget);
            } else if(qTarget.IsValidTarget(Q.Range)) Q.Cast(qTarget);
        }
    }
}
