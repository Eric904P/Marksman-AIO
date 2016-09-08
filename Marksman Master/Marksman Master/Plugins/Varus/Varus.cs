#region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="Varus.cs" company="EloBuddy">
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
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Rendering;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using EloBuddy.SDK.Utils;
using Marksman_Master.PermaShow.Values;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Varus
{
    internal class Varus : ChampionPlugin
    {
        protected static Spell.Chargeable Q { get; }
        protected static Spell.Active W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Skillshot R { get; }

        protected static Menu ComboMenu { get; set; }
        protected static Menu HarassMenu { get; set; }
        protected static Menu LaneClearMenu { get; set; }
        protected static Menu DrawingsMenu { get; set; }
        protected static Menu MiscMenu { get; set; }

        private static ColorPicker[] ColorPicker { get; }

        private static bool _changingRangeScan;

        private static readonly Dictionary<int, Dictionary<float, float>> Damages =
            new Dictionary<int, Dictionary<float, float>>();

        protected static BoolItem AutoHarass { get; private set; }

        protected static bool HasWDebuff(Obj_AI_Base unit) => unit.Buffs.Any(x => x.IsActive && x.Name.ToLower() == "varuswdebuff");
        protected static BuffInstance GetWDebuff(Obj_AI_Base unit) => HasWDebuff(unit) ? unit.Buffs.First(x => x.IsActive && x.Name.ToLower() == "varuswdebuff") : null;

        protected static bool IsPreAttack { get; private set; }

        static Varus()
        {
            Q = new Spell.Chargeable(SpellSlot.Q, 1000, 1600, 1500, 250, 2000, 60)
            {
                AllowedCollisionCount = int.MaxValue
            };
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Skillshot(SpellSlot.E, 900, SkillShotType.Circular, 250, 1500, 180);
            R = new Spell.Skillshot(SpellSlot.R, 1100, SkillShotType.Linear, 250, 2000, 115);

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("VarusQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("VarusE", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("VarusR", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("VarusHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color, (int) R.Range);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[3].OnColorChange +=
                (a, b) =>
                {
                    DamageIndicator.Color = b.Color;
                };
            
            Orbwalker.OnPostAttack += (sender, args) => IsPreAttack = false;
            Orbwalker.OnPreAttack += (target, args) => IsPreAttack = true;
        }

        private static float HandleDamageIndicator(Obj_AI_Base unit)
        {
            if (!Settings.Drawings.DrawDamageIndicator)
            {
                return 0;
            }

            return unit.GetType() != typeof (AIHeroClient) ? 0 : GetComboDamage(unit);
        }

        protected static float GetComboDamage(Obj_AI_Base unit)
        {
            if (Damages.ContainsKey(unit.NetworkId) &&
                !Damages.Any(x => x.Key == unit.NetworkId && x.Value.Any(k => Game.Time*1000 - k.Key > 200))) //
                return Damages[unit.NetworkId].Values.FirstOrDefault();

            var damage = Damage.GetWDamage(unit);

            if (Q.IsReady() && unit.IsValidTarget(Q.Range))
                damage += Damage.GetQDamage(unit);
            
            if (unit.IsValidTarget(E.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.E);

            if (unit.IsValidTarget(R.Range))
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.R);

            if (Player.Instance.IsInAutoAttackRange(unit))
                damage += Player.Instance.GetAutoAttackDamage(unit);

            Damages[unit.NetworkId] = new Dictionary<float, float> {{Game.Time*1000, damage}};

            return damage;
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(Color.White,
                    LaneClearMenu["Plugins.Varus.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);
            
            if (Settings.Drawings.DrawQ && (!Settings.Drawings.DrawSpellRangesWhenReady || Q.IsReady()))
                Circle.Draw(ColorPicker[0].Color, Q.Range, Player.Instance);
            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[1].Color, E.Range, Player.Instance);
            if (Settings.Drawings.DrawR && (!Settings.Drawings.DrawSpellRangesWhenReady || R.IsReady()))
                Circle.Draw(ColorPicker[2].Color, R.Range, Player.Instance);
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
            ComboMenu.AddGroupLabel("Combo mode settings for Varus addon");

            ComboMenu.AddLabel("Piercing Arrow (Q) settings :");
            ComboMenu.Add("Plugins.Varus.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Varus.ComboMenu.QMinDistanceToTarget", new Slider("Minimum distance to target to use Q", 500, 0, 1250));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Hail of Arrows (E) settings :");
            ComboMenu.Add("Plugins.Varus.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.Add("Plugins.Varus.ComboMenu.UseEToProc", new CheckBox("Use E only to proc 3rd W stack", false));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Chain of Corruption (R) settings :");
            ComboMenu.Add("Plugins.Varus.ComboMenu.UseR", new CheckBox("Use R", false));
            ComboMenu.Add("Plugins.Varus.ComboMenu.RKeybind", new KeyBind("Manual R keybind", false, KeyBind.BindTypes.HoldActive, 'W'));

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Varus addon");

            HarassMenu.AddLabel("Piercing Arrow (Q) settings :");
            HarassMenu.Add("Plugins.Varus.HarassMenu.AutoHarass",
                new KeyBind("Enable AutoHarass with Q", false, KeyBind.BindTypes.PressToggle, 'T')).OnValueChange +=
                (sender, args) =>
                {
                    AutoHarass.Value = args.NewValue;
                };
            HarassMenu.Add("Plugins.Varus.HarassMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 50, 1));
            HarassMenu.AddSeparator(2);

            if (EntityManager.Heroes.Enemies.Any())
            {
                HarassMenu.AddLabel("Enable auto harras for : ");

                EntityManager.Heroes.Enemies.ForEach(x => HarassMenu.Add("Plugins.Varus.HarassMenu.AutoHarassEnabled."+x.ChampionName, new CheckBox(x.ChampionName == "MonkeyKing" ? "Wukong" : x.ChampionName)));
            }

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Varus addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Lane clear settings :");
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q in Lane clear", false));
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.MinMinionsHitQ", new Slider("Min minions hit to use Q", 3, 1, 6));
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.UseEInLaneClear", new CheckBox("Use E in Lane clear", false));
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.MinMinionsHitE", new Slider("Min minions hit to use E", 3, 1, 6));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Jungle clear settings :");
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle clear", false));
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.UseEInJungleClear", new CheckBox("Use E in Jungle clear"));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Mana settings :");
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 50, 1));
            LaneClearMenu.Add("Plugins.Varus.LaneClearMenu.MinManaE", new Slider("Min mana percentage ({0}%) to use E", 50, 1));

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Varus addon");
            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Varus.MiscMenu.EnableKillsteal", new CheckBox("Enable Killsteal"));

            MenuManager.BuildAntiGapcloserMenu();

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Varus addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawSpellRangesWhenReady", new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Piercing Arrow (Q) settings :");
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawQ", new CheckBox("Draw Q range"));
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawQColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Hail of Arrows (E) settings :");
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawE", new CheckBox("Draw E range", false));
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawEColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Chain of Corruption (R) settings :");
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawR", new CheckBox("Draw R range", false));
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawRColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[2].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };

            DrawingsMenu.AddLabel("Damage indicator settings :");
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DrawDamageIndicator", new CheckBox("Draw damage indicator")).OnValueChange += (a, b) =>
            {
                if (b.NewValue)
                    DamageIndicator.DamageDelegate = HandleDamageIndicator;
                else if (!b.NewValue)
                    DamageIndicator.DamageDelegate = null;
            };
            DrawingsMenu.Add("Plugins.Varus.DrawingsMenu.DamageIndicatorColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[3].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };

            AutoHarass = MenuManager.PermaShow.AddItem("Varus.AutoHarass",
                new BoolItem("Enable auto harass with Q", Settings.Harass.AutoHarassWithQ));
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

        protected internal static class Settings
        {
            internal static class Combo
            {
                public static bool UseQ
                {
                    get
                    {
                        return ComboMenu?["Plugins.Varus.ComboMenu.UseQ"] != null &&
                               ComboMenu["Plugins.Varus.ComboMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.UseQ"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int QMinDistanceToTarget
                {
                    get
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.QMinDistanceToTarget"] != null)
                            return ComboMenu["Plugins.Varus.ComboMenu.QMinDistanceToTarget"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.ComboMenu.QMinDistanceToTarget menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.QMinDistanceToTarget"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.QMinDistanceToTarget"].Cast<Slider>().CurrentValue = value;
                    }
                }
                
                public static bool UseE
                {
                    get
                    {
                        return ComboMenu?["Plugins.Varus.ComboMenu.UseE"] != null &&
                               ComboMenu["Plugins.Varus.ComboMenu.UseE"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.UseE"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.UseE"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseEToProc
                {
                    get
                    {
                        return ComboMenu?["Plugins.Varus.ComboMenu.UseEToProc"] != null &&
                               ComboMenu["Plugins.Varus.ComboMenu.UseEToProc"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.UseEToProc"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.UseEToProc"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseR
                {
                    get
                    {
                        return ComboMenu?["Plugins.Varus.ComboMenu.UseR"] != null &&
                               ComboMenu["Plugins.Varus.ComboMenu.UseR"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.UseR"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.UseR"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool RKeybind
                {
                    get
                    {
                        return ComboMenu?["Plugins.Varus.ComboMenu.RKeybind"] != null &&
                               ComboMenu["Plugins.Varus.ComboMenu.RKeybind"].Cast<KeyBind>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Varus.ComboMenu.RKeybind"] != null)
                            ComboMenu["Plugins.Varus.ComboMenu.RKeybind"].Cast<KeyBind>()
                                .CurrentValue
                                = value;
                    }
                }
            }

            internal static class Harass
            {
                public static bool AutoHarassWithQ
                {
                    get
                    {
                        return HarassMenu?["Plugins.Varus.HarassMenu.AutoHarass"] != null &&
                               HarassMenu["Plugins.Varus.HarassMenu.AutoHarass"].Cast<KeyBind>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Varus.HarassMenu.AutoHarass"] != null)
                            HarassMenu["Plugins.Varus.HarassMenu.AutoHarass"].Cast<KeyBind>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (HarassMenu?["Plugins.Varus.HarassMenu.MinManaQ"] != null)
                            return HarassMenu["Plugins.Varus.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.HarassMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Varus.HarassMenu.MinManaQ"] != null)
                            HarassMenu["Plugins.Varus.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool IsAutoHarassEnabledFor(AIHeroClient unit)
                {
                    return HarassMenu?["Plugins.Varus.HarassMenu.AutoHarassEnabled." + unit.ChampionName] != null &&
                               HarassMenu["Plugins.Varus.HarassMenu.AutoHarassEnabled." + unit.ChampionName].Cast<CheckBox>()
                                   .CurrentValue;
                }

                public static bool IsAutoHarassEnabledFor(string championName)
                {
                    return HarassMenu?["Plugins.Varus.HarassMenu.AutoHarassEnabled." + championName] != null &&
                               HarassMenu["Plugins.Varus.HarassMenu.AutoHarassEnabled." + championName].Cast<CheckBox>()
                                   .CurrentValue;
                }
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Varus.LaneClearMenu.EnableLCIfNoEn"] != null &&
                               LaneClearMenu["Plugins.Varus.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.EnableLCIfNoEn"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int ScanRange
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.ScanRange"] != null)
                            return LaneClearMenu["Plugins.Varus.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.ScanRange menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.ScanRange"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static int AllowedEnemies
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.AllowedEnemies"] != null)
                            return
                                LaneClearMenu["Plugins.Varus.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.AllowedEnemies menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.AllowedEnemies"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseQInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseQInLaneClear"] != null &&
                               LaneClearMenu["Plugins.Varus.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseQInLaneClear"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinMinionsHitQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinMinionsHitQ"] != null)
                            return
                                LaneClearMenu["Plugins.Varus.LaneClearMenu.MinMinionsHitQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.MinMinionsHitQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinMinionsHitQ"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.MinMinionsHitQ"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseEInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseEInLaneClear"] != null &&
                               LaneClearMenu["Plugins.Varus.LaneClearMenu.UseEInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseEInLaneClear"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.UseEInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinMinionsHitE
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinMinionsHitE"] != null)
                            return
                                LaneClearMenu["Plugins.Varus.LaneClearMenu.MinMinionsHitE"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.MinMinionsHitE menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinMinionsHitE"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.MinMinionsHitE"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseQInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseQInJungleClear"] != null &&
                               LaneClearMenu["Plugins.Varus.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseQInJungleClear"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseEInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseEInJungleClear"] != null &&
                               LaneClearMenu["Plugins.Varus.LaneClearMenu.UseEInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.UseEInJungleClear"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.UseEInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinManaQ"] != null)
                            return
                                LaneClearMenu["Plugins.Varus.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinManaQ"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static int MinManaE
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinManaE"] != null)
                            return
                                LaneClearMenu["Plugins.Varus.LaneClearMenu.MinManaE"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Varus.LaneClearMenu.MinManaE menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Varus.LaneClearMenu.MinManaE"] != null)
                            LaneClearMenu["Plugins.Varus.LaneClearMenu.MinManaE"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }
            }

            internal static class Misc
            {
                public static bool EnableKillsteal
                {
                    get
                    {
                        return MiscMenu?["Plugins.Varus.MiscMenu.EnableKillsteal"] != null &&
                               MiscMenu["Plugins.Varus.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Varus.MiscMenu.EnableKillsteal"] != null)
                            MiscMenu["Plugins.Varus.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawSpellRangesWhenReady"] != null &&
                               DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawSpellRangesWhenReady"] != null)
                            DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool DrawQ
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawQ"] != null &&
                               DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawQ"] != null)
                            DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawE
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawE"] != null &&
                               DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawE"] != null)
                            DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawR
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawR"] != null &&
                               DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawR"] != null)
                            DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawDamageIndicator
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawDamageIndicator"] != null &&
                               DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Varus.DrawingsMenu.DrawDamageIndicator"] != null)
                            DrawingsMenu["Plugins.Varus.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue = value;
                    }
                }
            }
        }

        protected static class Damage
        {
            public static float GetQDamage(Obj_AI_Base unit)
            {
                float[] minDamage = { 0, 10, 46.7f, 83.3f, 120, 156.7f };
                float[] maxDamage = { 0, 15, 70, 125, 180, 235 };

                var time = Math.Min(Core.GameTickCount - Q.ChargingStartedTime, Q.FullyChargedTime);
                var percent = (float)(Misc.GetProcentFromNumberRange(time, 0, Q.FullyChargedTime) / 100);
                var damage = Misc.GetNumberInRangeFromProcent(percent * 100, minDamage[Q.Level], maxDamage[Q.Level]) + Player.Instance.TotalAttackDamage * Misc.GetNumberInRangeFromProcent(percent * 100, 1, 1.6);

                var collision = 1f-0.15f*Q.GetPrediction(unit).CollisionObjects.Count(where => where.NetworkId != unit.NetworkId && where.IsEnemy);

                return Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical, (float)damage*collision);
            }

            public static float GetWDamage(Obj_AI_Base unit)
            {
                if (!HasWDebuff(unit))
                    return 0;

                float[] magicDamagePerStack = {0, 0.02f, 0.0275f, 0.035f, 0.0425f, 0.05f};

                var stacks = GetWDebuff(unit).Count;

                var additionalDamage = 2*(Player.Instance.FlatMagicDamageMod/100);

                var damage = unit.MaxHealth*magicDamagePerStack[W.Level]*stacks +
                             (additionalDamage - additionalDamage%2);

                return Player.Instance.CalculateDamageOnUnit(unit, DamageType.Magical,
                    damage > 360 && unit.GetType() != typeof (AIHeroClient) ? 360 : damage);
            }
        }
    }
}