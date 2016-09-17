#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Draven.cs" company="EloBuddy">
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
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Marksman_Master.Utils;
using SharpDX;
using Color = System.Drawing.Color;
using ColorPicker = Marksman_Master.Utils.ColorPicker;
using Font = System.Drawing.Font;

namespace Marksman_Master.Plugins.Draven
{
    internal class Draven : ChampionPlugin
    {
        protected static Spell.Active Q { get; }
        protected static Spell.Active W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Skillshot R { get; }

        internal static Menu ComboMenu { get; set; }
        internal static Menu HarassMenu { get; set; }
        internal static Menu LaneClearMenu { get; set; }
        internal static Menu DrawingsMenu { get; set; }
        internal static Menu MiscMenu { get; set; }
        internal static Menu AxeSettingsMenu { get; set; }

        private static readonly List<AxeObjectData> AxeObjects = new List<AxeObjectData>();
        private static readonly Text Text;
        private static readonly ColorPicker[] ColorPicker;

        protected static float[] WAdditionalMovementSpeed { get; } = {0, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f};

        protected static bool HasSpinningAxeBuff
            => Player.Instance.Buffs.Any(x => x.Name.ToLowerInvariant() == "dravenspinningattack");

        protected static BuffInstance GetSpinningAxeBuff
            => Player.Instance.Buffs.FirstOrDefault(x => x.Name.ToLowerInvariant() == "dravenspinningattack");

        protected static bool HasMoveSpeedFuryBuff
            => Player.Instance.Buffs.Any(x => x.Name.ToLowerInvariant() == "dravenfury");

        protected static BuffInstance GetMoveSpeedFuryBuff
            => Player.Instance.Buffs.FirstOrDefault(x => x.Name.ToLowerInvariant() == "dravenfury");

        protected static bool HasAttackSpeedFuryBuff
            => Player.Instance.Buffs.Any(x => x.Name.ToLowerInvariant() == "dravenfurybuff");

        protected static BuffInstance GetAttackSpeedFuryBuff
            => Player.Instance.Buffs.FirstOrDefault(x => x.Name.ToLowerInvariant() == "dravenfurybuff");

        private static bool _changingRangeScan;
        private static bool _changingkeybindRange;
        private static bool _catching;

        protected static MissileClient DravenRMissile { get; private set; }

        static Draven()
        {
            Q = new Spell.Active(SpellSlot.Q);
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Skillshot(SpellSlot.E, 950, SkillShotType.Linear, 250, 1300, 100)
            {
                AllowedCollisionCount = int.MaxValue
            };
            R = new Spell.Skillshot(SpellSlot.R, 30000, SkillShotType.Linear, 300, 1900, 160)
            {
                AllowedCollisionCount = int.MaxValue
            };

            ColorPicker = new ColorPicker[2];
            ColorPicker[0] = new ColorPicker("DravenE", new ColorBGRA(114, 171, 160, 255));
            ColorPicker[1] = new ColorPicker("DravenCatchRange", new ColorBGRA(231, 237, 160, 255));

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));

            Game.OnTick += Game_OnTick;
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
            Orbwalker.OnPreAttack += Orbwalker_OnPreAttack;
            Orbwalker.OnPostAttack += Orbwalker_OnPostAttack;
        }

        private static void Orbwalker_OnPostAttack(AttackableUnit target, EventArgs args)
        {
            if (target.GetType() != typeof(AIHeroClient) || !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                return;

            if (Q.IsReady() && GetAxesCount() != 0 && GetAxesCount() < Settings.Combo.MaxAxesAmount)
                Q.Cast();
        }

        private static void Orbwalker_OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear))
            {
                var jungleMinions =
                    EntityManager.MinionsAndMonsters.GetJungleMonsters(Player.Instance.Position,
                        Player.Instance.GetAutoAttackRange()).ToList();

                var laneMinions =
                    EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                        Player.Instance.Position,
                        Player.Instance.GetAutoAttackRange()).ToList();

                if (jungleMinions.Any())
                {
                    if (Settings.LaneClear.UseQInJungleClear && Q.IsReady() && GetAxesCount() == 0 &&
                        Player.Instance.ManaPercent >= Settings.LaneClear.MinManaQ)
                    {
                        Q.Cast();
                    }

                    if (Settings.LaneClear.UseWInJungleClear && W.IsReady() && jungleMinions.Count > 1 &&
                        !HasAttackSpeedFuryBuff &&
                        Player.Instance.ManaPercent >= Settings.LaneClear.MinManaW)
                    {
                        W.Cast();
                    }
                    return;
                }
                if (laneMinions.Any() && Modes.LaneClear.CanILaneClear())
                {
                    if (Settings.LaneClear.UseQInLaneClear && Q.IsReady() && GetAxesCount() == 0 &&
                        Player.Instance.ManaPercent >= Settings.LaneClear.MinManaQ)
                    {
                        Q.Cast();
                    }

                    if (Settings.LaneClear.UseWInLaneClear && W.IsReady() && laneMinions.Count > 3 &&
                        !HasAttackSpeedFuryBuff &&
                        Player.Instance.ManaPercent >= Settings.LaneClear.MinManaW)
                    {
                        W.Cast();
                    }
                    return;
                }
            }

            if (target.GetType() != typeof(AIHeroClient) || !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                return;

            if (Q.IsReady() && GetAxesCount() == 0)
                Q.Cast();

            if (!W.IsReady() || !Settings.Combo.UseW || HasAttackSpeedFuryBuff || !(Player.Instance.Mana - 40 > 145))
                return;

            var t = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange(), DamageType.Physical);
            if (t != null)
            {
                W.Cast();
            }
        }
        
        private static void Game_OnTick(EventArgs args)
        {
            if(!AxeObjects.Any() || !Settings.Axe.CatchAxes || !_catching || GetAxesCount() == 0)
            { 
                Orbwalker.OverrideOrbwalkPosition += () => Game.CursorPos;
                _catching = false;
            }

            foreach (var axeObjectData in AxeObjects.Where(x => Game.CursorPos.Distance(x.EndPosition) < Settings.Axe.AxeCatchRange && CanPlayerCatchAxe(x)).OrderBy(x => x.EndPosition.Distance(Player.Instance)))
            {
                switch (Settings.Axe.CatchAxesMode)
                {
                    case 1:
                        var isOutside = !new Geometry.Polygon.Circle(Player.Instance.ServerPosition, Player.Instance.BoundingRadius - 15)
                            .Points.Any(x => new Geometry.Polygon.Circle(axeObjectData.EndPosition, 80).IsInside(x));

                        var isInside = new Geometry.Polygon.Circle(Player.Instance.ServerPosition, Player.Instance.BoundingRadius - 30)
                            .Points.Any(x => new Geometry.Polygon.Circle(axeObjectData.EndPosition, 40).IsInside(x));

                        if (isOutside)
                        {
                            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                            {
                                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 350,
                                    DamageType.Physical);

                                if (target != null &&
                                    target.TotalHealthWithShields() <
                                    Player.Instance.GetAutoAttackDamage(target, true) * 2)
                                {
                                    var pos = Prediction.Position.PredictUnitPosition(target, (int)(GetEta(axeObjectData, Player.Instance.MoveSpeed) * 1000));
                                    if (!axeObjectData.EndPosition.IsInRange(pos, Player.Instance.GetAutoAttackRange()))
                                    {
                                        continue;
                                    }
                                }
                            }

                            if (axeObjectData.EndTick - Game.Time < GetEta(axeObjectData, Player.Instance.MoveSpeed) && !HasMoveSpeedFuryBuff &&
                                GetEta(axeObjectData, Player.Instance.MoveSpeed * WAdditionalMovementSpeed[W.Level]) > axeObjectData.EndTick - Game.Time &&
                                W.IsReady() && Settings.Axe.UseWToCatch)
                            {
                                W.Cast();
                            }

                            Orbwalker.OverrideOrbwalkPosition +=
                                () =>
                                    axeObjectData.EndPosition.Extend(Player.Instance.Position,
                                        40 - Player.Instance.Distance(axeObjectData.EndPosition)).To3D();
                            _catching = true;
                        }
                        else if (isInside &&
                                 !CanPlayerLeaveAxeRangeInDesiredTime(axeObjectData.EndPosition,
                                     (axeObjectData.EndTick / 1000 - Game.Time) - 0.15f))
                        {
                            Orbwalker.OverrideOrbwalkPosition += () => Game.CursorPos;
                            _catching = false;
                        }
                        break;
                    case 0:
                        if (Player.Instance.Distance(axeObjectData.EndPosition) > 250)
                            return;

                        var isOutside2 = !new Geometry.Polygon.Circle(Player.Instance.ServerPosition, Player.Instance.BoundingRadius - 15)
                            .Points.Any(x => new Geometry.Polygon.Circle(axeObjectData.EndPosition, 80).IsInside(x));

                        var isInside2 = new Geometry.Polygon.Circle(Player.Instance.ServerPosition, Player.Instance.BoundingRadius - 30)
                            .Points.Any(x => new Geometry.Polygon.Circle(axeObjectData.EndPosition, 80).IsInside(x));

                        if (isOutside2)
                        {
                            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                            {
                                var target = TargetSelector.GetTarget(Player.Instance.GetAutoAttackRange() + 350,
                                    DamageType.Physical);
                                
                                if (target != null &&
                                    target.TotalHealthWithShields() <
                                    Player.Instance.GetAutoAttackDamage(target, true) * 2)
                                {
                                    var pos = Prediction.Position.PredictUnitPosition(target, (int)(GetEta(axeObjectData, Player.Instance.MoveSpeed)*1000));
                                    if (!axeObjectData.EndPosition.IsInRange(pos, Player.Instance.GetAutoAttackRange()))
                                    {
                                        continue;
                                    }
                                }
                            }

                            if (axeObjectData.EndTick - Game.Time < GetEta(axeObjectData, Player.Instance.MoveSpeed) && !HasMoveSpeedFuryBuff &&
                                GetEta(axeObjectData, Player.Instance.MoveSpeed * WAdditionalMovementSpeed[W.Level]) > axeObjectData.EndTick - Game.Time &&
                                W.IsReady() && Settings.Axe.UseWToCatch)
                            {
                                W.Cast();
                            }

                            Orbwalker.OverrideOrbwalkPosition +=
                                () =>
                                    axeObjectData.EndPosition.Extend(Player.Instance.Position,
                                        40 - Player.Instance.Distance(axeObjectData.EndPosition)).To3D();
                            _catching = true;
                        }
                        else if (isInside2 &&
                                 !CanPlayerLeaveAxeRangeInDesiredTime(axeObjectData.EndPosition,
                                     (axeObjectData.EndTick / 1000 - Game.Time) - 0.15f))
                        {
                            Orbwalker.OverrideOrbwalkPosition += () => Game.CursorPos;
                            _catching = false;
                        }
                        break;
                    default:
                        _catching = false;
                        return;
                }
            }

            AxeObjects.RemoveAll(x => x.EndTick - Game.Time * 1000 <= 0);
        }

        private static bool CanPlayerCatchAxe(AxeObjectData axe)
        {
            if (!Settings.Axe.CatchAxes || (Settings.Axe.CatchAxesWhen == 0 && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear)) || (Settings.Axe.CatchAxesWhen == 1 && !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)))
            {
                return false;
            }

            if (!Settings.Axe.CatchAxesUnderTower && axe.EndPosition.IsVectorUnderEnemyTower())
                return false;

            return Settings.Axe.CatchAxesNearEnemies || axe.EndPosition.CountEnemiesInRange(550) <= 2;
        }

        private static float GetEta(AxeObjectData axe, float movespeed)
        {
            return Player.Instance.Distance(axe.EndPosition) / movespeed;
        }

        private static bool CanPlayerLeaveAxeRangeInDesiredTime(Vector3 axeCenterPosition, float time)
        {
            var axePolygon = new Geometry.Polygon.Circle(axeCenterPosition, 90);
            var playerPosition = Player.Instance.ServerPosition;
            var playerLastWaypoint = Player.Instance.Path.LastOrDefault();
            var cloestPoint = playerLastWaypoint.To2D().Closest(axePolygon.Points);
            var distanceFromPoint = cloestPoint.Distance(playerPosition);
            var distanceInTime = Player.Instance.MoveSpeed*time;

            return distanceInTime > distanceFromPoint;
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if (Player.Instance.IsDead)
                return;

            if (sender.Name.Contains("Q_reticle_self"))
            {
                AxeObjects.Add(new AxeObjectData
                {
                    EndPosition = sender.Position,
                    EndTick = Game.Time * 1000 + 1227.1f,
                    NetworkId = sender.NetworkId,
                    Owner = Player.Instance,
                    StartTick = Game.Time * 1000
                });
            }


            var missile = sender as MissileClient;
            if (missile == null || !missile.IsValidMissile())
                return;

            if (missile.SData.Name.ToLowerInvariant() == "dravenr" && missile.SpellCaster.IsMe)
            {
                DravenRMissile = missile;
            }
        }

        private static void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            if (Player.Instance.IsDead)
                return;

            if (sender.Name.Contains("Q_reticle_self"))
            {
                AxeObjects.Remove(AxeObjects.Find(data => data.NetworkId == sender.NetworkId));
            }


            var missile = sender as MissileClient;
            if (missile == null)
                return;

            if (missile.SData.Name.ToLowerInvariant() == "dravenr" && missile.SpellCaster.IsMe)
            {
                DravenRMissile = null;
            }
        }

        protected static int GetAxesCount()
        {
            if (!HasSpinningAxeBuff && AxeObjects == null)
                return 0;

            if (!HasSpinningAxeBuff && AxeObjects?.Count > 0)
                return AxeObjects.Count;

            if (HasSpinningAxeBuff && GetSpinningAxeBuff.Count == 0 && AxeObjects?.Count > 0)
                return AxeObjects.Count;

            if (HasSpinningAxeBuff && GetSpinningAxeBuff.Count > 0 && AxeObjects?.Count == 0)
                return GetSpinningAxeBuff.Count;

            if (HasSpinningAxeBuff && GetSpinningAxeBuff.Count > 0 && AxeObjects?.Count > 0)
                return GetSpinningAxeBuff.Count + AxeObjects.Count;

            return 0;
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(SharpDX.Color.White,
                    LaneClearMenu["Plugins.Draven.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (_changingkeybindRange)
                Circle.Draw(SharpDX.Color.White, Settings.Combo.RRangeKeybind, Player.Instance);

            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[0].Color, E.Range, Player.Instance);

            foreach (var axeObjectData in AxeObjects)
            {
                if (Settings.Drawings.DrawAxes)
                {
                    Circle.Draw(
                        new Geometry.Polygon.Circle(Player.Instance.ServerPosition, Player.Instance.BoundingRadius).Points.Any(x => new Geometry.Polygon.Circle(axeObjectData.EndPosition, 80).IsInside(x))
                            ? new ColorBGRA(0, 255, 0, 255)
                            : new ColorBGRA(255, 0, 0, 255), 80, axeObjectData.EndPosition);
                }

                if (!Settings.Drawings.DrawAxesTimer)
                    continue;

                var timeLeft = axeObjectData.EndTick / 1000 - Game.Time;
                var degree = Misc.GetNumberInRangeFromProcent(timeLeft * 1000d / 1227.1 * 100d, 3, 110);
                
                Text.Color = new Misc.HsvColor(degree, 1, 1).ColorFromHsv();
                Text.X = (int) Drawing.WorldToScreen(axeObjectData.EndPosition).X;
                Text.Y = (int) Drawing.WorldToScreen(axeObjectData.EndPosition).Y + 50;
                Text.TextValue = ((axeObjectData.EndTick - Game.Time*1000)/1000).ToString("F1") + " s";
                Text.Draw();
            }

            if (Settings.Drawings.DrawAxesCatchRange)
                Circle.Draw(ColorPicker[1].Color, Settings.Axe.AxeCatchRange, Game.CursorPos);
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
            if (!Settings.Misc.EnableInterrupter || !E.IsReady() || !sender.IsValidTarget(E.Range))
                return;

            if (args.Delay == 0)
                E.Cast(sender);
            else Core.DelayAction(() => E.Cast(sender), args.Delay);
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
            if (!Settings.Misc.EnableAntiGapcloser || !(args.End.Distance(Player.Instance) < 350) || !E.IsReady() ||
                !sender.IsValidTarget(E.Range))
                return;

            if(args.Delay == 0)
                E.Cast(sender);
            else Core.DelayAction(() => E.Cast(sender), args.Delay);
        }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Draven addon");

            ComboMenu.AddLabel("Spinning Axe (Q) settings :");
            ComboMenu.Add("Plugins.Draven.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Draven.ComboMenu.MaxAxesAmount", new Slider("Maximum axes amount", 2, 1, 3));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Blood Rush (W) settings :");
            ComboMenu.Add("Plugins.Draven.ComboMenu.UseW", new CheckBox("Use W"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Stand Aside (E) settings :");
            ComboMenu.Add("Plugins.Draven.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Whirling Death (R) settings :");
            ComboMenu.Add("Plugins.Draven.ComboMenu.UseR", new CheckBox("Use R"));
            ComboMenu.Add("Plugins.Draven.ComboMenu.RKeybind",
                new KeyBind("R keybind", false, KeyBind.BindTypes.HoldActive, 'T'));
            ComboMenu.AddLabel("Fires R on best target in range when keybind is active.");
            ComboMenu.AddSeparator(5);
            var keybindRange = ComboMenu.Add("Plugins.Draven.ComboMenu.RRangeKeybind",
                new Slider("Maximum range to enemy to cast R while keybind is active", 1100, 300, 2500));
            keybindRange.OnValueChange += (a, b) =>
            {
                _changingkeybindRange = true;
                Core.DelayAction(() =>
                {
                    if (!keybindRange.IsLeftMouseDown && !keybindRange.IsMouseInside)
                    {
                        _changingkeybindRange = false;
                    }
                }, 2000);
            };

            AxeSettingsMenu = MenuManager.Menu.AddSubMenu("Axe Settings");
            AxeSettingsMenu.AddGroupLabel("Axe settings for Draven addon");
            AxeSettingsMenu.AddLabel("Basic settings :");
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.CatchAxes", new CheckBox("Catch Axes"));
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.UseWToCatch", new CheckBox("Cast W if axe is uncatchable"));
            AxeSettingsMenu.AddSeparator(5);

            AxeSettingsMenu.AddLabel("Catching settings :");
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.CatchAxesWhen",
                new ComboBox("When should I catch them", 0, "Lane clear and combo", " Only in Combo"));
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.CatchAxesMode",
                new ComboBox("Catch mode", 0, "Default", "Brutal"));
            AxeSettingsMenu.AddSeparator(2);
            AxeSettingsMenu.AddLabel("Default mode only tries to catch axe if distance to from player to axe is less than 250.\nBrutal catches all axes within range of desired catch radius.");
            AxeSettingsMenu.AddSeparator(5);

            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.AxeCatchRange",
                new Slider("Axe Catch Range", 450, 200, 1000));
            AxeSettingsMenu.AddSeparator(5);

            AxeSettingsMenu.AddLabel("Additional settings :");
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.CatchAxesUnderTower",
                new CheckBox("Catch Axes that are under enemy tower", false));
            AxeSettingsMenu.Add("Plugins.Draven.AxeSettingsMenu.CatchAxesNearEnemies",
                new CheckBox("Catch Axes that are near enemies", false));

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Draven addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.EnableLCIfNoEn",
                new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.ScanRange",
                new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.AllowedEnemies",
                new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Spinning Axe (Q) settings :");
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q in Lane Clear"));
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle Clear"));
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.MinManaQ",
                new Slider("Min mana percentage ({0}%) to use Q", 50, 1));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Blood Rush (W) settings :");
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.UseWInLaneClear", new CheckBox("Use Q in Lane Clear"));
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.UseWInJungleClear", new CheckBox("Use Q in Jungle Clear"));
            LaneClearMenu.Add("Plugins.Draven.LaneClearMenu.MinManaW",
                new Slider("Min mana percentage ({0}%) to use W", 75, 1));

            MenuManager.BuildAntiGapcloserMenu();
            MenuManager.BuildInterrupterMenu();

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Draven addon");
            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Draven.MiscMenu.EnableInterrupter", new CheckBox("Enable Interrupter"));
            MiscMenu.Add("Plugins.Draven.MiscMenu.EnableAntiGapcloser", new CheckBox("Enable Anti-Gapcloser"));

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Draven addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawSpellRangesWhenReady", new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Spinning Axe (Q) drawing settings :");
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawAxes", new CheckBox("Draw Axes"));
            DrawingsMenu.AddSeparator(1);
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawAxesTimer", new CheckBox("Draw Axes timer"));
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawAxesCatchRange", new CheckBox("Draw Axe's catch range"));
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawAxesCatchRangeColor",
                new CheckBox("Change Color", false)).OnValueChange += (a, b) =>
                {
                    if (!b.NewValue)
                        return;

                    ColorPicker[1].Initialize(Color.Aquamarine);
                    a.CurrentValue = false;
                };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Stand Aside (E) drawing settings :");
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawE", new CheckBox("Draw E range"));
            DrawingsMenu.Add("Plugins.Draven.DrawingsMenu.DrawEColor",
                new CheckBox("Change Color", false)).OnValueChange += (a, b) =>
                {
                    if (!b.NewValue)
                        return;

                    ColorPicker[0].Initialize(Color.Aquamarine);
                    a.CurrentValue = false;
                };
        }
        
        protected override void PermaActive()
        {
            Modes.PermaActive.Execute();
        }

        protected override void ComboMode()
        {
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
                public static bool UseQ => MenuManager.MenuValues["Plugins.Draven.ComboMenu.UseQ"];

                public static int MaxAxesAmount => MenuManager.MenuValues["Plugins.Draven.ComboMenu.MaxAxesAmount", true];

                public static bool UseW => MenuManager.MenuValues["Plugins.Draven.ComboMenu.UseW"];

                public static bool UseE => MenuManager.MenuValues["Plugins.Draven.ComboMenu.UseE"];

                public static bool UseR => MenuManager.MenuValues["Plugins.Draven.ComboMenu.UseR"];

                public static bool RKeybind => MenuManager.MenuValues["Plugins.Draven.ComboMenu.RKeybind"];

                public static int RRangeKeybind => MenuManager.MenuValues["Plugins.Draven.ComboMenu.RRangeKeybind", true];
            }

            internal static class Axe
            {
                public static bool CatchAxes => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.CatchAxes"];

                public static bool UseWToCatch => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.UseWToCatch"];

                /// <summary>
                /// 0 - Lane clear and combo
                /// 1 - Only in Combo
                /// </summary>
                public static int CatchAxesWhen => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.CatchAxesWhen", true];

                /// <summary>
                /// 0 - Default
                /// 1 - Brutal
                /// </summary>
                public static int CatchAxesMode => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.CatchAxesMode", true];

                public static int AxeCatchRange => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.AxeCatchRange", true];

                public static bool CatchAxesUnderTower => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.CatchAxesUnderTower"];

                public static bool CatchAxesNearEnemies => MenuManager.MenuValues["Plugins.Draven.AxeSettingsMenu.CatchAxesNearEnemies"];
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.EnableLCIfNoEn"];

                public static int ScanRange => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.ScanRange", true];

                public static int AllowedEnemies => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.AllowedEnemies", true];

                public static bool UseQInLaneClear => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.UseQInLaneClear"];

                public static bool UseQInJungleClear => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.UseQInJungleClear"];

                public static int MinManaQ => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.MinManaQ", true];

                public static bool UseWInLaneClear => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.UseWInLaneClear"];

                public static bool UseWInJungleClear => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.UseWInJungleClear"];

                public static int MinManaW => MenuManager.MenuValues["Plugins.Draven.LaneClearMenu.MinManaW", true];
            }

            internal static class Misc
            {
                public static bool EnableInterrupter => MenuManager.MenuValues["Plugins.Draven.MiscMenu.EnableInterrupter"];

                public static bool EnableAntiGapcloser => MenuManager.MenuValues["Plugins.Draven.MiscMenu.EnableAntiGapcloser"];
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady => MenuManager.MenuValues["Plugins.Draven.DrawingsMenu.DrawSpellRangesWhenReady"];

                public static bool DrawAxes => MenuManager.MenuValues["Plugins.Draven.DrawingsMenu.DrawAxes"];

                public static bool DrawAxesTimer => MenuManager.MenuValues["Plugins.Draven.DrawingsMenu.DrawAxesTimer"];

                public static bool DrawAxesCatchRange => MenuManager.MenuValues["Plugins.Draven.DrawingsMenu.DrawAxesCatchRange"];

                public static bool DrawE => MenuManager.MenuValues["Plugins.Draven.DrawingsMenu.DrawE"];
            }
        }
    }
}