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
            if (IsPostAttack && Q.IsReady() && Settings.Combo.UseQ && (!Settings.Combo.UseQOnlyToProcW ||
                 ((Orbwalker.LastTarget.GetType() == typeof (AIHeroClient)) &&
                  HasSilverDebuff((AIHeroClient) Orbwalker.LastTarget) &&
                  (GetSilverDebuff((AIHeroClient) Orbwalker.LastTarget).Count == 1))))
            {
                var enemies = Player.Instance.CountEnemiesInRangeCached(2000);
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 320, DamageType.Physical);
                var position = Vector3.Zero;

                if (!Settings.Misc.QSafetyChecks)
                {
                    if (!Player.Instance.Position.Extend(Game.CursorPos, 300).To3D().IsVectorUnderEnemyTower())
                    {
                        var positions =
                            StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero)
                                .Select(x => Prediction.Position.PredictUnitPosition(x, 300));

                        if (positions.Any(x => Player.Instance.IsInRange(x, Player.Instance.GetAutoAttackRange())))
                        {
                            Q.Cast(Player.Instance.Position.Extend(Game.CursorPos, 285).To3D());
                            return;
                        }
                    }
                }
                else
                {
                    switch (Settings.Misc.QMode)
                    {
                        case 1:
                            if ((target != null) && (Player.Instance.HealthPercent > target.HealthPercent) && (Player.Instance.HealthPercent > 10) &&
                                (target.CountEnemiesInRangeCached(1000) <= 2))
                            {
                                if ((!Player.Instance.Position.Extend(Game.CursorPos, 285)
                                    .To3D()
                                    .IsVectorUnderEnemyTower() &&
                                     (!target.IsMelee ||
                                      !Player.Instance.Position.Extend(Game.CursorPos, 285).IsInRangeCached(target.Position, target.GetAutoAttackRange()*1.5f))) || !target.IsMovingTowards(Player.Instance, 300))
                                {
                                    var qPosition = Player.Instance.Position.Extend(Game.CursorPos, 300).To3D();
                                    var unitPosition = Prediction.Position.PredictUnitPosition(target, 300);

                                    if (Settings.Combo.BlockQsOutOfAaRange &&
                                        !qPosition.IsInRangeCached(unitPosition, Player.Instance.GetAutoAttackRange()))
                                    {
                                        return;
                                    }

                                    Misc.PrintDebugMessage("1v1 Game.CursorPos");
                                    Q.Cast(qPosition);
                                    return;
                                }
                            }
                            else if (target != null)
                            {
                                var closest = StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                        x => x.IsValidTargetCached(2000)).OrderBy(x => x.DistanceCached(Player.Instance)).ToArray()[0];

                                var list =
                                    SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 900,
                                        2000, 600)
                                        .Where(
                                            x => (x.Key.CutVectorNearWall(320).Distance(Player.Instance) > 250) && (x.Value <= 1))
                                        .Select(source => source.Key)
                                        .ToList();

                                if (list.Any())
                                {
                                    var range = enemies * 125;

                                    var paths =
                                        StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                            x => x.IsValidTargetCached(2000))
                                            .Count(x => x.IsMovingTowards(Player.Instance, range < x.GetAutoAttackRange() ? (int)x.GetAutoAttackRange() : range));

                                    if ((Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange()) ==
                                         0) || (paths == 0))
                                    {
                                        if (Settings.Combo.BlockQsOutOfAaRange)
                                        {
                                            var positions =
                                                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero)
                                                        .Select(x => Prediction.Position.PredictUnitPosition(x, 300));
                                            
                                            list.ForEach(x =>
                                            {
                                                if (!positions.Any(p => x.IsInRangeCached(p, Player.Instance.GetAutoAttackRange())))
                                                {
                                                    list.Remove(x);
                                                }
                                            });
                                        }

                                        position = Misc.SortVectorsByDistance(list, closest.Position.To2D())[0].To3D();

                                        Misc.PrintDebugMessage("Paths low sorting Ascending");
                                    }
                                    else if ((Player.Instance.CountEnemiesInRangeCached(1000) <= 2) &&
                                             (paths <= 2) &&
                                             list.Any(
                                                 x =>
                                                     x.IsInRangeCached(
                                                         Prediction.Position.PredictUnitPosition(target, 300),
                                                         Player.Instance.GetAutoAttackRange() - 50)) &&
                                             (target.Health <
                                              Player.Instance.GetAutoAttackDamage(target)*2f +
                                              Damage.QBonusDamage[Q.Level]))
                                    {
                                        position =
                                            Misc.SortVectorsByDistance(
                                                list.Where(
                                                    x =>
                                                        x.IsInRangeCached(
                                                            Prediction.Position.PredictUnitPosition(target, 300),
                                                            Player.Instance.GetAutoAttackRange() - 50)).ToList(),
                                                target.Position.To2D())[0].To3D();
                                        Misc.PrintDebugMessage("Paths low sorting Ascending");
                                    }
                                    else
                                    {
                                        position =
                                            Misc.SortVectorsByDistanceDescending(list, target.Position.To2D())[0]
                                                .To3D();
                                        Misc.PrintDebugMessage("Paths high sorting Descending");
                                    }
                                }
                                else
                                {
                                    position =
                                        Misc.SortVectorsByDistanceDescending(
                                            SafeSpotFinder.PointsInRange(Player.Instance.Position.To2D(), 400).ToList(),
                                            closest.Position.To2D())[0]
                                            .To3D();

                                    Misc.PrintDebugMessage("not found positions...");
                                }
                            }

                            if ((position != Vector3.Zero) &&
                                StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                    x => x.IsValidTargetCached(Player.Instance.GetAutoAttackRange() + 300) && (Prediction.Health.GetPrediction(x, 500) > 0)).Any())
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
                                        var unitPosition = Prediction.Position.PredictUnitPosition(target, 300);
                                        
                                        if (!unitPosition.IsInRangeCached(pos, 300) || !target.IsMovingTowards(Player.Instance, 300))
                                        {
                                            if (Settings.Combo.BlockQsOutOfAaRange && !pos.IsInRangeCached(unitPosition, Player.Instance.GetAutoAttackRange()))
                                            {
                                                return;
                                            }

                                            Q.Cast(pos);
                                            return;
                                        }
                                    }
                                    else if ((enemies == 2) && ((Player.Instance.CountAlliesInRangeCached(400) > 1) ||
                                                (StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTarget(1200) && x.IsMovingTowards(Player.Instance, 450)).Count() <= 1)))
                                    {
                                        if (Settings.Combo.BlockQsOutOfAaRange)
                                        {
                                            var positions =
                                                StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero)
                                                    .Select(x => Prediction.Position.PredictUnitPosition(x, 300));

                                            if (!positions.Any(x => pos.IsInRangeCached(x, Player.Instance.GetAutoAttackRange())))
                                            {
                                                return;
                                            }
                                        }

                                        Q.Cast(pos);
                                        return;
                                    }
                                    else
                                    {
                                        var range = enemies * 150;

                                        if (!StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x =>
                                            pos.IsInRangeCached(Prediction.Position.PredictUnitPosition(x, 300), range < x.GetAutoAttackRange() ? x.GetAutoAttackRange() : range)).Any())
                                        {
                                            Q.Cast(pos);

                                            return;
                                        }
                                    }
                                }

                                var closest = StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                        x => x.IsValidTargetCached(1300)).OrderBy(x => x.DistanceCached(Player.Instance)).FirstOrDefault();

                                var paths =
                                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                                        x => x.IsValidTargetCached(1300))
                                        .Count(x => x.IsMovingTowards(Player.Instance, 300));

                                if ((closest != null) && (Player.Instance.CountEnemiesInRangeCached(350) >= 1) && (paths >= 1) && (pos.DistanceCached(closest) > Player.Instance.DistanceCached(closest)))
                                {
                                    Q.Cast(pos);
                                }
                            }
                            break;
                        default:
                            return;
                    }
                }
            }

            if (!IsPreAttack && Q.IsReady() && Settings.Combo.UseQ && Settings.Combo.UseQToPoke)
            {
                var enemies = Player.Instance.CountEnemiesInRangeCached(1200);
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 300, DamageType.Physical);
                var position = Player.Instance.Position.Extend(Game.CursorPos, 299).To3D();

                if ((target != null) && !target.IsMovingTowards(Player.Instance, 300) &&
                    (Player.Instance.HealthPercent > target.HealthPercent) &&
                    !Player.Instance.IsInAutoAttackRange(target) && (enemies == 1))
                {
                    var targetPos = Prediction.Position.PredictUnitPosition(target, 370);

                    if (!targetPos.IsInRange(position, 300) &&
                        position.IsInRange(targetPos, Player.Instance.GetAutoAttackRange()))
                    {
                        Q.Cast(position);
                    }
                }
            }

            if (E.IsReady() && Settings.Combo.UseE && (Settings.Misc.EMode == 1))
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Physical);

                if (target != null)
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

            if (!R.IsReady() || !Settings.Combo.UseR)
                return;
            {
                var enemies = Player.Instance.CountEnemiesInRangeCached(Player.Instance.GetAutoAttackRange() + 330);
                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 330, DamageType.Physical);

                if ((target == null) || (Orbwalker.LastTarget.GetType() != typeof (AIHeroClient)) || (enemies < 3) ||
                    !(Player.Instance.HealthPercent > 25))
                    return;

                R.Cast();
            }
        }
    }
}