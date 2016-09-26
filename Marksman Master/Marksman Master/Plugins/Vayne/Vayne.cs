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
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using Marksman_Master.Cache.Modules;
using Marksman_Master.PermaShow.Values;
using Marksman_Master.Utils;
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

        protected static bool IsPostAttack { get; private set; }
        
        static Vayne()
        {
            Q = new Spell.Skillshot(SpellSlot.Q, 300, SkillShotType.Linear);
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Targeted(SpellSlot.E, 765);
            R = new Spell.Active(SpellSlot.R);
            
            Orbwalker.OnPostAttack += Orbwalker_OnPostAttack;
            Orbwalker.OnPreAttack += Orbwalker_OnPreAttack;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Game.OnPostTick += args => IsPostAttack = false;

            if (EntityManager.Heroes.Enemies.Any(client => client.Hero == Champion.Rengar))
            {
                GameObject.OnCreate += Obj_AI_Base_OnCreate;
            }

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));
        }

        private static void Orbwalker_OnPostAttack(AttackableUnit target, EventArgs args)
        {
            IsPostAttack = true;

            if (target == null || target.GetType() != typeof(AIHeroClient) || !Settings.Misc.EKs || !target.IsValidTargetCached(E.Range))
                return;

            var enemy = (AIHeroClient)target;

            if (HasSilverDebuff(enemy) && GetSilverDebuff(enemy).Count == 1)
            {
                Core.DelayAction(() =>
                {
                    if (Damage.IsKillableFromSilverEAndAuto(enemy) && enemy.TotalHealthWithShields() - IncomingDamage.GetIncomingDamage(enemy) > 0)
                    {
                        Misc.PrintDebugMessage("casting e to ks");
                        E.Cast(enemy);
                    }}, 40 + Game.Ping / 2);
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (!Settings.Misc.NoAaWhileStealth || !HasInquisitionBuff)
                return;

            if (args.Slot == SpellSlot.Q)
            {
                _lastQCastTime = Game.Time*1000;
            }
        }

        private static void Obj_AI_Base_OnCreate(GameObject sender, EventArgs args)
        {
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
            if (!HasInquisitionBuff || !Settings.Misc.NoAaWhileStealth ||
                !(Game.Time*1000 - _lastQCastTime < Settings.Misc.NoAaDelay))
                return;

            var client = target as AIHeroClient;

            if (client != null && client.Health > Player.Instance.GetAutoAttackDamageCached(client, true)*3)
            {
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

            Misc.PrintInfoMessage("Interrupting " + sender.ChampionName + "'s " + args.SpellName);

            Misc.PrintDebugMessage($"[DEBUG] OnInterruptible | Champion : {sender.ChampionName} | SpellSlot : {args.SpellSlot}");
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
            if (E.IsReady() && sender.IsValidTargetCached(E.Range) && args.End.DistanceCached(Player.Instance.Position) < 500)
            {
                if (args.Delay == 0)
                    E.Cast(sender);
                else Core.DelayAction(() => E.Cast(sender), args.Delay);

                Misc.PrintDebugMessage($"[DEBUG] OnGapcloser | Champion : {sender.ChampionName} | SpellSlot : {args.SpellSlot}");
            }
        }

        public static bool WillEStun(Obj_AI_Base target)
        {
            if (target == null || !IsECastableOnEnemy(target))
                return false;

            var pushDistance = Settings.Misc.PushDistance;
            var eta = target.DistanceCached(Player.Instance) / 2000;
            var prediction = Prediction.Position.PredictLinearMissile(target, E.Range, 40, 350, 2000, int.MaxValue, Player.Instance.Position, true);
            var position = Prediction.Position.PredictUnitPosition(target, (int)(eta*1000 + 150));

            if (target.GetMovementBlockedDebuffDuration() > eta + 0.25f)
            {
                for (var i = 25; i < pushDistance + 50; i += 50)
                {
                    if (target.ServerPosition.Extend(Player.Instance.ServerPosition, -Math.Min(i, pushDistance)).IsWall())
                    {
                        return true;
                    }
                }
            }

            if (prediction.HitChancePercent < Settings.Misc.EHitchance)
                return false;

            for (var i = pushDistance; i >= 100; i -= 100)
            {
                var vec = position.Extend(Player.Instance.ServerPosition, -i);
                var polygon = new Geometry.Polygon.Circle(vec, target.BoundingRadius, 100);

                if (polygon.Points.Count(x => x.IsWall()) >= Settings.Misc.EHitchance)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsECastableOnEnemy(Obj_AI_Base unit)
        {
            return E.IsReady() && unit.IsValidTargetCached(E.Range) && !unit.IsZombie &&
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
        }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Vayne addon");

            ComboMenu.AddLabel("Tumble (Q) settings :");
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseQOnlyToProcW", new CheckBox("Use Q only to proc W stacks", false));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Condemn (E) settings :");
            ComboMenu.Add("Plugins.Vayne.ComboMenu.UseE", new CheckBox("Use E"));
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
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.AllowedEnemies",
                new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Tumble (Q) settings :");
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.UseQToLaneClear", new CheckBox("Use Q to lane clear"));
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.UseQToJungleClear", new CheckBox("Use Q to jungle clear"));
            LaneClearMenu.Add("Plugins.Vayne.LaneClearMenu.MinMana",
                new Slider("Min mana percentage ({0}%) to use Q", 80, 1));
            LaneClearMenu.AddSeparator(5);

            MenuManager.BuildAntiGapcloserMenu();
            MenuManager.BuildInterrupterMenu();

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
            MiscMenu.Add("Plugins.Vayne.MiscMenu.PushDistance", new Slider("Push distance", 420, 400, 455));
            MiscMenu.Add("Plugins.Vayne.MiscMenu.EHitchance", new Slider("Condemn hitchance : {0}%", 50, 1));
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
                public static bool UseQ => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseQ"];
                
                public static bool UseQOnlyToProcW => MenuManager.MenuValues["Plugins.Vayne.ComboMenu.UseQOnlyToProcW"];

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
    }
}