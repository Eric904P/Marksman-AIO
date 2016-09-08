 #region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="Caitlyn.cs" company="EloBuddy">
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
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Rendering;
using SharpDX;
using System.Drawing;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Utils;
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Caitlyn
{
    internal class Caitlyn : ChampionPlugin
    {
        protected static Spell.Skillshot Q { get; }
        protected static Spell.Skillshot W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Targeted R { get; }

        protected static Menu ComboMenu { get; set; }
        protected static Menu HarassMenu { get; set; }
        protected static Menu LaneClearMenu { get; set; }
        protected static Menu DrawingsMenu { get; set; }
        protected static Menu MiscMenu { get; set; }

        private static ColorPicker[] ColorPicker { get; }

        private static bool _changingRangeScan;

        protected static bool IsUnitNetted(AIHeroClient unit) => unit.Buffs.Any(x => x.Name == "caitlynyordletrapinternal");
        protected static bool IsUnitImmobilizedByTrap(AIHeroClient unit) => unit.Buffs.Any(x => x.Name == "caitlynyordletrapdebuff");
        protected static bool HasAutoAttackRangeBuff => Player.Instance.Buffs.Any(x => x.Name == "caitlynheadshotrangecheck");
        protected static bool HasAutoAttackRangeBuffOnChamp => Player.Instance.Buffs.Any(x => x.Name == "caitlynheadshotrangecheck") && EntityManager.Heroes.Enemies.Any(x=> x.IsValidTarget(1350) && IsUnitNetted(x));

        private static readonly Text Text;

        private static readonly Dictionary<int, Dictionary<float, float>> Damages =
            new Dictionary<int, Dictionary<float, float>>();

        static Caitlyn()
        {
            Q = new Spell.Skillshot(SpellSlot.Q, 1250, SkillShotType.Linear, 700, 2000, 70);
            W = new Spell.Skillshot(SpellSlot.W, 800, SkillShotType.Circular, 1400)
            {
                Width = 20
            };
            E = new Spell.Skillshot(SpellSlot.E, 900, SkillShotType.Linear, 250, 2000, 70);
            R = new Spell.Targeted(SpellSlot.R, 2000);

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("CaitlynQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("CaitlynE", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("CaitlynR", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("CaitlynHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color, (int)R.Range);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[3].OnColorChange +=
                (a, b) =>
                {
                    DamageIndicator.Color = b.Color;
                };

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));

            ChampionTracker.OnLongSpellCast += ChampionTracker_OnLongSpellCast;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        private static float _lastWCastTime;

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.Slot == SpellSlot.W)
            {
                _lastWCastTime = Game.Time * 1000;
            }
        }
        
        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (args.Slot == SpellSlot.W &&
                (GetTrapsInRange(args.EndPosition, 200).Any() || Game.Time*1000 - _lastWCastTime < 2000))
            {
                args.Process = false;
            }
        }

        private static void ChampionTracker_OnLongSpellCast(object sender, OnLongSpellCastEventArgs e)
        {
            if (!W.IsReady() || !Settings.Combo.UseWOnImmobile)
                return;

            if (e.IsTeleport && W.IsInRange(e.EndPosition))
            {
                W.Cast(e.EndPosition);
            }
            else if(e.Sender.IsValidTarget(W.Range))
            {
                W.Cast(e.Sender.ServerPosition);
            }
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
            if (Damages.ContainsKey(unit.NetworkId) &&
                !Damages.Any(x => x.Key == unit.NetworkId && x.Value.Any(k => Game.Time*1000 - k.Key > 200))) //
                return Damages[unit.NetworkId].Values.FirstOrDefault();
            
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
            
            if (!Damages.ContainsKey(unit.NetworkId))
            {
                Damages.Add(unit.NetworkId, new Dictionary<float, float> { { Game.Time * 1000, damage } });
            }
            else
            {
                Damages[unit.NetworkId] = new Dictionary<float, float> { { Game.Time * 1000, damage } };
            }

            return damage;
        }


        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(SharpDX.Color.White,
                    LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (Settings.Drawings.DrawQ && (!Settings.Drawings.DrawSpellRangesWhenReady || Q.IsReady()))
                Circle.Draw(ColorPicker[0].Color, Q.Range, Player.Instance);
            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[1].Color, E.Range, Player.Instance);
            if (Settings.Drawings.DrawR && (!Settings.Drawings.DrawSpellRangesWhenReady || R.IsReady()))
                Circle.Draw(ColorPicker[2].Color, R.Range, Player.Instance);

            if (!Settings.Drawings.DrawDamageIndicator || !R.IsReady())
                return;

            foreach (var source in EntityManager.Heroes.Enemies.Where(
                x => x.IsHPBarRendered && x.IsInRange(Player.Instance, R.Range)))
            {
                var hpPosition = source.HPBarPosition;
                hpPosition.Y = hpPosition.Y + 30;
                var percentDamage = Math.Min(100, Player.Instance.GetSpellDamage(source, SpellSlot.R) / source.TotalHealthWithShields() * 100);

                Text.X = (int) (hpPosition.X - 50);
                Text.Y = (int) source.HPBarPosition.Y;
                Text.Color =
                    new Misc.HsvColor(Misc.GetNumberInRangeFromProcent(percentDamage, 3, 110), 1, 1).ColorFromHsv();
                Text.TextValue = percentDamage.ToString("F1");
                Text.Draw();
            }
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
            if (Settings.Misc.WAgainstGapclosers && W.IsReady() && W.IsInRange(args.End))
            {
                W.Cast(args.End);
            }
        }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Caitlyn addon");

            ComboMenu.AddLabel("Piltover Peacemaker	(Q) settings :");
            ComboMenu.Add("Plugins.Caitlyn.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Yordle Snap Trap (W) settings :");
            ComboMenu.Add("Plugins.Caitlyn.ComboMenu.UseW", new CheckBox("Use W"));
            ComboMenu.Add("Plugins.Caitlyn.ComboMenu.UseWOnImmobile", new CheckBox("Use W on immobile"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("90 Caliber Net (E) settings :");
            ComboMenu.Add("Plugins.Caitlyn.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Ace in the Hole (R) settings :");
            ComboMenu.Add("Plugins.Caitlyn.ComboMenu.UseR", new CheckBox("Use R", false));

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Caitlyn addon");

            HarassMenu.AddLabel("Piltover Peacemaker (Q) settings :");
            HarassMenu.Add("Plugins.Caitlyn.HarassMenu.UseQ", new CheckBox("Use Q", false));
            HarassMenu.Add("Plugins.Caitlyn.HarassMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 75, 1));

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Caitlyn addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby", false));
            var scanRange = LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Piltover Peacemaker (Q) settings :");
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q in Lane clear"));
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ", new Slider("Min minions killed to use Q", 3, 1, 6));
            LaneClearMenu.AddSeparator(5);
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle clear"));
            LaneClearMenu.Add("Plugins.Caitlyn.LaneClearMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 50, 1));

            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Caitlyn addon");
            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.Caitlyn.MiscMenu.EnableKillsteal", new CheckBox("Enable Killsteal"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Yordle Snap Trap (W) settings :");
            MiscMenu.Add("Plugins.Caitlyn.MiscMenu.WAgainstGapclosers", new CheckBox("Use W against gapclosers"));

            MenuManager.BuildAntiGapcloserMenu();

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Caitlyn addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawSpellRangesWhenReady", new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Piltover Peacemaker (Q) settings :");
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawQ", new CheckBox("Draw Q range"));
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawQColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("90 Caliber Net (E) settings :");
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawE", new CheckBox("Draw E range", false));
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawEColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);
            
            DrawingsMenu.AddLabel("Ace in the Hole (R) settings :");
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawR", new CheckBox("Draw R range", false));
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawRColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[2].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };

            DrawingsMenu.AddLabel("Damage indicator settings :");
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DrawDamageIndicator", new CheckBox("Draw damage indicator")).OnValueChange += (a, b) =>
            {
                if (b.NewValue)
                    DamageIndicator.DamageDelegate = HandleDamageIndicator;
                else if (!b.NewValue)
                    DamageIndicator.DamageDelegate = null;
            };
            DrawingsMenu.Add("Plugins.Caitlyn.DrawingsMenu.DamageIndicatorColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[3].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
        }
        
        protected static IEnumerable<Obj_GeneralParticleEmitter> GetTrapsInRange(Vector3 position, float range)
        {
            return
                ObjectManager.Get<Obj_GeneralParticleEmitter>()
                    .Where(
                        x => x.Name == "Caitlyn_Base_W_Indicator_SizeRing.troy" && x.Position.Distance(position) < range);
        }
        
        protected static Vector3 GetDashEndPosition(Vector3 castPosition)
        {
            return Player.Instance.Position.Extend(castPosition, -400).To3D();
        }

        protected override void PermaActive()
        {
            R.Range = 2000 + (uint)(500*(R.Level - 1));
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
                        return ComboMenu?["Plugins.Caitlyn.ComboMenu.UseQ"] != null &&
                               ComboMenu["Plugins.Caitlyn.ComboMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Caitlyn.ComboMenu.UseQ"] != null)
                            ComboMenu["Plugins.Caitlyn.ComboMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseW
                {
                    get
                    {
                        return ComboMenu?["Plugins.Caitlyn.ComboMenu.UseW"] != null &&
                               ComboMenu["Plugins.Caitlyn.ComboMenu.UseW"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Caitlyn.ComboMenu.UseW"] != null)
                            ComboMenu["Plugins.Caitlyn.ComboMenu.UseW"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseWOnImmobile
                {
                    get
                    {
                        return ComboMenu?["Plugins.Caitlyn.ComboMenu.UseWOnImmobile"] != null &&
                               ComboMenu["Plugins.Caitlyn.ComboMenu.UseWOnImmobile"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Caitlyn.ComboMenu.UseWOnImmobile"] != null)
                            ComboMenu["Plugins.Caitlyn.ComboMenu.UseWOnImmobile"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseE
                {
                    get
                    {
                        return ComboMenu?["Plugins.Caitlyn.ComboMenu.UseE"] != null &&
                               ComboMenu["Plugins.Caitlyn.ComboMenu.UseE"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Caitlyn.ComboMenu.UseE"] != null)
                            ComboMenu["Plugins.Caitlyn.ComboMenu.UseE"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseR
                {
                    get
                    {
                        return ComboMenu?["Plugins.Caitlyn.ComboMenu.UseR"] != null &&
                               ComboMenu["Plugins.Caitlyn.ComboMenu.UseR"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.Caitlyn.ComboMenu.UseR"] != null)
                            ComboMenu["Plugins.Caitlyn.ComboMenu.UseR"].Cast<CheckBox>()
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
                        return HarassMenu?["Plugins.Caitlyn.HarassMenu.UseQ"] != null &&
                               HarassMenu["Plugins.Caitlyn.HarassMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Caitlyn.HarassMenu.UseQ"] != null)
                            HarassMenu["Plugins.Caitlyn.HarassMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (HarassMenu?["Plugins.Caitlyn.HarassMenu.MinManaQ"] != null)
                            return HarassMenu["Plugins.Caitlyn.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Caitlyn.HarassMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.Caitlyn.HarassMenu.MinManaQ"] != null)
                            HarassMenu["Plugins.Caitlyn.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.EnableLCIfNoEn"] != null &&
                               LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.EnableLCIfNoEn"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int ScanRange
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.ScanRange"] != null)
                            return LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Caitlyn.LaneClearMenu.ScanRange menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.ScanRange"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static int AllowedEnemies
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.AllowedEnemies"] != null)
                            return
                                LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Caitlyn.LaneClearMenu.AllowedEnemies menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.AllowedEnemies"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseQInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.UseQInLaneClear"] != null &&
                               LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.UseQInLaneClear"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseQInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.UseQInJungleClear"] != null &&
                               LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.UseQInJungleClear"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinMinionsKilledForQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ"] != null)
                            return LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.MinMinionsKilledForQ"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.MinManaQ"] != null)
                            return LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.Caitlyn.LaneClearMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.Caitlyn.LaneClearMenu.MinManaQ"] != null)
                            LaneClearMenu["Plugins.Caitlyn.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class Misc
            {
                public static bool EnableKillsteal
                {
                    get
                    {
                        return MiscMenu?["Plugins.Caitlyn.MiscMenu.EnableKillsteal"] != null &&
                               MiscMenu["Plugins.Caitlyn.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Caitlyn.MiscMenu.EnableKillsteal"] != null)
                            MiscMenu["Plugins.Caitlyn.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool WAgainstGapclosers
                {
                    get
                    {
                        return MiscMenu?["Plugins.Caitlyn.MiscMenu.WAgainstGapclosers"] != null &&
                               MiscMenu["Plugins.Caitlyn.MiscMenu.WAgainstGapclosers"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.Caitlyn.MiscMenu.WAgainstGapclosers"] != null)
                            MiscMenu["Plugins.Caitlyn.MiscMenu.WAgainstGapclosers"].Cast<CheckBox>()
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
                        return DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawSpellRangesWhenReady"] != null &&
                               DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawSpellRangesWhenReady"] != null)
                            DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool DrawQ
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawQ"] != null &&
                               DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawQ"] != null)
                            DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawE
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawE"] != null &&
                               DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawE"] != null)
                            DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawR
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawR"] != null &&
                               DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawR"] != null)
                            DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawDamageIndicator
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawDamageIndicator"] != null &&
                               DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.Caitlyn.DrawingsMenu.DrawDamageIndicator"] != null)
                            DrawingsMenu["Plugins.Caitlyn.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue = value;
                    }
                }
            }
        }

        protected internal static class Damage
        {
            private static readonly Dictionary<int, Dictionary<float, float>> HeadShotDamages =
                new Dictionary<int, Dictionary<float, float>>();

            private static readonly Dictionary<int, Dictionary<float, float>> RDamages =
                new Dictionary<int, Dictionary<float, float>>();

            public static float GetHeadShotDamage(AIHeroClient unit)
            {
                if (HeadShotDamages.ContainsKey(unit.NetworkId) &&
                    !HeadShotDamages.Any(x => x.Key == unit.NetworkId && x.Value.Any(k => Game.Time*1000 - k.Key > 200)))
                    return HeadShotDamages[unit.NetworkId].Values.FirstOrDefault();

                var damage = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical, Player.Instance.TotalAttackDamage * (1 + (0.5f + Player.Instance.FlatCritChanceMod * (1 + 0.5f * (Player.Instance.HasItem(ItemId.Infinity_Edge) ? 0.5f : 0)))), false, true) + (IsUnitImmobilizedByTrap(unit) ? GetTrapAdditionalHeadShotDamage(unit) : 0);

                if (!HeadShotDamages.ContainsKey(unit.NetworkId))
                {
                    HeadShotDamages.Add(unit.NetworkId, new Dictionary<float, float> { { Game.Time * 1000, damage } });
                }
                else
                {
                    HeadShotDamages[unit.NetworkId] = new Dictionary<float, float> { { Game.Time * 1000, damage } };
                }

                return damage;
            }

            public static float GetTrapAdditionalHeadShotDamage(AIHeroClient unit)
            {
                if (RDamages.ContainsKey(unit.NetworkId) &&
                    !RDamages.Any(x => x.Key == unit.NetworkId && x.Value.Any(k => Game.Time*1000 - k.Key > 200)))
                    return RDamages[unit.NetworkId].Values.FirstOrDefault();

                int[] additionalDamage = {0, 30, 70, 110, 150, 190};

                var damage = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical, additionalDamage[W.Level] + Player.Instance.TotalAttackDamage * 0.7f);

                if (!RDamages.ContainsKey(unit.NetworkId))
                {
                    RDamages.Add(unit.NetworkId, new Dictionary<float, float> { { Game.Time * 1000, damage } });
                }
                else
                {
                    RDamages[unit.NetworkId] = new Dictionary<float, float> { { Game.Time * 1000, damage } };
                }

                return damage;
            }
        }
    }
}