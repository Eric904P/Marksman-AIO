#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Tristana.cs" company="EloBuddy">
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
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using EloBuddy.SDK.Rendering;
using Marksman_Master.Utils;
using Color = SharpDX.Color;


namespace Marksman_Master.Plugins.Tristana
{
    internal class Tristana : ChampionPlugin
    {
        protected static Spell.Active Q { get; }
        protected static Spell.Skillshot W { get; }
        protected static Spell.Targeted E { get; }
        protected static Spell.Targeted R { get; }

        internal static Menu ComboMenu { get; set; }
        internal static Menu LaneClearMenu { get; set; }
        internal static Menu DrawingsMenu { get; set; }

        private static readonly ColorPicker[] ColorPicker;
        private static bool _changingRangeScan;
        private static readonly Text Text;

        protected static bool IsPreAttack { get; private set; }
        protected static bool IsCatingW {get; set; }
        protected static Vector3 WStartPos { get; set; }

        protected static bool HasExplosiveChargeBuff(Obj_AI_Base unit) =>  unit.Buffs.Any(x => x.Name.ToLowerInvariant() == "tristanaechargesound");

        protected static int CountEStacks(Obj_AI_Base unit) => unit.Buffs.Any(x => x.Name.ToLowerInvariant() == "tristanaecharge") ? unit.Buffs.First(x => x.Name.ToLowerInvariant() == "tristanaecharge").Count : 0;
        
        protected static BuffInstance GetExplosiveChargeBuff(Obj_AI_Base unit) => unit.Buffs.FirstOrDefault(x => x.Name.ToLowerInvariant() == "tristanaecharge");
        
        protected static AIHeroClient WTarget { get; set; }

        private static AIHeroClient Wtarg { get; set; }
        private static bool Checkw { get; set; }

        static Tristana()
        {
            Q = new Spell.Active(SpellSlot.Q);
            W = new Spell.Skillshot(SpellSlot.W, 900, SkillShotType.Circular, 400, 1400, 150);
            E = new Spell.Targeted(SpellSlot.E, 600);
            R = new Spell.Targeted(SpellSlot.R, 600);

            ColorPicker = new ColorPicker[2];

            ColorPicker[0] = new ColorPicker("TristanaW", new ColorBGRA(243, 109, 160, 255));
            ColorPicker[1] = new ColorPicker("TristanaHpBar", new ColorBGRA(255, 134, 0, 255));
            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));

            Orbwalker.OnPreAttack += Orbwalker_OnPreAttack;

            Orbwalker.OnPostAttack += (sender, args) =>
            {
                IsPreAttack = false;
                
                if (W.IsReady() && Settings.Combo.UseW && Settings.Combo.DoubleWKeybind)
                {
                    var possibleTargets =
                        EntityManager.Heroes.Enemies.Where(
                            x => x.IsValidTarget(W.Range) && HasExplosiveChargeBuff(x) && CountEStacks(x) == 2);

                    var target = TargetSelector.GetTarget(possibleTargets, DamageType.Physical);

                    if (target != null && !target.Position.IsVectorUnderEnemyTower())
                    {
                        var buff = target.Buffs.Find(x => x.Name.ToLowerInvariant() == "tristanaechargesound").EndTime;

                        if (buff - Game.Time > Player.Instance.Distance(target)/1300 + 0.5)
                        {
                            var wPrediction = W.GetPrediction(target);

                            if (wPrediction.HitChance >= HitChance.Medium)
                            {
                                Wtarg = target;
                                Checkw = true;

                                W.Cast(wPrediction.CastPosition);

                                Core.DelayAction(() => WTarget = null, 3000);
                            }
                        }
                    }
                }
            };
            
            DamageIndicator.Initalize(ColorPicker[1].Color, 1300);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[1].OnColorChange += (a, b) => { DamageIndicator.Color = b.Color; };

            GameObject.OnCreate += GameObject_OnCreate;

            Messages.OnMessage += Messages_OnMessage;
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if (!Checkw)
                return;

            if (sender.GetType() == typeof(Obj_GeneralParticleEmitter))
            {
                var particle = sender as Obj_GeneralParticleEmitter;

                if (particle != null)
                {
                    if (particle.Name == "Tristana_Base_W_launch.troy")
                    {
                        WTarget = Wtarg;
                        Wtarg = null;
                        Checkw = false;
                    }
                }
            }
        }
        
        private static void Messages_OnMessage(Messages.WindowMessage args)
        {
            if (args.Message == WindowMessages.KeyDown)
            {
                if (args.Handle.WParam == Keybind.Keys.Item1 || args.Handle.WParam == Keybind.Keys.Item2)
                {
                    Orbwalker.ActiveModesFlags |= Orbwalker.ActiveModes.Combo;
                }
            }
            if (args.Message == WindowMessages.KeyUp)
            {
                if (args.Handle.WParam == Keybind.Keys.Item1 || args.Handle.WParam == Keybind.Keys.Item2)
                {
                    Orbwalker.ActiveModesFlags = Orbwalker.ActiveModes.None;
                }
            }
        }

        private static void Orbwalker_OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            IsPreAttack = true;
        }

        private static float HandleDamageIndicator(Obj_AI_Base unit)
        {
            if (!Settings.Drawings.DrawInfo)
            {
                return 0;
            }

            var enemy = (AIHeroClient)unit;

            if (enemy == null)
                return 0;

            var damge = 0f;

            if (R.IsReady())
                damge += Damage.GetRDamage(unit);
            if (HasExplosiveChargeBuff(unit))
                damge += Damage.GetEPhysicalDamage(unit);

            damge += Player.Instance.GetAutoAttackDamage(unit, true);

            return damge;
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(Color.White,
                    LaneClearMenu["Plugins.Tristana.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (Settings.Drawings.DrawW && (!Settings.Drawings.DrawSpellRangesWhenReady || W.IsReady()))
                Circle.Draw(ColorPicker[0].Color, W.Range, Player.Instance);

            if (!Settings.Drawings.DrawInfo)
                return;

            foreach (var source in EntityManager.Heroes.Enemies.Where(x => x.IsVisible && x.IsHPBarRendered && x.Position.IsOnScreen() && x.Buffs.Any(k => k.Name.ToLowerInvariant() == "tristanaechargesound")))
            {
                var hpPosition = source.HPBarPosition;
                hpPosition.Y = hpPosition.Y + 30; // tracker friendly.
                var timeLeft = source.Buffs.Find(x => x.Name.ToLowerInvariant() == "tristanaechargesound").EndTime - Game.Time;
                var endPos = timeLeft * 0x3e8 / 0x25;

                var degree = Misc.GetNumberInRangeFromProcent(timeLeft * 1000d / 4000d * 100d, 3, 110);
                var color = new Misc.HsvColor(degree, 1, 1).ColorFromHsv();

                Text.X = (int)(hpPosition.X + endPos);
                Text.Y = (int)hpPosition.Y + 15; // + text size 
                Text.Color = color;
                Text.TextValue = timeLeft.ToString("F1");
                Text.Draw();

                var percentDamage = Math.Min(100, Damage.GetEPhysicalDamage(source) / source.TotalHealthWithShields() * 100);

                Text.X = (int)(hpPosition.X - 50);
                Text.Y = (int)source.HPBarPosition.Y;
                Text.Color = new Misc.HsvColor(Misc.GetNumberInRangeFromProcent(percentDamage, 3, 110), 1, 1).ColorFromHsv();
                Text.TextValue = percentDamage.ToString("F1");
                Text.Draw();

                Drawing.DrawLine(hpPosition.X + endPos, hpPosition.Y, hpPosition.X, hpPosition.Y, 1, color);
            }
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
            if (!R.IsReady() || !sender.IsValidTarget(R.Range) || !Settings.Combo.UseRVsInterruptible)
                return;

            if (args.Delay == 0)
                R.Cast(sender);
            else Core.DelayAction(() => R.Cast(sender), args.Delay);
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
            if (Settings.Combo.UseWVsGapclosers && W.IsReady() && args.End.Distance(Player.Instance) < 350)
            {
                var pos =
                    SafeSpotFinder.GetSafePosition(Player.Instance.Position.To2D(), 880, 1200, 400)
                        .Where(x => x.Value <= 1)
                        .Select(x => x.Key)
                        .ToList();
                if (pos.Any())
                {
                    W.Cast(Player.Instance.Position.Extend(Misc.SortVectorsByDistanceDescending(pos, args.End.To2D())[0], 880).To3D());
                }
            }

            if (!Settings.Combo.UseRVsGapclosers || !R.IsReady() || !sender.IsValidTarget(R.Range) || args.End.Distance(Player.Instance) > 350)
                return;

            if (args.Delay == 0)
                R.Cast(sender);
            else Core.DelayAction(() => R.Cast(sender), args.Delay);
        }

        private static KeyBind Keybind { get; set; }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Tristana addon");

            ComboMenu.AddLabel("Rapid Fire (Q) settings :");
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Rocket Jump (W) settings :");
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseW", new CheckBox("Use W", false));
            ComboMenu.AddLabel("Only if W - E - R combo will kill an enemy");
            ComboMenu.AddSeparator(2);
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseWVsGapclosers", new CheckBox("Use W against gapclosers"));
            Keybind = ComboMenu.Add("Plugins.Tristana.ComboMenu.DoubleWKeybind",
                new KeyBind("Perform double W combo", false, KeyBind.BindTypes.HoldActive, 'A'));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Explosive Charge (E) settings :");
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.Add("Plugins.Tristana.ComboMenu.FocusE", new CheckBox("Focus target with E first"));
            ComboMenu.AddSeparator(2);

            ComboMenu.AddLabel("Champion's whitelist :");
            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                ComboMenu.Add("Plugins.Tristana.ComboMenu.UseEOn."+enemy.Hero, new CheckBox(enemy.Hero == Champion.MonkeyKing ? "Wukong" : enemy.ChampionName));
            }

            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Buster Shot	(R) settings :");
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseR", new CheckBox("Use R to killsteal"));
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseRVsMelees", new CheckBox("Use R against melees"));
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseRVsInterruptible", new CheckBox("Use R to interrupt"));
            ComboMenu.Add("Plugins.Tristana.ComboMenu.UseRVsGapclosers", new CheckBox("Use R against gapclosers"));
            ComboMenu.AddSeparator(5);

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Tristana addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Rapid Fire (Q) settings :");
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q in Lane Clear"));
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle Clear"));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Explosive Charge (E) settings :");
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.UseEInLaneClear", new CheckBox("Use E in Lane Clear"));
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.UseEInJungleClear", new CheckBox("Use E in Jungle Clear"));
            LaneClearMenu.Add("Plugins.Tristana.LaneClearMenu.MinManaE", new Slider("Min mana percentage ({0}%) to use E", 80, 1));

            MenuManager.BuildAntiGapcloserMenu();
            MenuManager.BuildInterrupterMenu();

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Tristana addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.Tristana.DrawingsMenu.DrawSpellRangesWhenReady",
                new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Rocket Jump (W) settings :");
            DrawingsMenu.Add("Plugins.Tristana.DrawingsMenu.DrawW", new CheckBox("Draw W range"));
            DrawingsMenu.Add("Plugins.Tristana.DrawingsMenu.DrawWColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.Add("Plugins.Tristana.DrawingsMenu.DrawInfo", new CheckBox("Draw Infos")).OnValueChange += (a, b) =>
            {
                if (b.NewValue)
                    DamageIndicator.DamageDelegate = HandleDamageIndicator;
                else if (!b.NewValue)
                    DamageIndicator.DamageDelegate = null;
            };
            DrawingsMenu.Add("Plugins.Tristana.DrawingsMenu.InfoColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(System.Drawing.Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddLabel("Draws damage indicator and buff duration");
        }

        protected override void PermaActive()
        {
            E.Range = (uint)(630 + 7 * Player.Instance.Level);
            R.Range = (uint)(630 + 7 * Player.Instance.Level);
            
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
                public static bool UseQ => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseQ"];

                public static bool UseW => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseW"];

                public static bool UseWVsGapclosers => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseWVsGapclosers"];

                public static bool DoubleWKeybind => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.DoubleWKeybind"];

                public static bool UseE => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseE"];

                public static bool FocusE => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.FocusE"];

                public static bool UseR => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseR"];

                public static bool UseRVsMelees => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseRVsMelees"];

                public static bool UseRVsInterruptible => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseRVsInterruptible"];

                public static bool UseRVsGapclosers => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseRVsGapclosers"];

                public static bool IsEnabledFor(AIHeroClient unit) => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseEOn." + unit.Hero];

                public static bool IsEnabledFor(string championName) => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseEOn." + championName];

                public static bool IsEnabledFor(Champion championName) => MenuManager.MenuValues["Plugins.Tristana.ComboMenu.UseEOn." + championName];
            }
            
            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.EnableLCIfNoEn"];

                public static int ScanRange => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.ScanRange", true];

                public static int AllowedEnemies => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.AllowedEnemies", true];

                public static bool UseQInLaneClear => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.UseQInLaneClear"];

                public static bool UseQInJungleClear => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.UseQInJungleClear"];

                public static bool UseEInLaneClear => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.UseEInLaneClear"];

                public static bool UseEInJungleClear => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.UseEInJungleClear"];

                public static int MinManaE => MenuManager.MenuValues["Plugins.Tristana.LaneClearMenu.MinManaE", true];
            }

            internal static class Drawings
            {
                public static bool DrawSpellRangesWhenReady => MenuManager.MenuValues["Plugins.Tristana.DrawingsMenu.DrawSpellRangesWhenReady"];

                public static bool DrawW => MenuManager.MenuValues["Plugins.Tristana.DrawingsMenu.DrawW"];

                public static bool DrawInfo => MenuManager.MenuValues["Plugins.Tristana.DrawingsMenu.DrawInfo"];
            }
        }

        protected static class Damage
        {
            public static int[] EMagicDamage { get; } = {0, 50, 75, 100, 125, 150};
            public static float EMagicDamageApMod { get; } = 0.25f;
            public static int[] EPhysicalDamage { get; } = {0, 60, 70, 80, 90, 100};
            public static float[] EPhysicalDamageBonusAdMod { get; } = {0, 0.5f, 0.65f, 0.8f, 0.95f, 1.1f};
            public static float EPhysicalDamageBonusApMod { get; } = 0.5f;
            public static int[] EDamagePerStack { get; } = {0, 18, 21, 24, 27, 30};
            public static float[] EDamagePerStackBonusAdMod { get; } = {0, 0.15f, 0.195f, 0.24f, 0.285f, 0.33f};
            public static float EDamagePerStackBonusApMod { get; } = 0.15f;
            public static int[] RDamage { get; } = {0, 300, 400, 500};

            public static float GetEMagicDamage(Obj_AI_Base unit)
            {
                return Player.Instance.CalculateDamageOnUnit(unit, DamageType.Magical, EMagicDamage[E.Level] + Player.Instance.FlatMagicDamageMod * EMagicDamageApMod);
            }

            public static float GetEPhysicalDamage(Obj_AI_Base unit, int customStacks = -1)
            {
                var rawDamage = (EPhysicalDamage[E.Level] +
                                 (Player.Instance.FlatPhysicalDamageMod*EPhysicalDamageBonusAdMod[E.Level] +
                                  Player.Instance.FlatMagicDamageMod*EPhysicalDamageBonusApMod))
                                +
                                (EDamagePerStack[E.Level] +
                                 (Player.Instance.FlatPhysicalDamageMod*EDamagePerStackBonusAdMod[E.Level] +
                                  Player.Instance.FlatMagicDamageMod*EDamagePerStackBonusApMod))*
                                (customStacks > 0
                                    ? customStacks
                                    : (unit.Buffs.Any(x => x.Name.ToLowerInvariant() == "tristanaecharge")
                                        ? unit.Buffs.Find(x => x.Name.ToLowerInvariant() == "tristanaecharge").Count
                                        : 0));

                var damage = Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical, rawDamage);

                return damage;
            }

            public static float GetRDamage(Obj_AI_Base unit)
            {
                return Player.Instance.GetSpellDamage(unit, SpellSlot.R);
            }

            public static bool IsTargetKillableFromR(Obj_AI_Base unit)
            {
                if (unit.GetType() != typeof(AIHeroClient))
                {
                    return unit.TotalHealthWithShields() <= GetRDamage(unit);
                }

                var enemy = (AIHeroClient)unit;

                if (enemy.HasSpellShield() || enemy.HasUndyingBuffA())
                    return false;

                if (enemy.ChampionName != "Blitzcrank")
                    return enemy.TotalHealthWithShields(true) < GetRDamage(enemy);

                if (!enemy.HasBuff("BlitzcrankManaBarrierCD") && !enemy.HasBuff("ManaBarrier"))
                {
                    return enemy.TotalHealthWithShields(true) + enemy.Mana / 2 < GetRDamage(enemy);
                }

                return enemy.TotalHealthWithShields(true) < GetRDamage(enemy);
            }
        }
    }
}