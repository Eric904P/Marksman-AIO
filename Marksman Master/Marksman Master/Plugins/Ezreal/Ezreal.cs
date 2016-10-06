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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using EloBuddy.SDK.Rendering;
using Marksman_Master.Cache.Modules;
using Marksman_Master.PermaShow.Values;
using Marksman_Master.Utils;
using Color = SharpDX.Color;

namespace Marksman_Master.Plugins.Ezreal
{
    internal class Ezreal : ChampionPlugin
    {
        protected static Spell.Skillshot Q { get; }
        protected static Spell.Skillshot W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Skillshot R { get; }

        internal static Menu ComboMenu { get; set; }
        internal static Menu HarassMenu { get; set; }
        internal static Menu LaneClearMenu { get; set; }
        internal static Menu DrawingsMenu { get; set; }
        internal static Menu MiscMenu { get; set; }

        private static ColorPicker[] ColorPicker { get; }
        
        private static BoolItem AutoHarassItem { get; set; }

        private static bool _changingRangeScan;

        protected static bool IsPreAttack { get; private set; }
        protected static bool IsPostAttack { get; private set; }

        protected static bool HasPassiveBuff
            => Player.Instance.Buffs.Any(b => b.IsActive && b.Name.Equals("ezrealrisingspellforce", StringComparison.CurrentCultureIgnoreCase));

        protected static BuffInstance GetPassiveBuff
            => Player.Instance.Buffs.Find(b => b.IsActive && b.Name.Equals("ezrealrisingspellforce", StringComparison.CurrentCultureIgnoreCase));

        protected static int GetPassiveBuffAmount
            => HasPassiveBuff ? Player.Instance.Buffs.Find(
                        b => b.IsActive && b.Name.Equals("ezrealrisingspellforce", StringComparison.CurrentCultureIgnoreCase)).Count : 0;

        protected static Cache.Cache Cache => StaticCacheProvider.Cache;

        protected static CustomCache<int, float> ComboDamages { get; }

        private static readonly Text Text;

        static Ezreal()
        {
            Q = new Spell.Skillshot(SpellSlot.Q, 1200, SkillShotType.Linear, 250, 2000, 60);
            W = new Spell.Skillshot(SpellSlot.W, 1050, SkillShotType.Linear, 250, 1600, 80)
            {
                AllowedCollisionCount = int.MaxValue
            };
            E = new Spell.Skillshot(SpellSlot.E, 475, SkillShotType.Linear);
            R = new Spell.Skillshot(SpellSlot.R, 6000, SkillShotType.Linear, 1000, 2000, 160)
            {
                AllowedCollisionCount = int.MaxValue
            };

            ComboDamages = Cache.Resolve<CustomCache<int, float>>(1000);

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("EzrealQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("EzrealW", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("EzrealE", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("EzrealHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ChampionTracker.Initialize(ChampionTrackerFlags.LongCastTimeTracker);

            Obj_AI_Base.OnBasicAttack += Obj_AI_Base_OnBasicAttack;

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));

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

            Orbwalker.OnPostAttack += (a, b) =>
            {
                IsPreAttack = false;
                IsPostAttack = true;
            };

            ChampionTracker.OnLongSpellCast += ChampionTracker_OnLongSpellCast;
            ChampionTracker.OnPostBasicAttack += ChampionTracker_OnPostBasicAttack;
        }

        private static void ChampionTracker_OnPostBasicAttack(object sender, PostBasicAttackArgs e)
        {
            if (!e.Sender.IsMe)
                return;

            if (!Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
                return;

            if (!Q.IsReady() || !Settings.Combo.UseQ || Player.Instance.HasSheenBuff())
                return;

            var possibleTargets =
                StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                    x =>
                        x.IsValidTargetCached(Q.Range) &&
                        !StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero).Any(k =>
                            k.IsValidTargetCached(Q.Range - 80) &&
                            !k.HasSpellShield() &&
                            !k.HasUndyingBuffA() &&
                            (k.TotalHealthWithShields() < Player.Instance.GetSpellDamageCached(k, SpellSlot.Q))) &&
                        (Q.GetPrediction(x).HitChancePercent > 65) &&
                        !x.HasSpellShield() && !x.HasUndyingBuffA()).ToList();

            if (!possibleTargets.Any())
                return;

            var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

            if (target != null && !Player.Instance.HasSheenBuff() && !IsPreAttack)
            {
                Q.CastMinimumHitchance(target, 65);
            }
        }

        private static void Obj_AI_Base_OnBasicAttack(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe || sender.GetType() != typeof(AIHeroClient) || sender.IsEnemy || !Settings.Misc.WToPushTowers)
                return;

            if (W.IsReady() && sender.IsValidTargetCached(W.Range) && args.Target != null && args.Target.IsValid &&
                (args.Target.Type == GameObjectType.obj_AI_Turret ||
                 args.Target.Type == GameObjectType.obj_BarracksDampener || args.Target.Type == GameObjectType.obj_HQ))
            {
                W.CastMinimumHitchance(sender, 85);
            }
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
            else if (Settings.Harass.IsAutoHarassEnabledFor(e.Sender) && Q.IsReady() && Settings.Harass.UseQ && (Player.Instance.ManaPercent >= Settings.Harass.MinManaQ) &&
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

            return unit.GetType() != typeof(AIHeroClient) ? 0 : GetComboDamage(unit);
        }

        protected static float GetComboDamage(Obj_AI_Base unit)
        {
            if (MenuManager.IsCacheEnabled && ComboDamages.Exist(unit.NetworkId))
            {
                return ComboDamages.Get(unit.NetworkId);
            }

            var damage = 0f;

            if (Q.IsReady() && unit.IsValidTargetCached(Q.Range))
                damage += Player.Instance.GetSpellDamageCached(unit, SpellSlot.Q);

            if (W.IsReady() && unit.IsValidTargetCached(W.Range))
                damage += Player.Instance.GetSpellDamageCached(unit, SpellSlot.W);

            if (E.IsReady() && unit.IsValidTargetCached(E.Range))
                damage += Player.Instance.GetSpellDamageCached(unit, SpellSlot.E);

            if (R.IsReady() && unit.IsValidTargetCached(R.Range))
                damage += Player.Instance.GetSpellDamageCached(unit, SpellSlot.R);

            if (Player.Instance.IsInAutoAttackRange(unit))
                damage += Player.Instance.GetAutoAttackDamageCached(unit, true);

            if (MenuManager.IsCacheEnabled)
            {
                ComboDamages.Add(unit.NetworkId, damage);
            }

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
            
            if (!R.IsReady() || !Settings.Drawings.DrawDamageIndicator)
                return;

            foreach (var source in EntityManager.Heroes.Enemies.Where(
                x => x.IsHPBarRendered && x.Position.IsOnScreen()))
            {
                var hpPosition = source.HPBarPosition;
                hpPosition.Y = hpPosition.Y + 30;
                var percentDamage = Math.Min(100, Player.Instance.GetSpellDamageCached(source, SpellSlot.R) / source.TotalHealthWithShields() * 100);

                Text.X = (int)(hpPosition.X - 80);
                Text.Y = (int)source.HPBarPosition.Y;
                Text.Color = new Misc.HsvColor(Misc.GetNumberInRangeFromProcent(percentDamage, 3, 110), 1, 1).ColorFromHsv();
                Text.TextValue = $"R : {percentDamage.ToString("F1")}%";
                Text.Draw();
            }
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
                new KeyBind("Enable auto harass", false, KeyBind.BindTypes.PressToggle, 'A')).OnValueChange
                +=
                (sender, args) =>
                {
                    AutoHarassItem.Value = args.NewValue;
                };
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

            MiscMenu.AddLabel("Essence Flux (W) settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.WToPushTowers", new CheckBox("Use W on allies to push towers"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Arcane Shift (E) settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.EAntiMelee", new CheckBox("Use E against melee champions"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Trueshot Barrage (R) settings :");
            MiscMenu.Add("Plugins.Ezreal.MiscMenu.MaxRRangeKillsteal", new Slider("Max R range to enemy for killsteal", 8000, 0, 20000));
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
            
            AutoHarassItem = MenuManager.PermaShow.AddItem("Ezreal.AutoHarass", new BoolItem("Auto harass with Q", Settings.Harass.UseQ));
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
                public static bool UseQ => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.UseQ"];

                public static bool UseQOnImmobile => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.UseQOnImmobile"];

                public static bool UseW => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.UseW"];

                public static bool UseE => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.UseE"];

                public static bool UseR => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.UseR"];

                public static int RMinEnemiesHit => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.RMinEnemiesHit", true];

                public static bool RKeybind => MenuManager.MenuValues["Plugins.Ezreal.ComboMenu.RKeybind"];
            }

            internal static class Harass
            {
                public static bool UseQ => MenuManager.MenuValues["Plugins.Ezreal.HarassMenu.UseQ"];

                public static int MinManaQ => MenuManager.MenuValues["Plugins.Ezreal.HarassMenu.MinManaQ", true];

                public static bool IsAutoHarassEnabledFor(AIHeroClient unit) => MenuManager.MenuValues["Plugins.Ezreal.HarassMenu.UseQ." + unit.Hero];

                public static bool IsAutoHarassEnabledFor(string championName) => MenuManager.MenuValues["Plugins.Ezreal.HarassMenu.UseQ." + championName];
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.EnableLCIfNoEn"];

                public static int ScanRange => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.ScanRange", true];

                public static int AllowedEnemies => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.AllowedEnemies", true];

                public static bool UseQInLaneClear => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.UseQInLaneClear"];

                public static bool UseQInJungleClear => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.UseQInJungleClear"];

                public static int MinManaQ => MenuManager.MenuValues["Plugins.Ezreal.LaneClearMenu.MinManaQ", true];
            }

            internal static class Misc
            {
                public static bool EnableKillsteal => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.EnableKillsteal"];

                public static bool KeepPassiveStacks => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.KeepPassiveStacks"];

                public static bool EAntiMelee => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.EAntiMelee"];

                public static int MaxRRangeKillsteal => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.MaxRRangeKillsteal", true];

                public static bool EnableTearStacker => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.EnableTearStacker"];
                
                public static bool StackOnlyInFountain => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.StackOnlyInFountain"];

                public static bool WToPushTowers => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.WToPushTowers"];

                public static int MinimalManaPercentTearStacker => MenuManager.MenuValues["Plugins.Ezreal.MiscMenu.MinimalManaPercentTearStacker", true];
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady => MenuManager.MenuValues["Plugins.Ezreal.DrawingsMenu.DrawSpellRangesWhenReady"];

                public static bool DrawQ => MenuManager.MenuValues["Plugins.Ezreal.DrawingsMenu.DrawQ"];

                public static bool DrawW => MenuManager.MenuValues["Plugins.Ezreal.DrawingsMenu.DrawW"];

                public static bool DrawE => MenuManager.MenuValues["Plugins.Ezreal.DrawingsMenu.DrawE"];
                
                public static bool DrawDamageIndicator => MenuManager.MenuValues["Plugins.Ezreal.DrawingsMenu.DrawDamageIndicator"];
            }
        }
    }
}