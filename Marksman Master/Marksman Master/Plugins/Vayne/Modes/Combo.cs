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
using Marksman_Master.Utils;
using SharpDX;

namespace Marksman_Master.Plugins.Vayne.Modes
{
    internal class Combo : Vayne
    {
        public static void Execute()
        {
            if (IsPostAttack && Q.IsReady() && Settings.Combo.UseQ && (!Settings.Combo.UseQOnlyToProcW || (Orbwalker.LastTarget.GetType() == typeof(AIHeroClient) && HasSilverDebuff((AIHeroClient)Orbwalker.LastTarget) && GetSilverDebuff((AIHeroClient)Orbwalker.LastTarget).Count == 1)))
            {
                var enemies = Player.Instance.CountEnemiesInRangeCached(1300);
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 300, DamageType.Physical);
                var position = Vector3.Zero;

                if (!Settings.Misc.QSafetyChecks)
                {
                    if (!Player.Instance.Position.Extend(Game.CursorPos, 300).To3D().IsVectorUnderEnemyTower())
                    {
                        Q.Cast(Player.Instance.Position.Extend(Game.CursorPos, 285).To3D());
                        return;
                    }
                }
                else
                {
                    switch (Settings.Misc.QMode)
                    {
                        case 1:
                            if (target != null && Player.Instance.HealthPercent > 50 && target.HealthPercent < 30 && target.CountEnemiesInRangeCached(600) < 2)
                            {
                                if (!Player.Instance.Position.Extend(Game.CursorPos, 285)
                                    .To3D()
                                    .IsVectorUnderEnemyTower() && (!target.IsMelee || Player.Instance.Position.Extend(Game.CursorPos, 285).To3D().IsInRangeCached(target.Position, target.GetAutoAttackRange() * 1.5f)))
                                {
                                    Misc.PrintDebugMessage("1v1 Game.CursorPos");
                                    position = Player.Instance.Position.Extend(Game.CursorPos, 285).To3D();
                                }
                            }
                            else if (target != null)
                            {
                                var closest =
                                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTargetCached(1300))
                                        .OrderBy(x => x.DistanceCached(Player.Instance)).ToArray()[0];

                                var list =
                                    SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 900,
                                        1300,
                                        target.IsMelee ? target.GetAutoAttackRange() * 2 : target.GetAutoAttackRange()).Where(x => !x.Key.To3D().IsVectorUnderEnemyTower() && x.Key.IsInRangeCached(Prediction.Position.PredictUnitPosition(closest, 850), Player.Instance.GetAutoAttackRange() - 50)).Select(source => source.Key).ToList();

                                if (list.Any())
                                {
                                    var paths =
                                        StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTargetCached(1300))
                                            .Select(x => x.Path)
                                            .Count(result => result != null && result.Last().DistanceCached(Player.Instance.Position) < 300);

                                    var asc = Misc.SortVectorsByDistance(list, target.Position.To2D())[0].To3D();
                                    if (Player.Instance.CountEnemiesInRange(Player.Instance.GetAutoAttackRange()) == 0 &&
                                        !StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.DistanceCached(Player.Instance) < 1000).Any(
                                            x => Prediction.Position.PredictUnitPosition(x, 800)
                                                .IsInRangeCached(asc,
                                                    x.IsMelee ? x.GetAutoAttackRange() * 2 : x.GetAutoAttackRange())))
                                    {
                                        position = asc;

                                        Misc.PrintDebugMessage("Paths low sorting Ascending");
                                    }
                                    else if (Player.Instance.CountEnemiesInRangeCached(1000) <= 2 && (paths == 0 || paths == 1) && ((closest.Health < Player.Instance.GetAutoAttackDamage(closest, true) * 2) || (Orbwalker.LastTarget is AIHeroClient && Orbwalker.LastTarget.Health < Player.Instance.GetAutoAttackDamage(closest, true) * 2)))
                                    {
                                        position = asc;
                                    }
                                    else
                                    {
                                        position =
                                            Misc.SortVectorsByDistanceDescending(list, target.Position.To2D())[0].To3D();
                                        Misc.PrintDebugMessage("Paths high sorting Descending");
                                    }
                                }
                                else Misc.PrintDebugMessage("1v1 not found positions...");
                                
                            }
                            
                            if (position != Vector3.Zero && StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTargetCached(900)).Any())
                            {
                                Q.Cast(Player.Instance.Position.Extend(position, 285).To3D());
                                return;
                            }
                            break;
                        case 0:
                            var pos = Player.Instance.Position.Extend(Game.CursorPos, 299).To3D();

                            if (!pos.IsVectorUnderEnemyTower())
                            {
                                if (target != null)
                                {
                                    if (enemies == 1)
                                    {
                                        if (target.IsMelee && !pos.IsInRangeCached(Prediction.Position.PredictUnitPosition(target, 850), target.GetAutoAttackRange() + 150))
                                        {
                                            Q.Cast(pos);
                                            return;
                                        }
                                        if (!target.IsMelee)
                                        {
                                            Q.Cast(pos);
                                            return;
                                        }
                                    }
                                    else if (enemies == 2 && Player.Instance.CountAlliesInRangeCached(850) >= 1)
                                    {
                                        Q.Cast(pos);
                                        return;
                                    }
                                    else if (enemies >= 2)
                                    {
                                        
                                        if (!StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x =>
                                                    pos.IsInRangeCached(Prediction.Position.PredictUnitPosition(x, 400),
                                                        x.IsMelee ? x.GetAutoAttackRange() + 150 : x.GetAutoAttackRange())).Any())
                                        {
                                            Q.Cast(pos);
                                            return;
                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            return;
                    }
                }
            }

            if (E.IsReady() && Settings.Combo.UseE && Settings.Misc.EMode == 1)
            {
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 300, DamageType.Physical);

                if (target != null)
                {
                    var enemies = Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange() + 300);

                    if (WillEStun(target))
                    {
                        E.Cast(target);
                        return;
                    }
                    if (enemies > 1)
                    {
                        foreach (
                            var enemy in
                                StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                    x => x.IsValidTargetCached(E.Range) && WillEStun(x))
                                    .OrderByDescending(TargetSelector.GetPriority))
                        {
                            E.Cast(enemy);
                            return;
                        }
                    }
                }
            }

            if (!R.IsReady() || !Settings.Combo.UseR)
                return;

            {
                var enemies = Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange() + 330);
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 330, DamageType.Physical);

                if (target == null || Orbwalker.LastTarget.GetType() != typeof (AIHeroClient) || enemies < 3 ||
                    !(Player.Instance.HealthPercent > 25))
                    return;

                R.Cast();
            }
        }
    }
}