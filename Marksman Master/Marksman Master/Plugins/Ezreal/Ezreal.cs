#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Ezreal.cs" company="EloBuddy">
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
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using EloBuddy.SDK.Rendering;
using EloBuddy.SDK.Utils;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Ezreal
{
    internal class Ezreal : ChampionPlugin
    {
        protected static Spell.Skillshot Q { get; }
        protected static Spell.Skillshot W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Skillshot R { get; }

        protected static Menu ComboMenu { get; set; }
        protected static Menu HarassMenu { get; set; }
        protected static Menu LaneClearMenu { get; set; }
        protected static Menu DrawingsMenu { get; set; }
        protected static Menu MiscMenu { get; set; }

        private static ColorPicker[] ColorPicker { get; }

        private static bool _changingRangeScan;

        protected static bool IsPreAttack { get; private set; }
        protected static bool IsPostAttack { get; private set; }

        protected static bool HasPassiveBuff
            => Player.Instance.Buffs.Any(b => b.IsActive && b.Name.ToLowerInvariant() == "ezrealrisingspellforce");

        protected static BuffInstance GetPassiveBuff
            => Player.Instance.Buffs.Find(b => b.IsActive && b.Name.ToLowerInvariant() == "ezrealrisingspellforce");

        protected static int GetPassiveBuffAmount
            => HasPassiveBuff ? Player.Instance.Buffs.Find(
                        b => b.IsActive && b.Name.ToLowerInvariant() == "ezrealrisingspellforce").Count : 0;

        static Ezreal()
        {
            Q = new Spell.Skillshot(SpellSlot.Q, 1150, SkillShotType.Linear, 250, 2000, 60);
            W = new Spell.Skillshot(SpellSlot.W, 1000, SkillShotType.Linear, 250, 1550, 70)
            {
                AllowedCollisionCount = int.MaxValue
            };
            E = new Spell.Skillshot(SpellSlot.E, 475, SkillShotType.Linear);
            R = new Spell.Skillshot(SpellSlot.R, 30000, SkillShotType.Linear, 1000, 2000, 160)
            {
                AllowedCollisionCount = int.MaxValue
            };

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("EzrealQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("EzrealW", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("EzrealE", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("EzrealHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ChampionTracker.Initialize(ChampionTrackerFlags.LongCastTimeTracker);

            ColorPicker[3].OnColorChange +=
                (a, b) =>
                {
                    DamageIndicator.Color = b.Color;
                };

            TearStacker.Initializer(new Dictionary<SpellSlot, float> {{SpellSlot.Q, 5000}, {SpellSlot.W, 15000}},
                () => Player.Instance.CountEnemiesInRange(1000) == 0 && Player.Instance.CountEnemyMinionsInRange(1000) == 0 && !HasAnyOrbwalkerFlags());

            Orbwalker.OnPreAttack += (a, b) =>
            {
                if (a.IsMe)
                    return;

                IsPreAttack = true;
            };
            Orbwalker.OnPostAttack += (a, b) => { IsPreAttack = false; IsPostAttack = true; };

            ChampionTracker.OnLongSpellCast += ChampionTracker_OnLongSpellCast;
        }

        private static void ChampionTracker_OnLongSpellCast(object sender, OnLongSpellCastEventArgs e)
        {
            if (e.IsTeleport)
                return;

            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && Q.IsReady() && Settings.Combo.UseQ &&
                Settings.Combo.UseQOnImmobile && !Player.Instance.HasSheenBuff())
            {
                Q.CastMinimumHitchance(e.Sender, 65);
            }
            else if (Settings.Harass.IsAutoHarassEnabledFor(e.Sender) && Q.IsReady() && Settings.Harass.UseQ && Player.Instance.ManaPercent >= Settings.Harass.MinManaQ &&
                     !Player.Instance.HasSheenBuff())
            {
                Q.CastMinimumHitchance(e.Sender, 65);
            }
        }

        private static bool HasAnyOrbwalkerFlags()
        {
            return (Orbwalker.ActiveModesFlags & (Orbwalker.ActiveModes.Combo | Orbwalker.ActiveModes.Harass | Orbwalker.ActiveModes.LaneClear | Orbwalker.ActiveModes.LastHit | Orbwalker.ActiveModes.JungleClear | Orbwalker.ActiveModes.Flee)) != 0;
        }

        private static float HandleDamageIndicator(Obj_AI_Base unit)
        {
            if (!Settings.Drawings.DrawDamageIndicator)
            {
                return 0;
            }

            if (unit.GetType() != typeof(AIHeroClient))
                return 0;

            var damage = 0f;

            if (unit.IsValidTarget(Q.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.Q);

            if (unit.IsValidTarget(W.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.W);

            if (unit.IsValidTarget(R.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.R);

            if (Player.Instance.IsInAutoAttackRange(unit))
                damage += Player.Instance.GetAutoAttackDamage(unit);

            return damage;
        }

        protected static float GetComboDamage(Obj_AI_Base unit)
        {
            var damage = 0f;

            if (unit.IsValidTarget(Q.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.Q);

            if (unit.IsValidTarget(W.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.W);

            if (unit.IsValidTarget(E.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.E);

            if (unit.IsValidTarget(R.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.R);

            if (Player.Instance.IsInAutoAttackRange(unit))
                damage += Player.Instance.GetAutoAttackDamage(unit);

            return damage;
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(Color.White,
                    LaneClearMenu["Plugins.Ezreal.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (Settings.Drawings.DrawQ && (!Settings.Drawings.DrawSpellRangesWhenReady || Q.IsReady()))
                Circle.Draw(ColorPicker[0].Color, Q.Range, Player.Instance);
            if (Settings.Drawings.DrawW && (!Settings.Drawings.DrawSpellRangesWhenReady || W.IsReady()))
                Circle.Draw(ColorPicker[1].Color, W.Range, Player.Instance);
            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[2].Color, E.Range, Player.Instance);
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
        }
        
        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Ezreal addon");

            ComboMenu.AddLabel("Mystic Shot	(Q) settings :");
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.UseQOnImmobile", new CheckBox("Cast on immobile enemies"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Essence Flux (W) settings :");
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.UseW", new CheckBox("Use W"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Arcane Shift (E) settings :");
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Trueshot Barrage (R) settings :");
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.UseR", new CheckBox("Use R"));
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.RMinEnemiesHit", new Slider("Use R if will hit {0} or more enemies", 3, 1, 5));
            ComboMenu.Add("Plugins.Ezreal.ComboMenu.RKeybind", new KeyBind("R keybind", false, KeyBind.BindTypes.HoldActive, 'T'));

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Ezreal addon");

            HarassMenu.AddLabel("Mystic Shot (Q) settings :");
            HarassMenu.Add("Plugins.Ezreal.HarassMenu.UseQ",
                new KeyBind("Enable auto harass", false, KeyBind.BindTypes.PressToggle, 'A'));
            HarassMenu.Add("Plugins.Ezreal.HarassMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 30, 1));
            HarassMenu.AddSeparator(5);

            HarassMenu.AddLabel("Auto harass enabled for :");
            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                HarassMenu.Add("Plugins.Ezreal.HarassMenu.UseQ." + enemy.Hero, new CheckBox(enemy.ChampionName == "MonkeyKing" ? "Wukong" : enemy.ChampionName));
            }

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Ezreal addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby", false));
            var scanRange = LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("End of the Line (Q) settings :");
            LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q to lasthit minions"));
            LaneClearMenu.AddSeparator(5);
            LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle Clear"));
            LaneClearMenu.Add("Plugins.Ezreal.LaneClearMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 50, 1));

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Ezreal addon");
            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.EnableKillsteal", new CheckBox("Enable Killsteal"));
            MiscMenu.AddSeparator(2);
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.KeepPassiveStacks", new CheckBox("Keep passive stacks if possible"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Arcane Shift (E) settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.EAntiMelee", new CheckBox("Use E against melee champions"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Tear Stacker settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.EnableTearStacker", new CheckBox("Enable Tear stacker")).OnValueChange +=
                (a, b) =>
                {
                    TearStacker.Enabled = b.NewValue;
                };

            MiscMenu.Add("Plugins.Ezreal.MiscMenu.StackOnlyInFountain", new CheckBox("Stack only in fountain")).OnValueChange +=
                (a, b) =>
                {
                    TearStacker.OnlyInFountain = b.NewValue;
                };

            MiscMenu.Add("Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker", new Slider("Min mana percentage ({0}%) to use to stack tear",  50)).OnValueChange +=
                (a, b) =>
                {
                    TearStacker.MinimumManaPercent = b.NewValue;
                };

            MiscMenu.AddSeparator(5);
            
            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Ezreal addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady", new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Mystic Shot (Q) settings :");
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawQ", new CheckBox("Draw Q range"));
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawQColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Essence Flux (W) settings :");
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawW", new CheckBox("Draw W range", false));
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawWColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };

            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Arcane Shift (E) settings :");
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawE", new CheckBox("Draw E range", false));
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawEColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[2].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };

            DrawingsMenu.AddLabel("Damage indicator settings :");
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator", new CheckBox("Draw damage indicator")).OnValueChange += (a, b) =>
            {
                if (b.NewValue)
                    DamageIndicator.DamageDelegate = HandleDamageIndicator;
                else if (!b.NewValue)
                    DamageIndicator.DamageDelegate = null;
            };
            DrawingsMenu.Add("Plugins.Ezreal.DrawingsMenu.DamageIndicatorColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[3].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            TearStacker.Enabled = Settings.Misc.EnableTearStacker;
            TearStacker.OnlyInFountain = Settings.Misc.StackOnlyInFountain;
            TearStacker.MinimumManaPercent = Settings.Misc.MinimalManaPercentTearStacker;
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
                public static bool UseQ
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.UseQ"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.UseQ"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseQOnImmobile
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.UseQOnImmobile"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.UseQOnImmobile"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.UseQOnImmobile"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.UseQOnImmobile"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseW
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.UseW"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.UseW"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.UseW"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.UseW"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseE
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.UseE"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.UseE"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.UseE"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.UseE"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseR
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.UseR"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.UseR"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.UseR"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.UseR"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }
                public static int RMinEnemiesHit
                {
                    get
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.RMinEnemiesHit"] != null)
                            return ComboMenu["Plugins.Ezreal.ComboMenu.RMinEnemiesHit"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.ComboMenu.RMinEnemiesHit menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.RMinEnemiesHit"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.RMinEnemiesHit"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool RKeybind
                {
                    get
                    {
                        return ComboMenu?["Plugins.Ezreal.ComboMenu.RKeybind"] != null &&
                               ComboMenu["Plugins.Ezreal.ComboMenu.RKeybind"].Cast<KeyBind>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Ezreal.ComboMenu.RKeybind"] != null)
                            ComboMenu["Plugins.Ezreal.ComboMenu.RKeybind"].Cast<KeyBind>()
                                .CurrentValue
                                = value;
                    }
                }
            }

            internal static class Harass
            {
                public static bool UseQ
                {
                    get
                    {
                        return HarassMenu?["Plugins.Ezreal.HarassMenu.UseQ"] != null &&
                               HarassMenu["Plugins.Ezreal.HarassMenu.UseQ"].Cast<KeyBind>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Ezreal.HarassMenu.UseQ"] != null)
                            HarassMenu["Plugins.Ezreal.HarassMenu.UseQ"].Cast<KeyBind>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (HarassMenu?["Plugins.Ezreal.HarassMenu.MinManaQ"] != null)
                            return HarassMenu["Plugins.Ezreal.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.HarassMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Ezreal.HarassMenu.MinManaQ"] != null)
                            HarassMenu["Plugins.Ezreal.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool IsAutoHarassEnabledFor(AIHeroClient unit)
                {
                    return HarassMenu?["Plugins.Ezreal.HarassMenu.UseQ." + unit.Hero] != null &&
                           HarassMenu["Plugins.Ezreal.HarassMenu.UseQ." + unit.Hero].Cast<CheckBox>()
                               .CurrentValue;
                }

                public static bool IsAutoHarassEnabledFor(string championName)
                {
                    return HarassMenu?["Plugins.Ezreal.HarassMenu.UseQ." + championName] != null &&
                           HarassMenu["Plugins.Ezreal.HarassMenu.UseQ." + championName].Cast<CheckBox>()
                               .CurrentValue;
                }
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn"] != null &&
                               LaneClearMenu["Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int ScanRange
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.ScanRange"] != null)
                            return LaneClearMenu["Plugins.Ezreal.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.LaneClearMenu.ScanRange menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.ScanRange"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static int AllowedEnemies
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.AllowedEnemies"] != null)
                            return
                                LaneClearMenu["Plugins.Ezreal.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.LaneClearMenu.AllowedEnemies menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.AllowedEnemies"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseQInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.UseQInLaneClear"] != null &&
                               LaneClearMenu["Plugins.Ezreal.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.UseQInLaneClear"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseQInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.UseQInJungleClear"] != null &&
                               LaneClearMenu["Plugins.Ezreal.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.UseQInJungleClear"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.MinManaQ"] != null)
                            return LaneClearMenu["Plugins.Ezreal.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.LaneClearMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Ezreal.LaneClearMenu.MinManaQ"] != null)
                            LaneClearMenu["Plugins.Ezreal.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class Misc
            {
                public static bool EnableKillsteal
                {
                    get
                    {
                        return MiscMenu?["Plugins.Ezreal.MiscMenu.EnableKillsteal"] != null &&
                               MiscMenu["Plugins.Ezreal.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.EnableKillsteal"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool KeepPassiveStacks
                {
                    get
                    {
                        return MiscMenu?["Plugins.Ezreal.MiscMenu.KeepPassiveStacks"] != null &&
                               MiscMenu["Plugins.Ezreal.MiscMenu.KeepPassiveStacks"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.KeepPassiveStacks"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.KeepPassiveStacks"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool EAntiMelee
                {
                    get
                    {
                        return MiscMenu?["Plugins.Ezreal.MiscMenu.EAntiMelee"] != null &&
                               MiscMenu["Plugins.Ezreal.MiscMenu.EAntiMelee"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.EAntiMelee"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.EAntiMelee"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool EnableTearStacker
                {
                    get
                    {
                        return MiscMenu?["Plugins.Ezreal.MiscMenu.EnableTearStacker"] != null &&
                               MiscMenu["Plugins.Ezreal.MiscMenu.EnableTearStacker"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.EnableTearStacker"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.EnableTearStacker"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool StackOnlyInFountain
                {
                    get
                    {
                        return MiscMenu?["Plugins.Ezreal.MiscMenu.StackOnlyInFountain"] != null &&
                               MiscMenu["Plugins.Ezreal.MiscMenu.StackOnlyInFountain"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.StackOnlyInFountain"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.StackOnlyInFountain"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinimalManaPercentTearStacker
                {
                    get
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker"] != null)
                            return MiscMenu["Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker"] != null)
                            MiscMenu["Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady"] != null &&
                               DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady"] != null)
                            DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool DrawQ
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawQ"] != null &&
                               DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawQ"] != null)
                            DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawW
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawW"] != null &&
                               DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawW"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawW"] != null)
                            DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawW"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawE
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawE"] != null &&
                               DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawE"] != null)
                            DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue = value;
                    }
                }
                
                public static bool DrawDamageIndicator
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator"] != null &&
                               DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator"] != null)
                            DrawingsMenu["Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue = value;
                    }
                }
            }
        }
    }
}