#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Vayne.cs" company="EloBuddy">
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Marksman_Master.Cache.Modules;
using Marksman_Master.PermaShow.Values;
using Marksman_Master.Utils;
using SharpDX;
using Color = SharpDX.Color;
using Text = EloBuddy.SDK.Rendering.Text;

namespace Marksman_Master.Plugins.Vayne
{
    internal class Vayne : ChampionPlugin
    {
        protected static Spell.Skillshot Q { get; }
        protected static Spell.Active W { get; }
        protected static Spell.Targeted E { get; }
        protected static Spell.Active R { get; }

        internal static Menu ComboMenu { get; set; }
        internal static Menu HarassMenu { get; set; }
        internal static Menu LaneClearMenu { get; set; }
        internal static Menu MiscMenu { get; set; }
        internal static Menu DrawingsMenu { get; set; }

        private BoolItem DontAa { get; set; }
        private BoolItem SafetyChecks { get; set; }

        protected static BuffInstance GetTumbleBuff
            =>
                Player.Instance.Buffs.FirstOrDefault(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vaynetumble");

        protected static bool HasTumbleBuff
            =>
                Player.Instance.Buffs.Any(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vaynetumble");

        protected static bool HasSilverDebuff(Obj_AI_Base unit)
            =>
                unit.Buffs.Any(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vaynesilverdebuff");

        protected static BuffInstance GetSilverDebuff(Obj_AI_Base unit)
            =>
                unit.Buffs.FirstOrDefault(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vaynesilverdebuff");

        protected static bool HasInquisitionBuff
            =>
                Player.Instance.Buffs.Any(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vayneinquisition");

        protected static BuffInstance GetInquisitionBuff
            =>
                Player.Instance.Buffs.FirstOrDefault(
                    b => b.IsActive && b.DisplayName.ToLowerInvariant() == "vayneinquisition");

        private static bool _changingRangeScan;
        private static float _lastQCastTime;
        private static readonly Text Text;
        private static readonly Text FlashCondemnText;

        protected static bool IsPostAttack { get; private set; }
        protected static bool IsPreAttack { get; private set; }

        protected static KeyBind FlashCondemnKeybind { get; set; }

        protected static Spell.Skillshot Flash { get; }

        protected static Vector3 FlashPosition { get; set; }

        protected static float LastTick { get; set; }

        protected static float LastQ { get; set; }
        protected static float LastE { get; set; }
        protected static bool Stun { get; set; }
        protected static Vector3 EEndPosition { get; set; }
        protected static GameObject ETarget { get; set; }

        static Vayne()
        {
            Q = new Spell.Skillshot(SpellSlot.Q, 320, SkillShotType.Linear);
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Targeted(SpellSlot.E, 765);
            R = new Spell.Active(SpellSlot.R);

            Orbwalker.OnPreAttack += Orbwalker_OnPreAttack;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Game.OnPostTick += args => IsPostAttack = false;

            ChampionTracker.Initialize(ChampionTrackerFlags.PostBasicAttackTracker);
            ChampionTracker.OnPostBasicAttack += ChampionTracker_OnPostBasicAttack;
            GameObject.OnCreate += Obj_AI_Base_OnCreate;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Obj_AI_Base.OnPlayAnimation += Obj_AI_Base_OnPlayAnimation;
            Messages.OnMessage += Messages_OnMessage;

            var flashSlot = Player.Instance.GetSpellSlotFromName("summonerflash");

            if (flashSlot == SpellSlot.Summoner1 || flashSlot == SpellSlot.Summoner2)
            {
                Flash = new Spell.Skillshot(flashSlot, 475, SkillShotType.Linear);
            }
            
            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));
            FlashCondemnText = new Text("", new Font("calibri", 25, FontStyle.Regular));

            TargetedSpells.Initialize();
        }

        private static void Obj_AI_Base_OnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if(sender.IsMe && args.Animation == "Spell1")
                Orbwalker.ResetAutoAttack();
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
#region Q
            if (Q.IsReady() && sender.IsEnemy && sender.Type == GameObjectType.AIHeroClient)
            {
                var enemy = sender as AIHeroClient;
                
                if (enemy != null )
                {
                    var positions = new Geometry.Polygon.Circle(Player.Instance.Position, 300, 50).Points;

                    switch (enemy.Hero)
                    {
                        case Champion.Alistar:
                            {
                                if (args.Slot == SpellSlot.Q)
                                {
                                    var polygon = new Geometry.Polygon.Circle(enemy.Position, 365);
                                    if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                    {
                                        Q.Cast(
                                            positions.FirstOrDefault(
                                                x =>
                                                    new Geometry.Polygon.Circle(x,
                                                        Player.Instance.BoundingRadius).Points.All(
                                                            p => polygon.IsOutside(p)) &&
                                                    (x.DistanceCached(enemy) - 50 >
                                                     Player.Instance.DistanceCached(enemy)))
                                                .To3D());
                                    }
                                }
                                break;
                            }
                        case Champion.Leona:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Circle(args.End, 150);
                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                    {
                                        Q.Cast(
                                            positions.FirstOrDefault(
                                                x =>
                                                    new Geometry.Polygon.Circle(x,
                                                        Player.Instance.BoundingRadius).Points.All(
                                                            p => polygon.IsOutside(p)) &&
                                                    (x.DistanceCached(enemy) - 50 >
                                                     Player.Instance.DistanceCached(enemy)))
                                                .To3D());
                                    }
                            }
                            break;
                        }
                        case Champion.Chogath:
                        {
                            if (args.Slot == SpellSlot.Q)
                            {
                                var polygon = new Geometry.Polygon.Circle(args.End, 180);

                                if (polygon.IsInside(Player.Instance))
                                {
                                    var qPos =
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius, 10).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D();

                                    Q.Cast(qPos);
                                }
                            }
                            break;
                        }
                        case Champion.Thresh:
                        {
                            if (args.Slot == SpellSlot.E)
                            {
                                var endPosition = enemy.Position.Extend(args.End, 400);
                                var startPosition = enemy.Position.Extend(endPosition, -400);
                                var polygon = new Geometry.Polygon.Rectangle(startPosition, endPosition, 90);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                            }
                        case Champion.Braum:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 1250).To3D(), 120);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                        case Champion.Sona:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 900).To3D(), 120);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                        case Champion.Ezreal:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 3000).To3D(), 120);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                        case Champion.Jinx:
                        case Champion.Ashe:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 3000).To3D(), 90);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                        case Champion.Draven:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 3000).To3D(), 120);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                        case Champion.Graves:
                        {
                            if (args.Slot == SpellSlot.R)
                            {
                                var polygon = new Geometry.Polygon.Rectangle(enemy.Position,
                                    enemy.Position.Extend(args.End, 1000).To3D(), 120);

                                if (polygon.IsInside(Player.Instance) && positions.Any(x => polygon.IsOutside(x)))
                                {
                                    Q.Cast(
                                        positions.FirstOrDefault(
                                            x =>
                                                new Geometry.Polygon.Circle(x,
                                                    Player.Instance.BoundingRadius).Points.All(
                                                        p => polygon.IsOutside(p)) &&
                                                (x.DistanceCached(enemy) - 50 >
                                                 Player.Instance.DistanceCached(enemy)))
                                            .To3D());
                                }
                            }
                            break;
                        }
                    }
                }
            }
#endregion
            if(!sender.IsMe)
                return;

            if (args.Slot == SpellSlot.Q)
            {
                LastQ = Game.Time * 1000;
            }

            if (args.Slot != SpellSlot.E || args.Target == null)
                return;
            
            if (FlashPosition != Vector3.Zero)
            {
                Flash.Cast(FlashPosition);
                FlashPosition = Vector3.Zero;
            }

            ETarget = args.Target;
            Stun = args.Target.Position.Extend(sender.Position, -475).CutVectorNearWall(1000).Distance(args.Target) <= 475;
            EEndPosition = args.Target.Position.Extend(sender.Position, -475).CutVectorNearWall(1000).To3D();
            LastE = Game.Time*1000;
        }

        private static void Messages_OnMessage(Messages.WindowMessage args)
        {
            if (FlashCondemnKeybind == null)
                return;

            if (args.Message == WindowMessages.KeyDown)
            {
                if (args.Handle.WParam == FlashCondemnKeybind.Keys.Item1 || args.Handle.WParam == FlashCondemnKeybind.Keys.Item2)
                {
                    Orbwalker.ActiveModesFlags |= Orbwalker.ActiveModes.Combo;
                }
            }

            if (args.Message != WindowMessages.KeyUp)
                return;

            if (args.Handle.WParam == FlashCondemnKeybind.Keys.Item1 || args.Handle.WParam == FlashCondemnKeybind.Keys.Item2)
            {
                Orbwalker.ActiveModesFlags = Orbwalker.ActiveModes.None;
                FlashPosition = Vector3.Zero;
            }
        }

        private static void ChampionTracker_OnPostBasicAttack(object sender, PostBasicAttackArgs e)
        {
            if (e.Sender == null || !e.Sender.IsMe)
                return;

            if (LastQ > 0)
                LastQ = 0;

            IsPreAttack = false;
            IsPostAttack = true;

            if (e.Target.GetType() == typeof (Obj_AI_Turret) && Q.IsReady() && Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear) && Settings.LaneClear.UseQToLaneClear)
            {
                if (Player.Instance.CountEnemiesInRangeCached(1000) == 0 &&
                    Player.Instance.ManaPercent >= Settings.LaneClear.MinMana)
                {
                    Q.Cast(Player.Instance.Position.Extend(Game.CursorPos, 285).To3D());
                }
            }

            if (e.Target == null || e.Target.GetType() != typeof(AIHeroClient) || !Settings.Misc.EKs || !e.Target.IsValid)
                return;

            var enemy = (AIHeroClient) e.Target;

            if (!enemy.IsValidTargetCached(E.Range) || !HasSilverDebuff(enemy) || GetSilverDebuff(enemy).Count != 1)
                return;

            if (!Damage.IsKillableFromSilverEAndAuto(enemy) ||
                (enemy.TotalHealthWithShields() - IncomingDamage.GetIncomingDamage(enemy) <= 0))
                return;

            Misc.PrintDebugMessage("casting e to ks");

            E.Cast(enemy);

            Misc.PrintInfoMessage($"Casting <b><blue>condemn</blue></b> to execute <c>{enemy.Hero}</c>");
        }


        private static bool HasAnyOrbwalkerFlags()
        {
            return (Orbwalker.ActiveModesFlags & (Orbwalker.ActiveModes.Combo | Orbwalker.ActiveModes.Harass | Orbwalker.ActiveModes.LaneClear | Orbwalker.ActiveModes.LastHit | Orbwalker.ActiveModes.JungleClear | Orbwalker.ActiveModes.Flee)) != 0;
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (args.Slot == SpellSlot.Q && HasAnyOrbwalkerFlags())
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                Orbwalker.ResetAutoAttack();
            }

            if (!Settings.Misc.NoAaWhileStealth || !HasInquisitionBuff)
                return;

            if (args.Slot == SpellSlot.Q)
            {
                _lastQCastTime = Game.Time*1000;
            }
        }

        private static void Obj_AI_Base_OnCreate(GameObject sender, EventArgs args)
        {
            if (sender == null || !StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.Hero == Champion.Rengar).Any())
                return;

            if (sender.Name != "Rengar_LeapSound.troy" || !E.IsReady() || Player.Instance.IsDead || Settings.Misc.EAntiRengar)
                return;

            foreach (var rengar in EntityManager.Heroes.Enemies.Where(x => x.ChampionName == "Rengar").Where(rengar => rengar.Distance(Player.Instance.Position) < 1000).Where(rengar => rengar.IsValidTarget(E.Range) && E.IsReady()))
            {
                Misc.PrintDebugMessage("casting e as anti-rengar");
                E.Cast(rengar);
            }
        }

        private static void Orbwalker_OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            IsPreAttack = true;

            if (!HasInquisitionBuff || !Settings.Misc.NoAaWhileStealth ||
                !(Game.Time*1000 - _lastQCastTime < Settings.Misc.NoAaDelay))
                return;

            var client = target as AIHeroClient;

            if (client != null && client.Health > Player.Instance.GetAutoAttackDamageCached(client, true)*3)
            {
                IsPreAttack = false;
                args.Process = false;
            }
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
            if (!E.IsReady() || !sender.IsValidTargetCached(E.Range))
                return;

            if (args.Delay == 0)
                E.Cast(sender);
            else Core.DelayAction(() => E.Cast(sender), args.Delay);

            Misc.PrintInfoMessage($"Interrupting <c>{sender.ChampionName}'s</c> <in>{args.SpellName}</in>");

            Misc.PrintDebugMessage($"OnInterruptible | Champion : {sender.ChampionName} | SpellSlot : {args.SpellSlot}");
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
            if (args.End.DistanceCached(Player.Instance.Position) > 300)
                return;

            if (!E.IsReady() || !sender.IsValidTargetCached(E.Range))
            {
                if (Q.IsReady())
                {
                    var list = SafeSpotFinder.PointsInRange(Player.Instance.Position.To2D(), 300, 300);
                    var closestEnemy = StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsValidTargetCached(1300))
                            .OrderBy(x => x.DistanceCached(Player.Instance))
                            .ToList()[0];

                    var positionToCast = Misc.SortVectorsByDistanceDescending(list.ToList(),
                        closestEnemy.Position.To2D())[0];

                    if (positionToCast != default(Vector2))
                    {
                        Misc.PrintDebugMessage($"OnGapcloser | Champion : {sender.ChampionName} | SpellSlot : {args.SpellSlot}");
                        Q.Cast(positionToCast.To3D());
                        return;
                    }
                }
            }

            if (args.Delay == 0)
                E.Cast(sender);
            else Core.DelayAction(() => E.Cast(sender), args.Delay);

            Misc.PrintDebugMessage($"OnGapcloser | Champion : {sender.ChampionName} | SpellSlot : {args.SpellSlot}");
        }

        protected static bool WillEStun(Obj_AI_Base target, Vector3 from = default(Vector3), int customHitchance = -1)
        {
            if (target == null || !IsECastableOnEnemy(target))
                return false;

            var hitchance = customHitchance > 0 ? customHitchance : Settings.Misc.EHitchance;
            var checkFrom = @from != default(Vector3) ? @from : Player.Instance.Position;
            var pushDistance = Settings.Misc.PushDistance;
            var eta = target.DistanceCached(checkFrom) / 1300;
            var prediction = Prediction.Position.PredictLinearMissile(target, E.Range, 40, 250, 1300, int.MaxValue, checkFrom, true);
            var predictedPosition = Prediction.Position.PredictUnitPosition(target, (int)(eta * 1000 + 250));
            var position = predictedPosition.Shorten(target.Position.To2D(), target.BoundingRadius / 2);

            if (target.GetMovementBlockedDebuffDuration() > eta + 0.25f)
            {
                for (var i = 25; i < pushDistance + 50; i += 50)
                {
                    if (!target.ServerPosition.Extend(checkFrom, -Math.Min(i, pushDistance)).IsWall())
                        continue;

                    return true;
                }
            }

            if (prediction.HitChance < HitChance.High)
                return false;

            for (var i = 100; i < pushDistance + 50; i += 50)
            {
                var max = i > pushDistance ? pushDistance : i;
                var vec = position.Extend(checkFrom, -max);
                var tPos = target.ServerPosition.Extend(checkFrom, -max);
                var polygon = new Geometry.Polygon.Circle(vec, target.BoundingRadius, 100);
                var unitDir = tPos.Normalized();

                Vector2[] vectors =
                {
                    tPos + 25*unitDir.Perpendicular()*unitDir,
                    tPos - 25*unitDir.Perpendicular()*unitDir,
                    tPos + 50*unitDir.Perpendicular()*unitDir,
                    tPos - 50*unitDir.Perpendicular()*unitDir,
                    tPos + 75*unitDir.Perpendicular()*unitDir,
                    tPos - 75*unitDir.Perpendicular()*unitDir,
                    tPos + target.BoundingRadius*unitDir.Perpendicular()*unitDir,
                    tPos - target.BoundingRadius*unitDir.Perpendicular()*unitDir
                };

                if (vec.IsWall() && (vectors.Count(x => x.IsWall())*12.5 >= hitchance) && tPos.IsWall() &&
                    (polygon.Points.Count(x => x.IsWall()) >= hitchance))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool FlashCondemnCheck(Obj_AI_Base target, Vector3 from = default(Vector3))
        {
            if (target == null)
                return false;

            var checkFrom = @from != default(Vector3) ? @from : Player.Instance.Position;
            const int pushDistance = 440;
            var prediction = Prediction.Position.PredictLinearMissile(target, E.Range, 40, 250, 1300, int.MaxValue, checkFrom, true);
            var eta = target.DistanceCached(checkFrom) / 1300;
            var predictedPosition = Prediction.Position.PredictUnitPosition(target, (int)(eta * 1000));
            var position = predictedPosition.Shorten(target.Position.To2D(), target.BoundingRadius / 2);

            if (prediction.HitChance < HitChance.High)
                return false;

            for (var i = 100; i < pushDistance + 50; i += 50)
            {
                var max = i > pushDistance ? pushDistance : i;
                var vec = position.Extend(checkFrom, -max);
                var tPos = target.ServerPosition.Extend(checkFrom, -max);
                var polygon = new Geometry.Polygon.Circle(vec, target.BoundingRadius, 100);
                var unitDir = tPos.Normalized();

                Vector2[] vectors =
                {
                    tPos + 25*unitDir.Perpendicular()*unitDir,
                    tPos - 25*unitDir.Perpendicular()*unitDir,
                    tPos + 50*unitDir.Perpendicular()*unitDir,
                    tPos - 50*unitDir.Perpendicular()*unitDir,
                    tPos + 75*unitDir.Perpendicular()*unitDir,
                    tPos - 75*unitDir.Perpendicular()*unitDir,
                    tPos + target.BoundingRadius*unitDir.Perpendicular()*unitDir,
                    tPos - target.BoundingRadius*unitDir.Perpendicular()*unitDir
                };

                if (vec.IsWall() && (vectors.Count(x => x.IsWall()) * 12.5 >= 70) && tPos.IsWall() &&
                    (polygon.Points.Count(x => x.IsWall()) >= 70))
                {
                    return true;
                }
            }
            return false;
        }

        protected static void PerformFlashCondemn()
        {
            if(Game.Time * 1000 - LastTick < 500 || !E.IsReady() || (!Q.IsReady() && !Flash.IsReady()))
                return;

            LastTick = Game.Time * 1000;

            var target = TargetSelector.SelectedTarget != null &&
                         TargetSelector.SelectedTarget.IsValidTargetCached(E.Range + 475)
                ? TargetSelector.SelectedTarget
                : TargetSelector.GetTarget(E.Range + 475, DamageType.Physical);

            if (target == null || FlashCondemnCheck(target))
                return;

            List<Vector2> points;

            if (Q.IsReady() && Player.Instance.IsInRangeCached(target, Q.Range))
            {
                if (FlashCondemnCheck(target, Player.Instance.Position.Extend(Game.CursorPos, 300).To3D()))
                {
                    Q.Cast(Player.Instance.Position.Extend(Game.CursorPos, 285).To3D());
                    return;
                }

                points = new Geometry.Polygon.Circle(Player.Instance.Position, 300, 30).Points.Where(
                        x => !x.To3D().IsVectorUnderEnemyTower() && FlashCondemnCheck(target, x.To3D())).ToList();

                var position = points.FirstOrDefault();

                if (position != default(Vector2))
                {
                    Q.Cast(Player.Instance.Position.Extend(position, 285).To3D());

                    return;
                }
            }

            if (Flash.IsReady())
            {
                if (FlashCondemnCheck(target, Player.Instance.Position.Extend(Game.CursorPos, 450).To3D()))
                {
                    E.Cast(target);
                    FlashPosition = Player.Instance.Position.Extend(Game.CursorPos, 450).To3D();
                    return;
                }

                points =
                    SafeSpotFinder.PointsInRange(target.Position.To2D(), 500, 100)
                        .Where(
                            x =>
                                !x.To3D().IsVectorUnderEnemyTower() && (x.Distance(Player.Instance) < 475) &&
                                (x.Distance(target) > 150) && FlashCondemnCheck(target, x.To3D()))
                        .ToList();

                foreach (var vector2 in points)
                {
                    E.Cast(target);
                    FlashPosition = vector2.To3D();

                    break;
                }
            }
        }

        protected static bool IsECastableOnEnemy(Obj_AI_Base unit)
        {
            return E.IsReady() && unit.IsValidTargetCached(E.Range) && !IsPreAttack && !unit.IsZombie &&
                   !unit.HasBuffOfType(BuffType.Invulnerability) && !unit.HasBuffOfType(BuffType.SpellImmunity) &&
                   !unit.HasBuffOfType(BuffType.SpellShield);
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(Color.White,
                    LaneClearMenu["Plugins.Vayne.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (!Settings.Drawings.DrawInfo)
                return;

            foreach (
                var source in
                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                        x => x.IsVisible && x.IsHPBarRendered && x.Position.IsOnScreen() && HasSilverDebuff(x)))
            {
                var hpPosition = source.HPBarPosition;
                hpPosition.Y = hpPosition.Y + 30; // tracker friendly.
                var timeLeft = GetSilverDebuff(source).EndTime - Game.Time;
                var endPos = timeLeft*0x3e8/32;

                var degree = Misc.GetNumberInRangeFromProcent(timeLeft*1000d/3000d*100d, 3, 110);
                var color = new Misc.HsvColor(degree, 1, 1).ColorFromHsv();

                Text.X = (int) (hpPosition.X + endPos);
                Text.Y = (int) hpPosition.Y + 15; // + text size
                Text.Color = color;
                Text.TextValue = timeLeft.ToString("F1");
                Text.Draw();

                Drawing.DrawLine(hpPosition.X + endPos, hpPosition.Y, hpPosition.X, hpPosition.Y, 1, color);
            }

            if (FlashCondemnKeybind.CurrentValue)
            {
                FlashCondemnText.Position = new Vector2(Drawing.Width * 0.4f, Drawing.Height*0.8f);
                FlashCondemnText.Color = System.Drawing.Color.Red;
                FlashCondemnText.TextValue = "FLASH CONDEMN IS ACTIVE !";
                FlashCondemnText.Draw();
            }

            if (Game.Time*1000 - LastE < 2000 && EEndPosition != default(Vector3))
            {
                var polygon = new Geometry.Polygon.Rectangle(ETarget.Position, EEndPosition, 15);

                Line.DrawLine(System.Drawing.Color.White, 2, polygon.Points[1].To3D(), polygon.Points[2].To3D());
                Line.DrawLine(System.Drawing.Color.White, 2, polygon.Points[0].To3D(), polygon.Points[1].To3D());
                Line.DrawLine(System.Drawing.Color.White, 2, polygon.Points[2].To3D(), polygon.Points[3].To3D());
                Line.DrawLine(System.Drawing.Color.White, 2, polygon.Points[0].To3D(), polygon.Points[3].To3D());

                Line.DrawLine(Stun ? System.Drawing.Color.YellowGreen : System.Drawing.Color.Red, 2, polygon.Start.To3D(), polygon.End.To3D());
            }
        }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Vayne addon");

            ComboMenu.AddLabel("Tumble (Q) settings :");
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseQToPoke", new CheckBox("Use Q to poke"));
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseQOnlyToProcW", new CheckBox("Use Q only to proc W stacks", false));
            ComboMenu.Add("Plugins.Vayne.ComboMenu.BlockQsOutOfAARange", new CheckBox("Don't use Q if it leaves range of target", false));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Condemn (E) settings :");
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseE", new CheckBox("Use E"));
            FlashCondemnKeybind = ComboMenu.Add("Plugins.Vayne.ComboMenu.FlashCondemn", new KeyBind("Flash condemn", false, KeyBind.BindTypes.HoldActive, 'A'));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Final Hour (R) settings :");
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseR", new CheckBox("Use R", false));
            ComboMenu.AddSeparator(5);

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Vayne addon");

            HarassMenu.AddLabel("Tumble (Q) settings :");
            HarassMenu.Add("Plugins.Vayne.HarassMenu.UseQ", new CheckBox("Use Q", false));
            HarassMenu.Add("Plugins.Vayne.HarassMenu.MinManaToUseQ",
                new Slider("Min mana percentage ({0}%) to use Q", 80, 1));
            HarassMenu.AddSeparator(5);

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear mode");
            LaneClearMenu.AddGroupLabel("Lane clear / Jungle Clear mode settings for Vayne addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.EnableLCIfNoEn",
                new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.ScanRange",
                new Slider("Range to scan for enemies", 1500, 0, 2500));
            scanRange.OnValueChange += (a, b) =>
            {
                _changingRangeScan = true;
                Core.DelayAction(() =>
                {
                    if (!scanRange.IsLeftMouseDown && !scanRange.IsMouseInside)
                    {
                        _changingRangeScan = false;
                    }
                }, 2000);
            };
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.AllowedEnemies",
                new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Tumble (Q) settings :");
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.UseQToLaneClear", new CheckBox("Use Q to lane clear"));
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.UseQToJungleClear", new CheckBox("Use Q to jungle clear"));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Condemn (E) settings :");
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.UseE", new CheckBox("Use E in jungle clear"));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Mana settings :");
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.MinMana",
                new Slider("Min mana percentage ({0}%) for lane clear and jungle clear", 80, 1));

            MenuManager.BuildAntiGapcloserMenu();
            MenuManager.BuildInterrupterMenu();
            TargetedSpells.BuildMenu();

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Vayne addon");

            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Vayne.MiscMenu.NoAAWhileStealth",
                new KeyBind("Dont AutoAttack while stealth", false, KeyBind.BindTypes.PressToggle, 'T')).OnValueChange
                +=
                (sender, args) =>
                {
                    DontAa.Value = args.NewValue;
                };
            MiscMenu.Add("Plugins.Vayne.MiscMenu.NoAADelay", new Slider("Delay", 1000, 0, 1000));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Additional Condemn (E) settings :");
            MiscMenu.Add("Plugins.Vayne.MiscMenu.EAntiRengar", new CheckBox("Enable Anti-Rengar"));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.Eks", new CheckBox("Use E to killsteal"));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.PushDistance", new Slider("Push distance", 420, 400, 470));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.EHitchance", new Slider("Condemn hitchance : {0}%", 65));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.EMode", new ComboBox("E Mode", 1, "Always", "Only in Combo"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Additional Tumble (Q) settings :");
            MiscMenu.Add("Plugins.Vayne.MiscMenu.QMode", new ComboBox("Q Mode", 0, "CursorPos", "Auto"));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.QSafetyChecks", new CheckBox("Enable safety checks")).OnValueChange +=
                (sender, args) =>
                {
                    SafetyChecks.Value = args.NewValue;
                };

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawing settings for Vayne addon");
            DrawingsMenu.Add("Plugins.Vayne.DrawingsMenu.DrawInfo", new CheckBox("Draw info"));

            DontAa = MenuManager.PermaShow.AddItem("Vanye.SafetyChecks",
                new BoolItem("Don't auto attack while in stealth", Settings.Misc.NoAaWhileStealth));
            SafetyChecks = MenuManager.PermaShow.AddItem("Vanye.SafetyChecks",
                new BoolItem("Enable safety checks", Settings.Misc.QSafetyChecks));
        }

        protected override void PermaActive()
        {
            Orbwalker.DisableMovement = Game.Time * 1000 - LastQ < 400;

            Modes.PermaActive.Execute();
        }

        protected override void ComboMode()
        {
            if (FlashCondemnKeybind.CurrentValue)
            {
                PerformFlashCondemn();
            }

            Modes.Combo.Execute();
        }

        protected override void HarassMode()
        {
            Modes.Harass.Execute();
        }

        protected override void LaneClear()
        {
            Modes.LaneClear.Execute();
        }

        protected override void JungleClear()
        {
            Modes.JungleClear.Execute();
        }

        protected override void LastHit()
        {
            Modes.LastHit.Execute();
        }

        protected override void Flee()
        {
            Modes.Flee.Execute();
        }

        protected static class Settings
        {
            internal static class Combo
            {
                public static bool UseQ => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseQ"];

                public static bool UseQToPoke => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseQToPoke"];

                public static bool UseQOnlyToProcW => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseQOnlyToProcW"];

                public static bool BlockQsOutOfAaRange => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.BlockQsOutOfAARange"]; 

                public static bool UseE => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseE"];

                public static bool UseR => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseR"];
            }

            internal static class Harass
            {
                public static bool UseQ => MenuManager.MenuValues["Plugins.Vayne.HarassMenu.UseQ"];

                public static int MinManaToUseQ => MenuManager.MenuValues["Plugins.Vayne.HarassMenu.MinManaToUseQ", true];
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.EnableLCIfNoEn"];

                public static int ScanRange => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.ScanRange", true];

                public static int AllowedEnemies => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.AllowedEnemies", true];

                public static bool UseQToLaneClear => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.UseQToLaneClear"];

                public static bool UseQToJungleClear => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.UseQToJungleClear"];

                public static bool UseE => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.UseE"]; 

                public static int MinMana => MenuManager.MenuValues["Plugins.Vayne.LaneClearMenu.MinMana", true];
            }

            internal static class Misc
            {
                public static bool NoAaWhileStealth => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.NoAAWhileStealth"];

                public static int NoAaDelay => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.NoAADelay", true];

                public static bool EAntiRengar => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.EAntiRengar"];

                public static bool EKs => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.Eks"];

                public static int PushDistance => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.PushDistance", true];

                public static int EHitchance => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.EHitchance", true];

                /// <summary>
                /// 0 - Always
                /// 1 - Only in combo
                /// </summary>
                public static int EMode => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.EMode", true];

                /// <summary>
                /// 0 - CursorPos
                /// 1 - Auto
                /// </summary>
                public static int QMode => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.QMode", true];

                public static bool QSafetyChecks => MenuManager.MenuValues["Plugins.Vayne.MiscMenu.QSafetyChecks"];
            }

            internal static class Drawings
            {
                public static bool DrawInfo => MenuManager.MenuValues["Plugins.Vayne.DrawingsMenu.DrawInfo"];
            }
        }

        protected static class Damage
        {
            public static CustomCache<int, bool> IsKillableFrom3SilverStacksCache { get; set; } =
                StaticCacheProvider.Cache.Resolve<CustomCache<int, bool>>();

            public static CustomCache<int, float> WDamageCache { get; set; } =
                StaticCacheProvider.Cache.Resolve<CustomCache<int, float>>();

            public static CustomCache<int, bool> IsKillableFromSilverEAndAutoCache { get; set; } =
                StaticCacheProvider.Cache.Resolve<CustomCache<int, bool>>();

            public static float[] QBonusDamage { get; } = { 0, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
            public static int[] WMinimumDamage { get; } = {0, 40, 60, 80, 100, 120};
            public static float[] WPercentageDamage { get; } = {0, 0.06f, 0.075f, 0.09f, 0.105f, 0.12f};
            public static int[] EDamage { get; } = {0, 45, 80, 115, 150, 185};

            public static bool IsKillableFrom3SilverStacks(Obj_AI_Base unit)
            {
                if (MenuManager.IsCacheEnabled && IsKillableFrom3SilverStacksCache.Exist(unit.NetworkId))
                {
                    return IsKillableFrom3SilverStacksCache.Get(unit.NetworkId);
                }

                var output = unit.TotalHealthWithShields() <= GetWDamage(unit);

                if (MenuManager.IsCacheEnabled)
                    IsKillableFrom3SilverStacksCache.Add(unit.NetworkId, output);

                return output;
            }

            public static bool IsKillableFromSilverEAndAuto(Obj_AI_Base unit)
            {
                if (!IsECastableOnEnemy(unit))
                    return false;

                if (MenuManager.IsCacheEnabled && IsKillableFromSilverEAndAutoCache.Exist(unit.NetworkId))
                {
                    return IsKillableFromSilverEAndAutoCache.Get(unit.NetworkId);
                }

                bool output;

                var edmg = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical,
                    EDamage[E.Level] + Player.Instance.FlatPhysicalDamageMod / 2);

                if (WillEStun(unit))
                    edmg *= 2;

                var aaDamage = Player.Instance.GetAutoAttackDamageCached(unit);

                var damage = GetWDamage(unit) + edmg + aaDamage;

                if (unit.GetType() != typeof(AIHeroClient))
                {
                    output = unit.TotalHealthWithShields() <= damage;

                    if (MenuManager.IsCacheEnabled)
                        IsKillableFromSilverEAndAutoCache.Add(unit.NetworkId, output);

                    return output;
                }

                var enemy = (AIHeroClient)unit;

                if (enemy.HasSpellShield() || enemy.HasUndyingBuffA())
                    return false;

                if (enemy.ChampionName != "Blitzcrank")
                {
                    output = enemy.TotalHealthWithShields() < damage;

                    if (MenuManager.IsCacheEnabled)
                        IsKillableFromSilverEAndAutoCache.Add(unit.NetworkId, output);

                    return output;
                }

                if (!enemy.HasBuff("BlitzcrankManaBarrierCD") && !enemy.HasBuff("ManaBarrier"))
                {
                    output = enemy.TotalHealthWithShields() + enemy.Mana / 2 < damage;

                    if (MenuManager.IsCacheEnabled)
                        IsKillableFromSilverEAndAutoCache.Add(unit.NetworkId, output);

                    return output;
                }

                output = enemy.TotalHealthWithShields() < damage;

                if (MenuManager.IsCacheEnabled)
                    IsKillableFromSilverEAndAutoCache.Add(unit.NetworkId, output);

                return output;
            }

            public static float GetWDamage(Obj_AI_Base unit)
            {
                if (MenuManager.IsCacheEnabled && WDamageCache.Exist(unit.NetworkId))
                {
                    return WDamageCache.Get(unit.NetworkId);
                }

                var damage = Math.Max(WMinimumDamage[W.Level], unit.MaxHealth*WPercentageDamage[W.Level]);

                if (damage > 200 && !(unit is AIHeroClient))
                    damage = 200;

                damage = Player.Instance.CalculateDamageOnUnit(unit, DamageType.True, damage);

                if (MenuManager.IsCacheEnabled)
                    WDamageCache.Add(unit.NetworkId, damage);

                return damage;
            }
        }

        protected static class TargetedSpells
        {
            public static Menu EEvadeMenu { get; private set; } 

            public static List<TargetedSpell> SpellsList = new List<TargetedSpell>
            {
                new TargetedSpell(Champion.Brand, "Pyroclasm", SpellSlot.R),
                new TargetedSpell(Champion.Caitlyn, "Ace in the Hole", SpellSlot.R),
                new TargetedSpell(Champion.Chogath, "Feast", SpellSlot.R),
                new TargetedSpell(Champion.Darius, "Noxian Guillotine", SpellSlot.R),
                new TargetedSpell(Champion.FiddleSticks, "Terrify", SpellSlot.Q, false, 50, 20, 700),
                new TargetedSpell(Champion.Fiora, "Grand Challenge", SpellSlot.R),
                new TargetedSpell(Champion.Garen, "Demacian Justice", SpellSlot.R),
                new TargetedSpell(Champion.JarvanIV, "Cataclysm", SpellSlot.R),
                new TargetedSpell(Champion.Jayce, "To The Skies!", SpellSlot.Q, true, 100, 30),
                new TargetedSpell(Champion.Karma, "Focused Resolve", SpellSlot.W, true, 100, 30),
                new TargetedSpell(Champion.Kayle, "Reckoning", SpellSlot.Q, true, 50, 30),
                new TargetedSpell(Champion.Khazix, "Taste Their Fear", SpellSlot.Q, false),
                new TargetedSpell(Champion.Kindred, "Mounting Dread", SpellSlot.E, true, 100, 50),
                new TargetedSpell(Champion.Kled, "Beartrap on a Rope", SpellSlot.Q, true, 100, 50),
                new TargetedSpell(Champion.LeeSin, "Dragon's Rage", SpellSlot.R),
                new TargetedSpell(Champion.Mordekaiser, "Children of the Grave", SpellSlot.R),
                new TargetedSpell(Champion.Morgana, "Soul Shackles", SpellSlot.R),
                new TargetedSpell(Champion.Nasus, "Wither", SpellSlot.W, false, 100, 20),
                new TargetedSpell(Champion.Quinn, "Vault", SpellSlot.E, true, 50, 20),
                new TargetedSpell(Champion.Renekton, "Ruthless Predator", SpellSlot.W),
                new TargetedSpell(Champion.Rammus, "Puncturing Taunt", SpellSlot.E),
                new TargetedSpell(Champion.Ryze, "Rune Prison", SpellSlot.W, true, 100, 20, 500),
                new TargetedSpell(Champion.Shaco, "Two-Shiv Poison", SpellSlot.E, true, 70, 20, 400),
                new TargetedSpell(Champion.Singed, "Fling", SpellSlot.E),
                new TargetedSpell(Champion.TahmKench, "Devour", SpellSlot.W),
                new TargetedSpell(Champion.Teemo, "Blinding Dart", SpellSlot.Q, true, 70, 30, 400),
                new TargetedSpell(Champion.Vayne, "Condemn", SpellSlot.E, false),
                new TargetedSpell(Champion.Warwick, "Infinite Duress", SpellSlot.R)
            };


            public static void Initialize()
            {
                Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;

                if (EntityManager.Heroes.Enemies.Any(x => x.Hero == Champion.Kled))
                {
                    Game.OnTick += Game_OnTick;
                }
            }

            private static void Game_OnTick(EventArgs args)
            {
                if (!E.IsReady())
                    return;

                var buff =
                    Player.Instance.Buffs.Find(
                        x => x.IsActive && string.Equals(x.Name, "kledqmark", StringComparison.InvariantCultureIgnoreCase));

                if (buff?.Caster == null)
                    return;

                var target =
                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero)
                        .ToList()
                        .Find(x => x.NetworkId == buff.Caster.NetworkId);

                if (target == null || !target.IsValidTargetCached(E.Range) || target.DistanceCached(Player.Instance) < 150)
                    return;

                var data = GetMenuData(target.Hero, SpellSlot.Q);

                if (data == null || !data.Enabled)
                    return;

                if (data.OnlyInCombo && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                    return;

                if ((target.DistanceCached(Player.Instance) <= data.CasterMinimumDistanceToPlayer) && (Player.Instance.HealthPercent <= data.MyHealthMinimumPercent) &&
                    (target.HealthPercent <= data.CasterHealthMinimumPercent))
                {
                    E.Cast(target);

                    Misc.PrintInfoMessage("Casting <blue>condemn</blue> against <c>Kled's</c> <in>Beartrap on a Rope</in>");
                }
            }

            private static void OnProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
            {
                if (!E.IsReady() || sender.IsMe || Player.Instance.IsDead || sender.GetType() != typeof (AIHeroClient) ||
                    !sender.IsEnemy || !sender.IsValidTargetCached(E.Range))
                    return;

                var hero = sender as AIHeroClient;

                if (hero == null || hero.Hero == Champion.Kled || hero.Hero == Champion.Rengar)
                    return;

                var data = GetMenuData(hero.Hero, args.Slot);

                if (data == null || !data.Enabled)
                    return;

                if (data.OnlyInCombo && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                    return;

                if ((hero.DistanceCached(Player.Instance) <= data.CasterMinimumDistanceToPlayer) && (Player.Instance.HealthPercent <= data.MyHealthMinimumPercent) &&
                    (hero.HealthPercent <= data.CasterHealthMinimumPercent))
                {
                    E.Cast(hero);

                    Misc.PrintInfoMessage($"Casting <b><blue>condemn</blue></b> against <c>{hero.Hero}</c> <in>{data.SpellName}</in>");
                }
            }

            public static void BuildMenu()
            {
                EEvadeMenu = MenuManager.Menu.AddSubMenu("Condemn evade");

                foreach (var spellData in EntityManager.Heroes.Enemies.Where(enemy => SpellsList.Any(x => x.Champion == enemy.Hero)).Select(enemy => SpellsList.Find(x => x.Champion == enemy.Hero)).Where(spellData => spellData != null))
                {
                    EEvadeMenu.AddGroupLabel(spellData.Champion.ToString());

                    EEvadeMenu.AddLabel($"Spell : [{spellData.SpellSlot}] {spellData.SpellName}");
                    EEvadeMenu.Add($"Plugins.Vayne.EEvadeMenu.{spellData.Champion}.{spellData.SpellSlot}.MyMinHealth", new Slider("My minimum health percentage to cast condemn : {0}%", spellData.MyHealthMinimumPercent));
                    EEvadeMenu.Add($"Plugins.Vayne.EEvadeMenu.{spellData.Champion}.{spellData.SpellSlot}.CasterMinHealth", new Slider("Cast if "+spellData.Champion+" health percentage is higher than : {0}%", spellData.CasterHealthMinimumPercent));
                    EEvadeMenu.Add($"Plugins.Vayne.EEvadeMenu.{spellData.Champion}.{spellData.SpellSlot}.CasterMinDistance", new Slider(spellData.Champion + " minimum distance to player to cast condemn : {0}", spellData.CasterMinimumDistanceToPlayer, 0, 750));
                    EEvadeMenu.Add($"Plugins.Vayne.EEvadeMenu.{spellData.Champion}.{spellData.SpellSlot}.OnlyInCombo", new CheckBox("Only in combo", false));
                    EEvadeMenu.Add($"Plugins.Vayne.EEvadeMenu.{spellData.Champion}.{spellData.SpellSlot}.Enabled", new CheckBox("Enabled", spellData.EnabledByDefault));
                }
            }

            public static TargetedSpell GetMenuData(Champion champion, SpellSlot slot)
            {
                if (!SpellsList.Any(x => x.Champion == champion && x.SpellSlot == slot))
                    return null;

                if (EEvadeMenu == null)
                    return SpellsList.Find(x => x.Champion == champion && x.SpellSlot == slot);

                var myMinHealth = EEvadeMenu[$"Plugins.Vayne.EEvadeMenu.{champion}.{slot}.MyMinHealth"];
                var casterMinHealth = EEvadeMenu[$"Plugins.Vayne.EEvadeMenu.{champion}.{slot}.CasterMinHealth"];
                var casterMinDistance = EEvadeMenu[$"Plugins.Vayne.EEvadeMenu.{champion}.{slot}.CasterMinDistance"];
                var onlyInCombo = EEvadeMenu[$"Plugins.Vayne.EEvadeMenu.{champion}.{slot}.OnlyInCombo"];
                var enabled = EEvadeMenu[$"Plugins.Vayne.EEvadeMenu.{champion}.{slot}.Enabled"];
                var spellData = SpellsList.Find(x => x.Champion == champion && x.SpellSlot == slot);

                var output = new TargetedSpell(champion, spellData.SpellName, slot, spellData.EnabledByDefault,
                    myMinHealth?.Cast<Slider>().CurrentValue ?? spellData.MyHealthMinimumPercent,
                    casterMinHealth?.Cast<Slider>().CurrentValue ?? spellData.CasterHealthMinimumPercent,
                    casterMinDistance?.Cast<Slider>().CurrentValue ?? spellData.CasterMinimumDistanceToPlayer,
                    onlyInCombo?.Cast<CheckBox>().CurrentValue ?? spellData.EnabledByDefault,
                    enabled?.Cast<CheckBox>().CurrentValue ?? spellData.EnabledByDefault);

                return output;
            }


            public class TargetedSpell
            {
                public Champion Champion { get; }
                public string SpellName { get; }
                public SpellSlot SpellSlot { get; }
                public bool EnabledByDefault { get; }
                public bool Enabled { get; }
                public bool OnlyInCombo { get; }
                public int MyHealthMinimumPercent { get; } = 100;
                public int CasterHealthMinimumPercent { get; } = 100;
                public int CasterMinimumDistanceToPlayer { get; } = 750;

                public TargetedSpell(Champion champion, string spellName, SpellSlot spellSlot, bool enabledByDefault = true)
                {
                    Champion = champion;
                    SpellName = spellName;
                    SpellSlot = spellSlot;
                    EnabledByDefault = enabledByDefault;
                }

                public TargetedSpell(Champion champion, string spellName, SpellSlot spellSlot, bool enabledByDefault,
                    int myHealthMinimumPercent, int casterHealthMinimumPercent, int casterMinimumDistanceToPlayer = 750)
                    : this(champion, spellName, spellSlot, enabledByDefault)
                {
                    Champion = champion;
                    SpellName = spellName;
                    SpellSlot = spellSlot;
                    EnabledByDefault = enabledByDefault;
                    MyHealthMinimumPercent = myHealthMinimumPercent;
                    CasterHealthMinimumPercent = casterHealthMinimumPercent;
                    CasterMinimumDistanceToPlayer = casterMinimumDistanceToPlayer;
                }

                public TargetedSpell(Champion champion, string spellName, SpellSlot spellSlot, bool enabledByDefault,
                    int myHealthMinimumPercent, int casterHealthMinimumPercent, int casterMinimumDistanceToPlayer, bool onlyInCombo, bool enabled)
                    : this(champion, spellName, spellSlot, enabledByDefault, myHealthMinimumPercent, casterHealthMinimumPercent, casterMinimumDistanceToPlayer)
                {
                    Champion = champion;
                    SpellName = spellName;
                    SpellSlot = spellSlot;
                    EnabledByDefault = enabledByDefault;
                    OnlyInCombo = onlyInCombo;
                    Enabled = enabled;
                    MyHealthMinimumPercent = myHealthMinimumPercent;
                    CasterHealthMinimumPercent = casterHealthMinimumPercent;
                    CasterMinimumDistanceToPlayer = casterMinimumDistanceToPlayer;
                }
            }
        }
    }
}
