#region Licensing
// ---------------------------------------------------------------------
// <copyright file="MissFortune.cs" company="EloBuddy">
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
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;
using EloBuddy.SDK.Utils;
using Marksman_Master.PermaShow.Values;
using Marksman_Master.Utils;
using Color = System.Drawing.Color;

namespace Marksman_Master.Plugins.MissFortune
{
    internal class MissFortune : ChampionPlugin
    {
        protected static Spell.Targeted Q { get; }
        protected static Spell.Active W { get; }
        protected static Spell.Skillshot E { get; }
        protected static Spell.Skillshot R { get; }

        protected static Menu ComboMenu { get; set; }
        protected static Menu HarassMenu { get; set; }
        protected static Menu LaneClearMenu { get; set; }
        protected static Menu DrawingsMenu { get; set; }
        protected static Menu MiscMenu { get; set; }

        private static BoolItem AutoHarassItem { get; set; }

        private static ColorPicker[] ColorPicker { get; }

        private static bool _changingRangeScan;
        
        private static readonly Dictionary<int, Dictionary<float, float>> Damages =
            new Dictionary<int, Dictionary<float, float>>();

        protected static byte[] QMana { get; } = {0, 43, 46, 49, 52, 55};
        protected static byte WMana { get; } = 30;
        protected static byte EMana { get; } = 80;
        protected static byte RMana { get; } = 100;
        protected static byte[] RWaves { get; } = { 0, 12, 14, 16 };

        protected static bool IsAfterAttack { get; private set; }
        protected static bool IsPreAttack { get; private set; }
        protected static bool RCasted { get; private set; }
        protected static float RCastTime { get; private set; }

        protected static bool HasLoveTap(Obj_AI_Base unit)
            =>
                ObjectManager.Get<Obj_GeneralParticleEmitter>()
                    .Any(x => x.Name == "MissFortune_Base_P_Mark.troy" && Math.Abs(x.Distance(unit)) < 0.01);

        protected static bool HasWBuff => Player.Instance.Buffs.Any(x => x.Name.ToLower() == "missfortuneviciousstrikes");

        static MissFortune()
        {
            Q = new Spell.Targeted(SpellSlot.Q, 720);
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Skillshot(SpellSlot.E, 1000, SkillShotType.Circular)
            {
                Width = 350
            };
            R = new Spell.Skillshot(SpellSlot.R, 1400, SkillShotType.Cone)
            {
                Width = (int)Math.PI / 180 * 35
            };

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("MissFortuneQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("MissFortuneE", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("MissFortuneR", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("MissFortuneHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(ColorPicker[3].Color, (int)R.Range);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[3].OnColorChange +=
                (a, b) =>
                {
                    DamageIndicator.Color = b.Color;
                };

            Orbwalker.OnPostAttack += (sender, args) =>
            {
                IsAfterAttack = true;
                IsPreAttack = false;
            };

            Orbwalker.OnPreAttack += (target, args) => IsPreAttack = true;
            Game.OnPostTick += args => { IsAfterAttack = false;};
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Player.OnIssueOrder += Player_OnIssueOrder;

            Obj_AI_Base.OnPlayAnimation += Obj_AI_Base_OnPlayAnimation;
        }

        private static void Obj_AI_Base_OnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if (!sender.IsMe || !Settings.Combo.RBlockMovement)
                return;

            if (args.Animation == "Spell4")
            {
                Orbwalker.DisableAttacking = true;
                Orbwalker.DisableMovement = true;
            }
        }

        private static void Player_OnIssueOrder(Obj_AI_Base sender, PlayerIssueOrderEventArgs args)
        {
            if (!sender.IsMe || !Settings.Combo.RBlockMovement)
                return;

            if (RCasted && Game.Time * 1000 - RCastTime < 1000)
            {
                args.Process = false;
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (!Settings.Combo.RBlockMovement)
                return;

            if (args.Slot == SpellSlot.R)
            {
                Orbwalker.DisableAttacking = true;
                Orbwalker.DisableMovement = true;
            }

            if (Settings.Combo.RBlockMovement && RCasted && Player.Instance.Spellbook.IsChanneling)
            {
                args.Process = false;
            }
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Settings.Combo.RBlockMovement)
                return;

            if (sender.IsMe && (args.Slot == SpellSlot.R || args.SData.Name == "MissFortuneBulletTime"))
            {
                Orbwalker.DisableAttacking = true;
                Orbwalker.DisableMovement = true;

                RCasted = true;
                RCastTime = Game.Time * 1000;
            }
        }

        protected static IEnumerable<T> GetObjectsWithinQBounceRange<T>(Vector3 position) where T : Obj_AI_Base
        {
            var qPolygon = new Geometry.Polygon.Sector(position,
                Player.Instance.Position.Extend(position, position.Distance(Player.Instance) + 400).To3D(),
                (float) Math.PI/180f*55f,
                400);

            if (typeof (T) == typeof (AIHeroClient))
            {
                return (IEnumerable<T>) EntityManager.Heroes.Enemies.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => qPolygon.IsInside(k)));
            }
            if (typeof (T) == typeof (Obj_AI_Base))
            {
                return (IEnumerable<T>) EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => qPolygon.IsInside(k))).Cast<Obj_AI_Base>()
                    .Concat(EntityManager.Heroes.Enemies.Where(
                        unit =>
                            new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                                k => qPolygon.IsInside(k))));
            }
            if (typeof (T) == typeof (Obj_AI_Minion))
            {
                return (IEnumerable<T>) EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => qPolygon.IsInside(k)));
            }
            return null;
        }

        protected static IEnumerable<T> GetObjectsWithinRRange<T>(Vector3 position) where T : Obj_AI_Base
        {
            var rPolygon = new Geometry.Polygon.Sector(Player.Instance.Position, position, (float)Math.PI / 180f * 35f,
                1280);

            if (typeof(T) == typeof(AIHeroClient))
            {
                return (IEnumerable<T>)EntityManager.Heroes.Enemies.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => rPolygon.IsInside(k)));
            }
            if (typeof(T) == typeof(Obj_AI_Base))
            {
                return (IEnumerable<T>)EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => rPolygon.IsInside(k))).Cast<Obj_AI_Base>()
                    .Concat(EntityManager.Heroes.Enemies.Where(
                        unit =>
                            new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                                k => rPolygon.IsInside(k))));
            }
            if (typeof(T) == typeof(Obj_AI_Minion))
            {
                return (IEnumerable<T>)EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                    unit =>
                        new Geometry.Polygon.Circle(unit.Position, unit.BoundingRadius - 15).Points.Any(
                            k => rPolygon.IsInside(k)));
            }
            return null;
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
                !Damages.Any(x => x.Key == unit.NetworkId && x.Value.Any(k => Game.Time * 1000 - k.Key > 200))) //
                return Damages[unit.NetworkId].Values.FirstOrDefault();

            var damage = 0f;

            if (unit.IsValidTarget(Q.Range) && Q.IsReady())
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.Q);
            
            if (unit.IsValidTarget(E.Range) && E.IsReady())
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.E);

            if (unit.IsValidTarget(R.Range) && R.IsReady())
                damage += Player.Instance.GetSpellDamage(unit, SpellSlot.R) * 6;

            if (Player.Instance.IsInAutoAttackRange(unit))
                damage += Player.Instance.GetAutoAttackDamage(unit);

            Damages[unit.NetworkId] = new Dictionary<float, float> {{Game.Time*1000, damage}};

            return damage;
        }

        protected static Obj_AI_Base GetQMinion(AIHeroClient target)
        {
            if (!EntityManager.MinionsAndMonsters.CombinedAttackable.Any(
                x => x.IsValidTarget(Q.Range) && x.Distance(target) <= 400))
                return null;

            return
                (from minion in
                    EntityManager.MinionsAndMonsters.CombinedAttackable.Where(
                        x =>
                            x.IsValidTarget(Q.Range) && !x.IsMoving && x.Distance(target) <= 400 &&
                            Prediction.Health.GetPrediction(x, 500) > 20 &&
                            Prediction.Health.GetPrediction(x, 500) < Player.Instance.GetSpellDamage(x, SpellSlot.Q))
                    let closest = GetQBouncePossibleObject(minion)
                    where
                        closest != null && closest.Type == GameObjectType.AIHeroClient &&
                        closest.NetworkId == target.NetworkId
                    select minion).FirstOrDefault();
        }

        protected static Obj_AI_Base GetQBouncePossibleObject(Obj_AI_Base from)
        {
            var qobjects = GetObjectsWithinQBounceRange<Obj_AI_Base>(from.Position);

            foreach (var objAiBase in qobjects.OrderBy(x=>x.Distance(@from)).Where(objAiBase => @from.NetworkId != objAiBase.NetworkId))
            {
                if (objAiBase.GetType() == typeof(AIHeroClient) && HasLoveTap(objAiBase) && new Geometry.Polygon.Circle(objAiBase.Position,
                    objAiBase.BoundingRadius - 15).Points.Any(k => new Geometry.Polygon.Sector(objAiBase.Position,
                          Player.Instance.Position.Extend(objAiBase, objAiBase.Distance(Player.Instance) + 400).To3D(),
                          (float)Math.PI / 180f * 40f, 400).IsInside(k)))
                {
                    return objAiBase;
                }


                if (objAiBase.GetType() == typeof (Obj_AI_Minion) &&
                    new Geometry.Polygon.Circle(objAiBase.Position, objAiBase.BoundingRadius - 15).Points.Any(
                        k =>
                            new Geometry.Polygon.Sector(objAiBase.Position,
                                Player.Instance.Position.Extend(objAiBase, objAiBase.Distance(Player.Instance) + 400)
                                    .To3D(), (float) Math.PI/180f*20f, 400).IsInside(k)))
                {
                    return objAiBase;
                }
                if (objAiBase.GetType() == typeof (AIHeroClient) &&
                    new Geometry.Polygon.Circle(objAiBase.Position, objAiBase.BoundingRadius - 15).Points.Any(
                        k =>
                            new Geometry.Polygon.Sector(objAiBase.Position,
                                Player.Instance.Position.Extend(objAiBase, objAiBase.Distance(Player.Instance) + 400)
                                    .To3D(), (float) Math.PI/180f*20f, 400).IsInside(k)))
                {
                    return objAiBase;
                }
                if (objAiBase.GetType() == typeof (Obj_AI_Minion) &&
                    new Geometry.Polygon.Circle(objAiBase.Position, objAiBase.BoundingRadius - 15).Points.Any(
                        k =>
                            new Geometry.Polygon.Sector(objAiBase.Position,
                                Player.Instance.Position.Extend(objAiBase, objAiBase.Distance(Player.Instance) + 400)
                                    .To3D(), (float) Math.PI/180f*40f, 400).IsInside(k)))
                {
                    return objAiBase;
                }
                if (objAiBase.GetType() == typeof (AIHeroClient) &&
                    new Geometry.Polygon.Circle(objAiBase.Position, objAiBase.BoundingRadius - 15).Points.Any(
                        k =>
                            new Geometry.Polygon.Sector(objAiBase.Position,
                                Player.Instance.Position.Extend(objAiBase, objAiBase.Distance(Player.Instance) + 400)
                                    .To3D(), (float) Math.PI/180f*40f, 400).IsInside(k)))
                {
                    return objAiBase;
                }
            }
            return null;
        }

        protected override void OnDraw()
        {
            if (_changingRangeScan)
                Circle.Draw(SharpDX.Color.White,
                    LaneClearMenu["Plugins.MissFortune.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

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
            if (Settings.Misc.EVsGapclosers && E.IsReady() && args.End.Distance(Player.Instance) < 350 && (Player.Instance.Mana - EMana > QMana[Q.Level] + WMana + RMana))
            {
                E.CastMinimumHitchance(sender, 65);
            }
        }

        protected override void CreateMenu()
        {
            ComboMenu = MenuManager.Menu.AddSubMenu("Combo");
            ComboMenu.AddGroupLabel("Combo mode settings for Miss Fortune addon");

            ComboMenu.AddLabel("Double Up (Q) settings :");
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.UseQ", new CheckBox("Use Q"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Strut (W) settings :");
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.UseW", new CheckBox("Use W"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Make It Rain (E) settings :");
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.UseE", new CheckBox("Use E"));
            ComboMenu.AddSeparator(5);

            ComboMenu.AddLabel("Bullet Time (R) settings :");
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.UseR", new CheckBox("Use R"));
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.RWhenXEnemies", new Slider("Use R when can hit {0} or more enemies", 5, 1, 5));
            ComboMenu.AddSeparator(2);

            ComboMenu.Add("Plugins.MissFortune.ComboMenu.RBlockMovement", new CheckBox("Block movement when casting R"));
            ComboMenu.Add("Plugins.MissFortune.ComboMenu.SemiAutoRKeybind",
                new KeyBind("Semi-Auto R", false, KeyBind.BindTypes.HoldActive, 'T'));

            HarassMenu = MenuManager.Menu.AddSubMenu("Harass");
            HarassMenu.AddGroupLabel("Harass mode settings for Miss Fortune addon");

            HarassMenu.AddLabel("Double Up (Q) settings :");
            HarassMenu.Add("Plugins.MissFortune.HarassMenu.UseQ", new CheckBox("Use Q", false));
            HarassMenu.Add("Plugins.MissFortune.HarassMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 75, 1));

            LaneClearMenu = MenuManager.Menu.AddSubMenu("Clear");
            LaneClearMenu.AddGroupLabel("Lane clear settings for Miss Fortune addon");

            LaneClearMenu.AddLabel("Basic settings :");
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.EnableLCIfNoEn", new CheckBox("Enable lane clear only if no enemies nearby"));
            var scanRange = LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.ScanRange", new Slider("Range to scan for enemies", 1500, 300, 2500));
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
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.AllowedEnemies", new Slider("Allowed enemies amount", 1, 0, 5));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Double Up (Q) settings :");
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseQInLaneClear", new CheckBox("Use Q in Lane clear", false));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseQInJungleClear", new CheckBox("Use Q in Jungle clear"));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.MinManaQ", new Slider("Min mana percentage ({0}%) to use Q", 50, 1));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Strut (W) settings :");
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseWInLaneClear", new CheckBox("Use W in Lane clear", false));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseWInJungleClear", new CheckBox("Use W in Jungle clear"));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.MinManaW", new Slider("Min mana percentage ({0}%) to use W", 50, 1));
            LaneClearMenu.AddSeparator(5);

            LaneClearMenu.AddLabel("Make It Rain (E) settings :");
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseEInLaneClear", new CheckBox("Use E in Lane clear", false));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.UseEInJungleClear", new CheckBox("Use E in Jungle clear", false));
            LaneClearMenu.Add("Plugins.MissFortune.LaneClearMenu.MinManaE", new Slider("Min mana percentage ({0}%) to use E", 50, 1));


            MiscMenu = MenuManager.Menu.AddSubMenu("Misc");
            MiscMenu.AddGroupLabel("Misc settings for Miss Fortune addon");
            MiscMenu.AddLabel("Basic settings :");
            MiscMenu.Add("Plugins.MissFortune.MiscMenu.EnableKillsteal", new CheckBox("Enable Killsteal"));
            MiscMenu.AddSeparator(5);

            MiscMenu.AddLabel("Double Up (Q) settings :");
            MiscMenu.Add("Plugins.MissFortune.MiscMenu.BounceQFromMinions", new CheckBox("Cast Q on killable minions if can hit enemy"));
            MiscMenu.Add("Plugins.MissFortune.MiscMenu.AutoHarassQ", new CheckBox("Auto harass with Q")).OnValueChange
                +=
                (sender, args) =>
                {
                    AutoHarassItem.Value = args.NewValue;
                };
            MiscMenu.Add("Plugins.MissFortune.MiscMenu.AutoHarassQMinMana", new Slider("Min mana percentage ({0}%) for auto harass", 50, 1));

            if (EntityManager.Heroes.Enemies.Any())
            {
                MiscMenu.AddLabel("Enable auto harras for : ");

                EntityManager.Heroes.Enemies.ForEach(x => MiscMenu.Add("Plugins.MissFortune.MiscMenu.AutoHarassEnabled." + x.ChampionName, new CheckBox(x.ChampionName == "MonkeyKing" ? "Wukong" : x.ChampionName)));
            }

            MiscMenu.AddLabel("Make It Rain (E) settings :");
            MiscMenu.Add("Plugins.MissFortune.MiscMenu.EVsGapclosers", new CheckBox("Cast E against gapclosers"));

            MenuManager.BuildAntiGapcloserMenu();

            DrawingsMenu = MenuManager.Menu.AddSubMenu("Drawings");
            DrawingsMenu.AddGroupLabel("Drawings settings for Miss Fortune addon");

            DrawingsMenu.AddLabel("Basic settings :");
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawSpellRangesWhenReady", new CheckBox("Draw spell ranges only when they are ready"));
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Double Up (Q) settings :");
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawQ", new CheckBox("Draw Q range", false));
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawQColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[0].Initialize(Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Make It Rain (E) settings :");
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawE", new CheckBox("Draw E range"));
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawEColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[1].Initialize(Color.Aquamarine);
                a.CurrentValue = false;
            };
            DrawingsMenu.AddSeparator(5);

            DrawingsMenu.AddLabel("Bullet Time (R) settings :");
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawR", new CheckBox("Draw R range"));
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawRColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[2].Initialize(Color.Aquamarine);
                a.CurrentValue = false;
            };

            DrawingsMenu.AddLabel("Damage indicator settings :");
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DrawDamageIndicator", new CheckBox("Draw damage indicator")).OnValueChange += (a, b) =>
            {
                if (b.NewValue)
                    DamageIndicator.DamageDelegate = HandleDamageIndicator;
                else if (!b.NewValue)
                    DamageIndicator.DamageDelegate = null;
            };
            DrawingsMenu.Add("Plugins.MissFortune.DrawingsMenu.DamageIndicatorColor", new CheckBox("Change color", false)).OnValueChange += (a, b) =>
            {
                if (!b.NewValue)
                    return;

                ColorPicker[3].Initialize(Color.Aquamarine);
                a.CurrentValue = false;
            };

            AutoHarassItem = MenuManager.PermaShow.AddItem("MissFortune.AutoHarass",
                new BoolItem("Auto harass with Q", Settings.Misc.AutoHarassQ));
        }

        protected override void PermaActive()
        {
            if (Settings.Combo.RBlockMovement && RCasted && (Player.Instance.Spellbook.IsChanneling || Player.Instance.Spellbook.IsCastingSpell))
            {
                Orbwalker.DisableAttacking = true;
                Orbwalker.DisableMovement = true;
            }
            else if(!Player.Instance.Spellbook.IsChanneling && (Game.Time * 1000 - RCastTime > 1000))
            {
                Orbwalker.DisableAttacking = false;
                Orbwalker.DisableMovement = false;

                RCasted = false;
            }

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
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.UseQ"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.UseQ"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseW
                {
                    get
                    {
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.UseW"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.UseW"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.UseW"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.UseW"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseE
                {
                    get
                    {
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.UseE"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.UseE"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.UseE"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.UseE"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseR
                {
                    get
                    {
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.UseR"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.UseR"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.UseR"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.UseR"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool RBlockMovement
                {
                    get
                    {
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.RBlockMovement"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.RBlockMovement"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.RBlockMovement"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.RBlockMovement"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int RWhenXEnemies
                {
                    get
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.RWhenXEnemies"] != null)
                            return ComboMenu["Plugins.MissFortune.ComboMenu.RWhenXEnemies"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.ComboMenu.RWhenXEnemies menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.RWhenXEnemies"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.RWhenXEnemies"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool SemiAutoRKeybind
                {
                    get
                    {
                        return ComboMenu?["Plugins.MissFortune.ComboMenu.SemiAutoRKeybind"] != null &&
                               ComboMenu["Plugins.MissFortune.ComboMenu.SemiAutoRKeybind"].Cast<KeyBind>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (ComboMenu?["Plugins.MissFortune.ComboMenu.SemiAutoRKeybind"] != null)
                            ComboMenu["Plugins.MissFortune.ComboMenu.SemiAutoRKeybind"].Cast<KeyBind>()
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
                        return HarassMenu?["Plugins.MissFortune.HarassMenu.UseQ"] != null &&
                               HarassMenu["Plugins.MissFortune.HarassMenu.UseQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.MissFortune.HarassMenu.UseQ"] != null)
                            HarassMenu["Plugins.MissFortune.HarassMenu.UseQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (HarassMenu?["Plugins.MissFortune.HarassMenu.MinManaQ"] != null)
                            return HarassMenu["Plugins.MissFortune.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.HarassMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (HarassMenu?["Plugins.MissFortune.HarassMenu.MinManaQ"] != null)
                            HarassMenu["Plugins.MissFortune.HarassMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class LaneClear
            {
                public static bool EnableIfNoEnemies
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.EnableLCIfNoEn"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.EnableLCIfNoEn"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.EnableLCIfNoEn"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int ScanRange
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.ScanRange"] != null)
                            return LaneClearMenu["Plugins.MissFortune.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.LaneClearMenu.ScanRange menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.ScanRange"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static int AllowedEnemies
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.AllowedEnemies"] != null)
                            return
                                LaneClearMenu["Plugins.MissFortune.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.LaneClearMenu.AllowedEnemies menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.AllowedEnemies"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.AllowedEnemies"].Cast<Slider>().CurrentValue =
                                value;
                    }
                }

                public static bool UseQInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseQInLaneClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseQInLaneClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseQInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseQInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseQInJungleClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseQInJungleClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseQInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaQ
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaQ"] != null)
                            return LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.LaneClearMenu.MinManaQ menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaQ"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaQ"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool UseWInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseWInLaneClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseWInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseWInLaneClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseWInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseWInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseWInJungleClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseWInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseWInJungleClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseWInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaW
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaW"] != null)
                            return LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaW"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.LaneClearMenu.MinManaW menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaW"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaW"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool UseEInLaneClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseEInLaneClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseEInLaneClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseEInLaneClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseEInLaneClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool UseEInJungleClear
                {
                    get
                    {
                        return LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseEInJungleClear"] != null &&
                               LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseEInJungleClear"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.UseEInJungleClear"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.UseEInJungleClear"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int MinManaE
                {
                    get
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaE"] != null)
                            return LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaE"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.LaneClearMenu.MinManaE menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (LaneClearMenu?["Plugins.MissFortune.LaneClearMenu.MinManaE"] != null)
                            LaneClearMenu["Plugins.MissFortune.LaneClearMenu.MinManaE"].Cast<Slider>().CurrentValue = value;
                    }
                }
            }

            internal static class Misc
            {
                public static bool EnableKillsteal
                {
                    get
                    {
                        return MiscMenu?["Plugins.MissFortune.MiscMenu.EnableKillsteal"] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.EnableKillsteal"] != null)
                            MiscMenu["Plugins.MissFortune.MiscMenu.EnableKillsteal"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool BounceQFromMinions
                {
                    get
                    {
                        return MiscMenu?["Plugins.MissFortune.MiscMenu.BounceQFromMinions"] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.BounceQFromMinions"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.BounceQFromMinions"] != null)
                            MiscMenu["Plugins.MissFortune.MiscMenu.BounceQFromMinions"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool AutoHarassQ
                {
                    get
                    {
                        return MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassQ"] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassQ"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassQ"] != null)
                            MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassQ"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static int AutoHarassQMinMana
                {
                    get
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassQMinMana"] != null)
                            return MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassQMinMana"].Cast<Slider>().CurrentValue;

                        Logger.Error("Couldn't get Plugins.MissFortune.MiscMenu.AutoHarassQMinMana menu item value.");
                        return 0;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassQMinMana"] != null)
                            MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassQMinMana"].Cast<Slider>().CurrentValue = value;
                    }
                }

                public static bool IsAutoHarassEnabledFor(AIHeroClient unit)
                {
                    return MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassEnabled." + unit.ChampionName] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassEnabled." + unit.ChampionName].Cast<CheckBox>()
                                   .CurrentValue;
                }

                public static bool IsAutoHarassEnabledFor(string championName)
                {
                    return MiscMenu?["Plugins.MissFortune.MiscMenu.AutoHarassEnabled." + championName] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.AutoHarassEnabled." + championName].Cast<CheckBox>()
                                   .CurrentValue;
                }

                public static bool EVsGapclosers
                {
                    get
                    {
                        return MiscMenu?["Plugins.MissFortune.MiscMenu.EVsGapclosers"] != null &&
                               MiscMenu["Plugins.MissFortune.MiscMenu.EVsGapclosers"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (MiscMenu?["Plugins.MissFortune.MiscMenu.EVsGapclosers"] != null)
                            MiscMenu["Plugins.MissFortune.MiscMenu.EVsGapclosers"].Cast<CheckBox>()
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
                        return DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawSpellRangesWhenReady"] != null &&
                               DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                   .CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawSpellRangesWhenReady"] != null)
                            DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawSpellRangesWhenReady"].Cast<CheckBox>()
                                .CurrentValue
                                = value;
                    }
                }

                public static bool DrawQ
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawQ"] != null &&
                               DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawQ"] != null)
                            DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawQ"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawE
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawE"] != null &&
                               DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawE"] != null)
                            DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawE"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawR
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawR"] != null &&
                               DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawR"] != null)
                            DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawR"].Cast<CheckBox>().CurrentValue = value;
                    }
                }

                public static bool DrawDamageIndicator
                {
                    get
                    {
                        return DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawDamageIndicator"] != null &&
                               DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue;
                    }
                    set
                    {
                        if (DrawingsMenu?["Plugins.MissFortune.DrawingsMenu.DrawDamageIndicator"] != null)
                            DrawingsMenu["Plugins.MissFortune.DrawingsMenu.DrawDamageIndicator"].Cast<CheckBox>().CurrentValue = value;
                    }
                }
            }
        }
    }
}