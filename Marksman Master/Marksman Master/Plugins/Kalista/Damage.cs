#region Licensing
// //  ---------------------------------------------------------------------
// //  <copyright file="Damage.cs" company="EloBuddy">
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
using Marksman_Master.Utils;

namespace Marksman_Master.Plugins.Kalista
{
    internal static class Damage
    {
        private static readonly int[] EDamage = { 0, 20, 30, 40, 50, 60 };
        private const float EDamageMod = 0.6f;
        private static readonly int[] EDamagePerSpear = { 0, 10, 14, 19, 25, 32 };
        private static readonly float[] EDamagePerSpearMod = { 0, 0.2f, 0.225f, 0.25f, 0.275f, 0.3f };

        private static readonly Dictionary<int, Tuple<Dictionary<float, float>, int>> ComboDamages =
            new Dictionary<int, Tuple<Dictionary<float, float>, int>>();
        private static readonly Dictionary<int, Tuple<Dictionary<float, float>, int>> EDamagesStacks =
            new Dictionary<int, Tuple<Dictionary<float, float>, int>>();
        private static readonly Dictionary<int, Dictionary<float, float>> EDamages =
            new Dictionary<int, Dictionary<float, float>>();
        private static readonly Dictionary<int, Dictionary<float, bool>> IsKillable =
            new Dictionary<int, Dictionary<float, bool>>();

        public static float GetComboDamage(this AIHeroClient enemy, int stacks)
        {
            if (ComboDamages.ContainsKey(enemy.NetworkId) && !ComboDamages.Any(x => x.Key == enemy.NetworkId && x.Value.Item2 == stacks && x.Value.Item1.Any(k => Game.Time * 1000 - k.Key > 200)))
                return ComboDamages[enemy.NetworkId].Item1.Values.FirstOrDefault();

            float damage = 0;

            if (Kalista.Q.IsReady())
                damage += Player.Instance.GetSpellDamage(enemy, SpellSlot.Q);

            if (Activator.Activator.Items[ItemsEnum.BladeOfTheRuinedKing] != null && Activator.Activator.Items[ItemsEnum.BladeOfTheRuinedKing].ToItem().IsReady())
                damage += Player.Instance.GetItemDamage(enemy, ItemId.Blade_of_the_Ruined_King);

            if (Activator.Activator.Items[ItemsEnum.Cutlass] != null && Activator.Activator.Items[ItemsEnum.Cutlass].ToItem().IsReady())
                damage += Player.Instance.GetItemDamage(enemy, ItemId.Bilgewater_Cutlass);

            if (Activator.Activator.Items[ItemsEnum.Gunblade] != null && Activator.Activator.Items[ItemsEnum.Gunblade].ToItem().IsReady())
                damage += Player.Instance.GetItemDamage(enemy, ItemId.Hextech_Gunblade);

            if (Kalista.E.IsReady())
                damage += enemy.GetRendDamageOnTarget(stacks);

            damage += Player.Instance.GetAutoAttackDamage(enemy, true) * stacks;

            ComboDamages[enemy.NetworkId] = new Tuple<Dictionary<float, float>, int>(new Dictionary<float, float> { { Game.Time * 1000, damage } }, stacks);

            return damage;
        }

        public static bool CanCastEOnUnit(this Obj_AI_Base target)
        {
            if (target == null || !target.IsValidTarget(Kalista.E.Range) || target.GetRendBuff() == null ||
                !Kalista.E.IsReady() || target.GetRendBuff().Count < 1)
                return false;

            if (!(target is AIHeroClient))
                return true;

            var heroClient = (AIHeroClient) target;

            return !heroClient.HasUndyingBuffA() && !heroClient.HasSpellShield();
        }

        public static bool IsTargetKillableByRend(this Obj_AI_Base target)
        {
            if (target == null || !target.IsValidTarget(Kalista.E.Range) || target.GetRendBuff() == null ||
                !Kalista.E.IsReady() || target.GetRendBuff().Count < 1)
                return false;

            if (IsKillable.ContainsKey(target.NetworkId) && !IsKillable.Any(x => x.Key == target.NetworkId && x.Value.Any(k => Game.Time * 1000 - k.Key > 200)))
                return IsKillable[target.NetworkId].Values.FirstOrDefault();

            if (target.GetType() != typeof(AIHeroClient))
            {
                IsKillable[target.NetworkId] = new Dictionary<float, bool> { { Game.Time * 1000, target.GetRendDamageOnTarget() > target.TotalHealthWithShields() } };
                return target.GetRendDamageOnTarget() > target.TotalHealthWithShields();
            }

            var heroClient = (AIHeroClient) target;

            if (heroClient.HasUndyingBuffA() || heroClient.HasSpellShield())
            {
                IsKillable[heroClient.NetworkId] = new Dictionary<float, bool> { { Game.Time * 1000, false } };
                return false;
            }

            if (heroClient.ChampionName != "Blitzcrank")
            {
                IsKillable[heroClient.NetworkId] = new Dictionary<float, bool> { { Game.Time * 1000, heroClient.GetRendDamageOnTarget() >= heroClient.TotalHealthWithShields() } };
                return heroClient.GetRendDamageOnTarget() >= heroClient.TotalHealthWithShields();
            }
            if (!heroClient.HasBuff("BlitzcrankManaBarrierCD") && !heroClient.HasBuff("ManaBarrier"))
            {
                IsKillable[heroClient.NetworkId] = new Dictionary<float, bool> { { Game.Time * 1000, heroClient.GetRendDamageOnTarget() > heroClient.TotalHealthWithShields() + heroClient.Mana / 2 } };
                return heroClient.GetRendDamageOnTarget() > heroClient.TotalHealthWithShields() + heroClient.Mana / 2;
            }

            IsKillable[heroClient.NetworkId] = new Dictionary<float, bool> { { Game.Time * 1000, heroClient.GetRendDamageOnTarget() > heroClient.TotalHealthWithShields() } };

            return heroClient.GetRendDamageOnTarget() > heroClient.TotalHealthWithShields();
        }

        public static float GetRendDamageOnTarget(this Obj_AI_Base target)
        {
            if (!CanCastEOnUnit(target))
                return 0f;

            if (EDamages.ContainsKey(target.NetworkId) && !EDamages.Any(x => x.Key == target.NetworkId && x.Value.Any(k => Game.Time * 1000 - k.Key > 200)))
                return EDamages[target.NetworkId].Values.FirstOrDefault();

            var damageReduction = 100 - Kalista.Settings.Misc.ReduceEDmg;
            var damage = EDamage[Kalista.E.Level] + Player.Instance.TotalAttackDamage * EDamageMod +
                         (target.GetRendBuff().Count > 1
                             ? (EDamagePerSpear[Kalista.E.Level] +
                                Player.Instance.TotalAttackDamage * EDamagePerSpearMod[Kalista.E.Level]) *
                               (target.GetRendBuff().Count - 1)
                             : 0);

            var finalDamage = Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical,
                damage*damageReduction/100);

            EDamages[target.NetworkId] = new Dictionary<float, float> { { Game.Time * 1000, finalDamage } };

            return finalDamage;
        }

        public static float GetRendDamageOnTarget(this Obj_AI_Base target, int stacks)
        {
            if (target == null || stacks < 1)
                return 0f;

            if (EDamagesStacks.ContainsKey(target.NetworkId) && !EDamagesStacks.Any(x => x.Key == target.NetworkId && x.Value.Item2 == stacks && x.Value.Item1.Any(k => Game.Time * 1000 - k.Key > 200)))
                return EDamagesStacks[target.NetworkId].Item1.Values.FirstOrDefault();

            var damageReduction = 100 - Kalista.Settings.Misc.ReduceEDmg;

            var damage = EDamage[Kalista.E.Level] + Player.Instance.TotalAttackDamage * EDamageMod +
                         (stacks > 1
                             ? (EDamagePerSpear[Kalista.E.Level] +
                                Player.Instance.TotalAttackDamage * EDamagePerSpearMod[Kalista.E.Level]) *
                               (stacks - 1)
                             : 0);
            var finalDamage = Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical,
                damage*damageReduction/100);

            ComboDamages[target.NetworkId] = new Tuple<Dictionary<float, float>, int>(new Dictionary<float, float> { { Game.Time * 1000, finalDamage } }, stacks);

            return finalDamage;
        }

        public static BuffInstance GetRendBuff(this Obj_AI_Base target)
        {
            return
                target.Buffs.Find(
                    b => b.Caster.IsMe && b.IsValid && b.DisplayName.ToLowerInvariant() == "kalistaexpungemarker");
        }

        public static bool HasRendBuff(this Obj_AI_Base target)
        {
            return target.GetRendBuff() != null;
        }
    }
}