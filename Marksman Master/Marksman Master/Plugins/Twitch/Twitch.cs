#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Twitch.cs" company="EloBuddy">
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
using Marksman_Master.Cache.Modules;
using SharpDX;
using Marksman_Master.Utils;
using Color = SharpDX.Color;

namespace Marksman_Master.Plugins.Twitch
{
    internal class Twitch : ChampionPlugin
    {
        protected static Spell.Active Q { get; }
        protected static Spell.Skillshot W { get; }
        protected static Spell.Active E { get; }
        protected static Spell.Active R { get; }

        internal static Menu ComboMenu { get; set; }
        internal static Menu HarassMenu { get; set; }
        internal static Menu JungleClearMenu { get; set; }
        internal static Menu LaneClearMenu { get; set; }
        internal static Menu MiscMenu { get; set; }
        internal static Menu DrawingsMenu { get; set; }

        private static readonly ColorPicker[] ColorPicker;

        protected static bool HasDeadlyVenomBuff(Obj_AI_Base unit)
            => unit.Buffs.Any(b => b.IsActive && b.DisplayName.Equals("twitchdeadlyvenom", StringComparison.CurrentCultureIgnoreCase));

        protected static BuffInstance GetDeadlyVenomBuff(Obj_AI_Base unit) => unit.Buffs.FirstOrDefault(
            b => b.IsActive && b.DisplayName.Equals("twitchdeadlyvenom", StringComparison.CurrentCultureIgnoreCase));

        protected static bool IsCastingR
            => Player.Instance.Buffs.Any(b => b.IsActive && b.Name.Equals("twitchfullautomatic", StringComparison.CurrentCultureIgnoreCase));

        private static readonly Text Text;

        private static bool _changingRangeScan;

        protected static Cache.Cache Cache => StaticCacheProvider.Cache;

        static Twitch()
        {
            Q = new Spell.Active(SpellSlot.Q);
            W = new Spell.Skillshot(SpellSlot.W, 900, SkillShotType.Circular, 250, 1400, 275)
            {
                AllowedCollisionCount = int.MaxValue
            };
            E = new Spell.Active(SpellSlot.E, 1200);
            R = new Spell.Active(SpellSlot.R, 950);
            
            ColorPicker = new ColorPicker[4];
            
            ColorPicker[0] = new ColorPicker("TwitchW", new ColorBGRA(243, 109, 160, 255));
            ColorPicker[1] = new ColorPicker("TwitchE", new ColorBGRA(255, 210, 54, 255));
            ColorPicker[2] = new ColorPicker("TwitchR", new ColorBGRA(241, 188, 160, 255));
            ColorPicker[3] = new ColorPicker("TwitchHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color, (int)E.Range);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[3].OnColorChange += (sender, args) =>
            {
                DamageIndicator.Color = args.Color;
            };

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));

            Orbwalker.OnPreAttack += Orbwalker_OnPreAttack;
            Orbwalker.OnPostAttack += Orbwalker_OnPostAttack;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Game.OnNotify += Game_OnNotify;
        }

        private static void Orbwalker_OnPostAttack(AttackableUnit target, EventArgs args)
        {
            if (Q.IsReady() && Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && Settings.Combo.UseQ && (target?.GetType() == typeof (AIHeroClient)) &&
                target.IsValidTargetCached(Player.Instance.GetAutoAttackRange() - 100) &&
                (Player.Instance.Mana >= 130 + (R.IsReady() ? 100 : 0)))
            {
                Q.Cast();
            }
        }

        private static void Game_OnNotify(GameNotifyEventArgs args)
        {
            if (Q.IsReady() && Settings.Combo.UseQAfterKill && (args.NetworkId == Player.Instance.NetworkId) && (args.EventId == GameEventId.OnChampionKill))
            {
                Core.DelayAction(() =>
                {
                    if (StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero)
                            .Any(x => x.IsValidTarget(1500)))
                    {
                        Q.Cast();
                    }
                }, 150);
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (!sender.Owner.IsMe)
                return;

            if (args.Slot == SpellSlot.R)
            {
                if (Activator.Activator.Items[ItemsEnum.Ghostblade] != null)
                {
                    Activator.Activator.Items[ItemsEnum.Ghostblade].UseItem();
                }
            }

            if ((args.Slot != SpellSlot.Recall) || !Q.IsReady() || !Settings.Misc.StealthRecall || Player.Instance.IsInShopRange())
                return;

            Q.Cast();

            Core.DelayAction(() => Player.CastSpell(SpellSlot.Recall), 500); //bug possible stackoverflow w/o coredelay

            args.Process = false;
        }

        private static void Orbwalker_OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            if (R.IsReady() && Settings.Combo.UseR && (target.GetType() == typeof(AIHeroClient)))
            {
                if (Player.Instance.CountEnemiesInRangeCached(1000) < Settings.Combo.RIfEnemiesHit)
                    return;

                var polygon = new Geometry.Polygon.Rectangle(Player.Instance.Position, Player.Instance.Position.Extend(args.Target, 850).To3D(), 65);

                var count =
                    StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero,
                        x =>
                            !x.IsDead && x.IsValidTargetCached(950) &&
                            new Geometry.Polygon.Circle(x.Position, x.BoundingRadius).Points.Any(
                                k => polygon.IsInside(k))).Count();

                if (count >= Settings.Combo.RIfEnemiesHit)
                {
                    Misc.PrintInfoMessage("Casting R because it can hit <font color=\"#ff1493\">" + count + "</font>. enemies");
                    R.Cast();
                }
            }
        }

        private static float HandleDamageIndicator(Obj_AI_Base unit)
        {
            if (!Settings.Drawings.DrawDamageIndicator)
            {
                return 0;
            }

            var enemy = (AIHeroClient)unit;

            return enemy != null ? Damage.GetEDamage(enemy) : 0;
        }

        protected override void OnDraw()
        {
            if (Settings.Drawings.DrawW && (!Settings.Drawings.DrawSpellRangesWhenReady || W.IsReady()))
                Circle.Draw(ColorPicker[0].Color, W.Range, Player.Instance);
            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[1].Color, E.Range, Player.Instance);
            if (Settings.Drawings.DrawR && (!Settings.Drawings.DrawSpellRangesWhenReady || R.IsReady()))
                Circle.Draw(ColorPicker[2].Color, R.Range, Player.Instance);

            if (_changingRangeScan)
                Circle.Draw(Color.White,
                    LaneClearMenu["Plugins.Twitch.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (!Settings.Drawings.DrawDamageIndicator)
                return;
            
            foreach (var source in StaticCacheProvider.GetChampions(CachedEntityType.EnemyHero, x => x.IsVisible && x.IsHPBarRendered && x.Position.IsOnScreen() && HasDeadlyVenomBuff(x)))
            {
                var hpPosition = source.HPBarPosition;
                hpPosition.Y = hpPosition.Y + 30; // tracker friendly.

                if (GetDeadlyVenomBuff(source) != null)
                {
                    var timeLeft = GetDeadlyVenomBuff(source).EndTime - Game.Time;
                    var endPos = timeLeft * 0x3e8 / 0x37;

                    var degree = Misc.GetNumberInRangeFromProcent(timeLeft * 1000d / 6000d * 100d, 3, 110);
                    var color = new Misc.HsvColor(degree, 1, 1).ColorFromHsv();

                    Text.X = (int)(hpPosition.X + endPos);
                    Text.Y = (int)hpPosition.Y + 15; // + text size 
                    Text.Color = color;
                    Text.TextValue = timeLeft.ToString("F1");
                    Text.Draw();

                    Drawing.DrawLine(hpPosition.X + endPos, hpPosition.Y, hpPosition.X, hpPosition.Y, 1, color);
                }

                var percentDamage = Math.Min(100, Damage.GetEDamage(source) / source.TotalHealthWithShields() * 100);

                Text.X = (int)(hpPosition.X - 50);
                Text.Y = (int)source.HPBarPosition.Y;
                Text.Color = new Misc.HsvColor(Misc.GetNumberInRangeFromProcent(percentDamage, 3, 110), 1, 1).ColorFromHsv();
                Text.TextValue = percentDamage.ToString("F1");
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
            ComboMenu.AddGroupLabel("Combo mode settings for Twitch addon");

            ComboMenu.AddLabel("Ambush (Q) settings :");
            ComboMenu.Add("Plugins.Twitch.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Twitch.ComboMenu.UseQAfterKill", new CheckBox("Use Q after kill"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Venom Cask (W) settings :");
            ComboMenu.Add("Plugins.Twitch.ComboMenu.UseW", new CheckBox("Use W"));
            ComboMenu.Add("Plugins.Twitch.ComboMenu.BlockWIfRIsActive", new CheckBox("Don't use W if R is active"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Contaminate (E) settings :");
            ComboMenu.Add("Plugins.Twitch.ComboMenu.UseE", new CheckBox("Use E"));
            var mode = ComboMenu.Add("Plugins.Twitch.ComboMenu.UseEIfDmg", new ComboBox("E usage mode", 0, "Percentage", "At stacks", "Only to killsteal"));
            ComboMenu.AddSeparator(10);
            ComboMenu.AddLabel("Percentage : Uses E only if it will deal desired percentage of enemy current health.\nAt stacks : Uses E only if desired amount of stack are reached on enemy.\nOnly to killsteal : " +
                               "Uses E only to execute enemies.");
            ComboMenu.AddSeparator(10);

            var percentage = ComboMenu.Add("Plugins.Twitch.ComboMenu.EAtStacks",
                new Slider("Use E if will deal ({0}%) percentage of enemy hp.", 30));

            switch (mode.CurrentValue)
            {
                case 0:
                    percentage.DisplayName = "Use E if will deal ({0}%) percentage of enemy hp.";
                    percentage.MinValue = 0;
                    percentage.MaxValue = 100;
                    break;
                case 1:
                    percentage.DisplayName = "Use E at {0} stacks.";
                    percentage.MinValue = 1;
                    percentage.MaxValue = 6;
                    break;
                case 2:
                    percentage.IsVisible = false;
                    break;
            }
            mode.OnValueChange += (a, b) =>
            {
                switch (b.NewValue)
                {
                    case 0:
                        percentage.DisplayName = "Use E if will deal ({0}%) percentage of enemy hp.";
                        percentage.MinValue = 0;
                        percentage.MaxValue = 100;
                        percentage.IsVisible = true;
                        break;
                    case 1:
                        percentage.DisplayName = "Use E at {0} stacks.";
                        percentage.MinValue = 1;
                        percentage.MaxValue = 6;
                        percentage.IsVisible = true;
                        break;
                    case 2:
                        percentage.IsVisible = false;
                        break;
                }
            };
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Rat-Ta-Tat-Tat (R) settings :");
            ComboMenu.Add("Plugins.Twitch.ComboMenu.UseR", new CheckBox("Use R"));
            ComboMenu.Add("Plugins.Twitch.ComboMenu.RIfEnemiesHit", new Slider("Use R if gonna hit {0} enemies", 3, 1, 5));
            ComboMenu.AddSeparator(5);
            ComboMenu.Add("Plugins.Twitch.ComboMenu.RifTargetOutOfRange", new CheckBox("Use R if target is out of range", false));
            ComboMenu.AddLabel("Uses R if target is killabe, but he is not inside basic attack range, and R won't be up in next 2 secs.");

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Twitch addon");

            HarassMenu.AddLabel("Venom Cask (W) settings :");
            HarassMenu.Add("Plugins.Twitch.HarassMenu.UseW", new CheckBox("Use W", false));
            HarassMenu.Add("Plugins.Twitch.HarassMenu.WMinMana", new Slider("Min mana percentage ({0}%) to use W", 80, 1));
            HarassMenu.AddSeparator(5);

            HarassMenu.AddLabel("Contaminate (E) settings :");
            HarassMenu.Add("Plugins.Twitch.HarassMenu.UseE", new CheckBox("Use E", false));
            HarassMenu.Add("Plugins.Twitch.HarassMenu.TwoEnemiesMin", new CheckBox("Only if will hit 2 or more enemies", false));
            HarassMenu.Add("Plugins.Twitch.HarassMenu.EMinMana", new Slider("Min mana percentage ({0}%) to use E", 80, 1));
            HarassMenu.Add("Plugins.Twitch.HarassMenu.EMinStacks", new Slider("Min stacks to use E", 6, 1, 6));

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Lane clear");
            LaneClearMenu.AddGroupLabel("Lane clear mode settings for Twitch addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Venom Cask (W) settings :");
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.UseW", new CheckBox("Use W", false));
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.WMinMana", new Slider("Min mana percentage ({0}%) to use W", 80, 1));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Contaminate (E) settings :");
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.UseE", new CheckBox("Use E", false));
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.EMinMana", new Slider("Min mana percentage ({0}%) to use E", 80, 1));
            LaneClearMenu.Add("Plugins.Twitch.LaneClearMenu.EMinMinionsHit", new Slider("Min minions hit to use E", 4, 1, 7));

            JungleClearMenu = MenuManager.Menu.AddSubMenu("Jungle clear");
            JungleClearMenu.AddGroupLabel("Jungle clear mode settings for Twitch addon");

            JungleClearMenu.AddLabel("Venom Cask (W) settings :");
            JungleClearMenu.Add("Plugins.Twitch.JungleClearMenu.UseW", new CheckBox("Use W", false));
            JungleClearMenu.Add("Plugins.Twitch.JungleClearMenu.WMinMana", new Slider("Min mana percentage ({0}%) to use W", 80, 1));
            JungleClearMenu.AddSeparator(5);

            JungleClearMenu.AddLabel("Contaminate (E) settings :");
            JungleClearMenu.Add("Plugins.Twitch.JungleClearMenu.UseE", new CheckBox("Use E"));
            JungleClearMenu.Add("Plugins.Twitch.JungleClearMenu.EMinMana", new Slider("Min mana percentage ({0}%) to use E", 30, 1));
            JungleClearMenu.AddLabel("Uses E only on big monsters and buffs");

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Twitch addon");

            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Twitch.MiscMenu.StealthRecall", new CheckBox("Enable steath recall"));

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Twitch addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawSpellRangesWhenReady",
                new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Venom Cask (W) drawing settings :");
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawW", new CheckBox("Draw W range", false));
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawWColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Contaminate (E) drawing settings :");
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawE", new CheckBox("Draw E range"));
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawEColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Rat-Ta-Tat-Tat (R) drawing settings :");
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawR", new CheckBox("Draw R range"));
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawRColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[2].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Damage indicator drawing settings :");
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawDamageIndicator",
                new CheckBox("Draw damage indicator on enemy HP bars", false)).OnValueChange += (a, b) =>
                {
                    if (b.NewValue)
                        DamageIndicator.DamageDelegate = HandleDamageIndicator;
                    else if(!b.NewValue)
                        DamageIndicator.DamageDelegate = null;
                };
            DrawingsMenu.Add("Plugins.Twitch.DrawingsMenu.DrawDamageIndicatorColor",
                new CheckBox("Change color", false)).OnValueChange += (a, b) =>
                {
                    if (!b.NewValue)
                        return;

                    ColorPicker[3].Initialize(System.Drawing.Color.Aquamarine);
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

        internal static class Settings
        {
            internal static class Combo
            {
                public static bool UseQAfterKill => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseQAfterKill"];

                public static bool UseQ => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseQ"];

                public static bool UseW => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseW"];

                public static bool BlockWIfRIsActive => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.BlockWIfRIsActive"]; 

                public static bool UseE => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseE"];

                public static int EMode => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseEIfDmg", true];

                public static int EAt => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.EAtStacks", true];

                public static bool UseR => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.UseR"];

                public static bool RifTargetOutOfRange => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.RifTargetOutOfRange"];

                public static int RIfEnemiesHit => MenuManager.MenuValues["Plugins.Twitch.ComboMenu.RIfEnemiesHit", true];
            }

            internal static class Harass
            {
                public static bool UseW => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.UseW"];

                public static int MinManaToUseW => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.WMinMana", true];

                public static bool UseE => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.UseE"];

                public static bool TwoEnemiesMin => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.TwoEnemiesMin"];

                public static int EMinMana => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.EMinMana", true];

                public static int EMinStacks => MenuManager.MenuValues["Plugins.Twitch.HarassMenu.EMinStacks", true];
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.EnableLCIfNoEn"];

                public static int ScanRange => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.ScanRange", true];

                public static int AllowedEnemies => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.AllowedEnemies", true];

                public static bool UseW => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.UseW"];

                public static int WMinMana => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.WMinMana", true];

                public static bool UseE => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.UseE"];

                public static int EMinMana => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.EMinMana", true];

                public static int EMinMinionsHit => MenuManager.MenuValues["Plugins.Twitch.LaneClearMenu.EMinMinionsHit", true];
            }

            internal static class JungleClear
            {
                public static bool UseW => MenuManager.MenuValues["Plugins.Twitch.JungleClearMenu.UseW"];
                
                public static int WMinMana => MenuManager.MenuValues["Plugins.Twitch.JungleClearMenu.WMinMana", true];

                public static bool UseE => MenuManager.MenuValues["Plugins.Twitch.JungleClearMenu.UseE"];

                public static int EMinMana => MenuManager.MenuValues["Plugins.Twitch.JungleClearMenu.EMinMana", true];
            }

            internal static class Misc
            {
                public static bool StealthRecall => MenuManager.MenuValues["Plugins.Twitch.MiscMenu.StealthRecall"];
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady => MenuManager.MenuValues["Plugins.Twitch.DrawingsMenu.DrawSpellRangesWhenReady"];
                
                public static bool DrawW => MenuManager.MenuValues["Plugins.Twitch.DrawingsMenu.DrawW"];

                public static bool DrawE => MenuManager.MenuValues["Plugins.Twitch.DrawingsMenu.DrawE"];

                public static bool DrawR => MenuManager.MenuValues["Plugins.Twitch.DrawingsMenu.DrawR"];

                public static bool DrawDamageIndicator => MenuManager.MenuValues["Plugins.Twitch.DrawingsMenu.DrawDamageIndicator"];
            }
        }
        
        internal static class Damage
        {
            private static float[] EDamage { get; } = { 0, 20, 35, 50, 65, 80 };
            private static float[] EDamagePerStack { get; } = { 0, 15, 20, 25, 30, 35 };
            private static float EDamagePerStackBounsAdMod { get; } = 0.25f;
            private static float EDamagePerStackBounsApMod { get; } = 0.2f;
            public static int[] RBonusAd { get; } = {0, 20, 30, 40};

            private static CustomCache<KeyValuePair<int, int>, float> ComboDamages { get; } = Cache.Resolve<CustomCache<KeyValuePair<int, int>, float>>(1000);
            private static CustomCache<int, int> EStacks { get; } = Cache.Resolve<CustomCache<int, int>>(100);
            private static CustomCache<Tuple<int, bool, int>, float> EDamages { get; } = Cache.Resolve<CustomCache<Tuple<int, bool, int>, float>>(250);
            private static CustomCache<KeyValuePair<int, int>, float> PassiveDamages { get; } = Cache.Resolve<CustomCache<KeyValuePair<int, int>, float>>(250);

            public static float GetComboDamage(AIHeroClient enemy, int autos = 0)
            {
                if (MenuManager.IsCacheEnabled && ComboDamages.Exist(new KeyValuePair<int, int>(enemy.NetworkId, autos)))
                {
                    return ComboDamages.Get(new KeyValuePair<int, int>(enemy.NetworkId, autos));
                }
                
                float damage = 0;

                if ((Activator.Activator.Items[ItemsEnum.BladeOfTheRuinedKing] != null) &&
                    Activator.Activator.Items[ItemsEnum.BladeOfTheRuinedKing].ToItem().IsReady())
                {
                    damage += Player.Instance.GetItemDamage(enemy, ItemId.Blade_of_the_Ruined_King);
                }

                if ((Activator.Activator.Items[ItemsEnum.Cutlass] != null) && Activator.Activator.Items[ItemsEnum.Cutlass].ToItem().IsReady())
                    damage += Player.Instance.GetItemDamage(enemy, ItemId.Bilgewater_Cutlass);

                if ((Activator.Activator.Items[ItemsEnum.Gunblade] != null) && Activator.Activator.Items[ItemsEnum.Gunblade].ToItem().IsReady())
                    damage += Player.Instance.GetItemDamage(enemy, ItemId.Hextech_Gunblade);

                if (E.IsReady())
                    damage += GetEDamage(enemy, true, autos > 0 ? autos : CountEStacks(enemy));
                
                damage += Player.Instance.GetAutoAttackDamageCached(enemy, true) * autos < 1 ? 1 : autos;

                if (MenuManager.IsCacheEnabled)
                {
                    ComboDamages.Add(new KeyValuePair<int, int>(enemy.NetworkId, autos), damage);
                }

                return damage;
            }

            public static bool CanCastEOnUnit(Obj_AI_Base target)
            {
                if ((target == null) || !target.IsValidTargetCached(E.Range) || (GetDeadlyVenomBuff(target) == null))
                    return false;

                if (target.GetType() != typeof(AIHeroClient))
                    return true;

                var heroClient = (AIHeroClient) target;

                return !heroClient.HasUndyingBuffA() && !heroClient.HasSpellShield();
            }

            public static bool IsTargetKillableByE(Obj_AI_Base target)
            {
                if (!CanCastEOnUnit(target))
                    return false;

                if (target.GetType() != typeof(AIHeroClient))
                {
                    return GetEDamage(target) > target.TotalHealthWithShields();
                }

                var heroClient = (AIHeroClient) target;

                if (heroClient.HasUndyingBuffA() || heroClient.HasSpellShield())
                {
                    return false;
                }

                if (heroClient.ChampionName != "Blitzcrank")
                    return GetEDamage(heroClient) >= heroClient.TotalHealthWithShields();

                if (!heroClient.HasBuff("BlitzcrankManaBarrierCD") && !heroClient.HasBuff("ManaBarrier"))
                {
                    return GetEDamage(heroClient) > heroClient.TotalHealthWithShields() + heroClient.Mana/2;
                }
                return GetEDamage(heroClient) > heroClient.TotalHealthWithShields();
            }

            public static float GetPassiveDamage(Obj_AI_Base target, int stacks = -1)
            {
                if (MenuManager.IsCacheEnabled && PassiveDamages.Exist(new KeyValuePair<int, int>(target.NetworkId, stacks)))
                {
                    return PassiveDamages.Get(new KeyValuePair<int, int>(target.NetworkId, stacks));
                }

                if (!HasDeadlyVenomBuff(target))
                    return 0;

                var damagePerStack = 0;

                if (Player.Instance.Level < 5)
                    damagePerStack = 2;
                else if (Player.Instance.Level < 9)
                    damagePerStack = 3;
                else if (Player.Instance.Level < 13)
                    damagePerStack = 4;
                else if (Player.Instance.Level < 17)
                    damagePerStack = 5;
                else if (Player.Instance.Level >= 17)
                    damagePerStack = 6;

                var time = Math.Max(0, GetDeadlyVenomBuff(target).EndTime - Game.Time);

                var final = damagePerStack*(stacks > 0 ? stacks : CountEStacks(target))*time - target.HPRegenRate*time;

                if (MenuManager.IsCacheEnabled)
                {
                    PassiveDamages.Add(new KeyValuePair<int, int>(target.NetworkId, stacks), final);
                }
                return final;
            }

            public static float GetEDamage(Obj_AI_Base unit, bool includePassive = false, int stacks = 0)
            {
                if (MenuManager.IsCacheEnabled && EDamages.Exist(new Tuple<int, bool, int>(unit.NetworkId, includePassive, stacks)))
                {
                    return EDamages.Get(new Tuple<int, bool, int>(unit.NetworkId, includePassive, stacks));
                }

                if (unit == null)
                    return 0;

                var stack = stacks > 0 ? stacks : CountEStacks(unit);

                if (stack == 0)
                    return 0;

                if (unit.GetType() != typeof(AIHeroClient))
                {
                    var damage = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical,
                        EDamage[E.Level] + stack*
                        (Player.Instance.FlatMagicDamageMod*EDamagePerStackBounsApMod +
                         Player.Instance.FlatPhysicalDamageMod*EDamagePerStackBounsAdMod +
                         EDamagePerStack[E.Level]));

                    damage = damage + (includePassive && HasDeadlyVenomBuff(unit) ? GetPassiveDamage(unit) : 0);

                    if (MenuManager.IsCacheEnabled)
                    {
                        EDamages.Add(new Tuple<int, bool, int>(unit.NetworkId, includePassive, stacks), damage);
                    }

                    return damage;
                }

                var client = (AIHeroClient) unit;

                if (client.HasSpellShield() || client.HasUndyingBuffA())
                    return 0;

                var dmg = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical,
                    EDamage[E.Level] + stack*
                    (Player.Instance.FlatMagicDamageMod*EDamagePerStackBounsApMod +
                     Player.Instance.FlatPhysicalDamageMod*EDamagePerStackBounsAdMod +
                     EDamagePerStack[E.Level]), false, true);

                dmg = dmg + (includePassive && HasDeadlyVenomBuff(unit) ? GetPassiveDamage(unit) : 0);

                if (MenuManager.IsCacheEnabled)
                {
                    EDamages.Add(new Tuple<int, bool, int>(unit.NetworkId, includePassive, stacks), dmg);
                }
                return dmg;
            }

            public static int CountEStacks(Obj_AI_Base unit)
            {
                if (MenuManager.IsCacheEnabled && EStacks.Exist(unit.NetworkId))
                {
                    return EStacks.Get(unit.NetworkId);
                }

                if (unit.IsDead || !unit.IsEnemy || ((unit.Type != GameObjectType.AIHeroClient) && (unit.Type != GameObjectType.obj_AI_Minion)))
                {
                    return 0;
                }
                
                var index = (from i in ObjectManager.Get<Obj_GeneralParticleEmitter>()
                    where
                        i.Name.Contains("Twitch_Base_P_Stack") &&
                        (i.Position.DistanceCached(unit.ServerPosition) <=
                         (unit.Type == GameObjectType.obj_AI_Minion ? 65 : 176.7768f))
                    orderby i.DistanceCached(unit)
                    select i.Name).FirstOrDefault();

                if (index == null)
                    return 0;

                int stacks;

                switch (index)
                {
                    case "Twitch_Base_P_Stack_01.troy":
                        stacks = 1;
                        break;
                    case "Twitch_Base_P_Stack_02.troy":
                        stacks = 2;
                        break;
                    case "Twitch_Base_P_Stack_03.troy":
                        stacks = 3;
                        break;
                    case "Twitch_Base_P_Stack_04.troy":
                        stacks = 4;
                        break;
                    case "Twitch_Base_P_Stack_05.troy":
                        stacks = 5;
                        break;
                    case "Twitch_Base_P_Stack_06.troy":
                        stacks = 6;
                        break;
                    default:
                        stacks = 0;
                        break;
                }

                if (MenuManager.IsCacheEnabled)
                {
                    EStacks.Add(unit.NetworkId, stacks);
                }

                return stacks;
            }
        }
    }
}
