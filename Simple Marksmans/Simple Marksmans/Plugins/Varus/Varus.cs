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
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Rendering;
using Simple_Marksmans.Utils;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using EloBuddy.SDK.Utils;

namespace Simple_Marksmans.Plugins.Varus
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

        private static readonly Text Text;

        private static readonly Dictionary<int, Dictionary<float, float>> Damages =
            new Dictionary<int, Dictionary<float, float>>();

        static Varus()
        {
            Q = new Spell.Chargeable(SpellSlot.Q, 1000, 1600, 1500, 250, 2000, 50);
            W = new Spell.Active(SpellSlot.W);
            E = new Spell.Skillshot(SpellSlot.E, 900, SkillShotType.Linear);
            R = new Spell.Skillshot(SpellSlot.R, 2000, SkillShotType.Linear);

            ColorPicker = new ColorPicker[4];

            ColorPicker[0] = new ColorPicker("VarusQ", new ColorBGRA(10, 106, 138, 255));
            ColorPicker[1] = new ColorPicker("VarusE", new ColorBGRA(177, 67, 191, 255));
            ColorPicker[2] = new ColorPicker("VarusR", new ColorBGRA(255, 134, 0, 255));
            ColorPicker[3] = new ColorPicker("VarusHpBar", new ColorBGRA(255, 134, 0, 255));

            DamageIndicator.Initalize(System.Drawing.Color.FromArgb(ColorPicker[3].Color.R, ColorPicker[3].Color.G,
                ColorPicker[3].Color.B), (int) R.Range);
            DamageIndicator.DamageDelegate = HandleDamageIndicator;

            ColorPicker[3].OnColorChange +=
                (a, b) =>
                {
                    DamageIndicator.Color = System.Drawing.Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B);
                };

            Text = new Text("", new Font("calibri", 15, FontStyle.Regular));
        }

        private static float HandleDamageIndicator(Obj_AI_Base unit)
        {
            //if (!Settings.Drawings.DrawDamageIndicator)
            //{
            //    return 0;
            //}

            //return unit.GetType() != typeof (AIHeroClient) ? 0 : GetComboDamage(unit);
            return 0;
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
                Damages.Add(unit.NetworkId, new Dictionary<float, float> {{Game.Time*1000, damage}});
            }
            else
            {
                Damages[unit.NetworkId] = new Dictionary<float, float> {{Game.Time*1000, damage}};
            }

            return damage;
        }

        protected override void OnDraw()
        {
/*
            if (_changingRangeScan)
                Circle.Draw(SharpDX.Color.White,
                    LaneClearMenu["Plugins.Varus.LaneClearMenu.ScanRange"].Cast<Slider>().CurrentValue, Player.Instance);

            if (Settings.Drawings.DrawQ && (!Settings.Drawings.DrawSpellRangesWhenReady || Q.IsReady()))
                Circle.Draw(ColorPicker[0].Color, Q.Range, Player.Instance);
            if (Settings.Drawings.DrawE && (!Settings.Drawings.DrawSpellRangesWhenReady || E.IsReady()))
                Circle.Draw(ColorPicker[1].Color, E.Range, Player.Instance);
            if (Settings.Drawings.DrawR && (!Settings.Drawings.DrawSpellRangesWhenReady || R.IsReady()))
                Circle.Draw(ColorPicker[2].Color, R.Range, Player.Instance);*/

            var t =
                EntityManager.MinionsAndMonsters.CombinedAttackable.FirstOrDefault(
                    x => x.Distance(Player.Instance) < 500);

            if (t == null)
                return;
            Circle.Draw(ColorPicker[2].Color, 120, t);

            Text.TextValue = "Damage : " + GetQDamage(t);
            Text.Position = new Vector2(200, 200);
            Text.Draw();

        }

        protected static float GetQDamage(Obj_AI_Base unit)
        {
            float[] minDamage = {0, 10, 46.7f, 83.3f, 120, 156.7f};
            float[] maxDamage = {0, 15, 70, 125, 180, 235};
            const float minScaling = 1f;
            const float maxScaling = 1.6f;

            var time = Math.Min(Core.GameTickCount + 500 - Q.ChargingStartedTime, Q.FullyChargedTime);
            var percent = (float) (Misc.GetProcentFromNumberRange(time, 0, Q.FullyChargedTime)/100);
            var damage = Math.Max(minDamage[Q.Level], maxDamage[Q.Level]*percent) +
                         Player.Instance.TotalAttackDamage*Math.Max(minScaling, maxScaling*percent);

            return Player.Instance.CalculateDamageOnUnit(unit, DamageType.Physical, damage);
        }

        protected override void OnInterruptible(AIHeroClient sender, InterrupterEventArgs args)
        {
        }

        protected override void OnGapcloser(AIHeroClient sender, GapCloserEventArgs args)
        {
        }

        protected override void CreateMenu()
        {

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
    }
}