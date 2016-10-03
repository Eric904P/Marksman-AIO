#region Licensing
// ---------------------------------------------------------------------
// <copyright file="InitializeAddon.cs" company="EloBuddy">
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
using EloBuddy.SDK.Events;
using Marksman_Master.Interfaces;
using Marksman_Master.Utils;
using SharpDX;

namespace Marksman_Master
{
    internal static class InitializeAddon
    {
        internal static IHeroAddon PluginInstance { get; private set; }

        private static readonly Dictionary<InterrupterEventArgs, AIHeroClient> InterruptibleSpellsFound =
            new Dictionary<InterrupterEventArgs, AIHeroClient>();

        public static bool Initialize()
        {
            LoadPlugin();

            if (PluginInstance == null)
            {
                Misc.PrintInfoMessage("<b><font color=\"#5ED43D\">" + Player.Instance.ChampionName +
                                      "</font></b> is not yet supported.");
                return false;
            }

            if (EntityManager.Heroes.Enemies.Any(x => x.Hero == Champion.Ziggs))
            {
                GameObject.OnCreate += GameObject_OnCreate;
            }

            Chat.OnClientSideMessage += args =>
            {
                if (args.Message.Equals("reset spell", StringComparison.InvariantCultureIgnoreCase))
                    args.Process = false;
            };

            Game.OnTick += Game_OnTick;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;

            return true;
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            if (!sender.Name.Equals("Ziggs_Base_W_tar.troy", StringComparison.InvariantCultureIgnoreCase))
                return;

            if (!MenuManager.MenuValues["MenuManager.GapcloserMenu.Enabled"] ||
                (MenuManager.MenuValues["MenuManager.GapcloserMenu.OnlyInCombo"] &&
                 !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)))
                return;

            var ziggs = EntityManager.Heroes.Enemies.Find(x => x.Hero == Champion.Ziggs);

            if (ziggs == null)
                return;

            var polygon = new Geometry.Polygon.Circle(sender.Position, 325, 50);

            if (!polygon.IsInside(ziggs))
                return;

            var closestPoint = polygon.Points.OrderBy(x => x.Distance(ziggs.ServerPosition.To2D())).ToList()[0];
            var endPosition = ziggs.ServerPosition.Extend(closestPoint, 465).To3D();

            var hp = MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{ziggs.ChampionName}.W.Hp", true];
            var enemies = MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{ziggs.ChampionName}.W.Enemies", true];

            if (MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{ziggs.ChampionName}.W.Enabled"] &&
                Player.Instance.HealthPercent <= hp &&
                Player.Instance.CountEnemiesInRange(MenuManager.GapcloserScanRange) <= enemies)
            {
                PluginInstance.OnGapcloser(ziggs,
                    new GapCloserEventArgs(null, SpellSlot.W, GapcloserTypes.Skillshot,
                        ziggs.Position, endPosition,
                        MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{ziggs.ChampionName}.W.Delay",
                            true], enemies, hp, Core.GameTickCount));
            }
        }

        private static Vector3 _flagPos;
        private static int _flagCreateTick;

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe || Player.Instance.IsDead)
                return;

            var enemy = sender as AIHeroClient;

            if (enemy == null || !enemy.IsEnemy)
                return;

            var menu = MenuManager.MenuValues;

            if (MenuManager.InterruptibleSpellsFound > 0 && menu["MenuManager.InterrupterMenu.Enabled"])
            {
                if (Utils.Interrupter.InterruptibleList.Exists(e => e.ChampionName == enemy.ChampionName) &&
                    ((menu["MenuManager.InterrupterMenu.OnlyInCombo"] &&
                      Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)) ||
                     !menu["MenuManager.InterrupterMenu.OnlyInCombo"]))
                {
                    foreach (var interruptibleSpell in 
                        Utils.Interrupter.InterruptibleList.Where(
                            x => x.ChampionName == enemy.ChampionName && x.SpellSlot == args.Slot))
                    {
                        var hp = menu[$"MenuManager.InterrupterMenu.{enemy.ChampionName}.{interruptibleSpell.SpellSlot}.Hp", true];
                        var enemies = menu[$"MenuManager.InterrupterMenu.{enemy.ChampionName}.{interruptibleSpell.SpellSlot}.Enemies", true];

                        if (menu[$"MenuManager.InterrupterMenu.{enemy.ChampionName}.{interruptibleSpell.SpellSlot}.Enabled"] &&
                            Player.Instance.HealthPercent <= hp &&
                            Player.Instance.CountEnemiesInRange(MenuManager.GapcloserScanRange) <= enemies)
                        {
                            InterruptibleSpellsFound.Add(
                                new InterrupterEventArgs(args.Target, args.Slot, interruptibleSpell.DangerLevel,
                                    interruptibleSpell.SpellName, args.Start, args.End,
                                    menu[$"MenuManager.InterrupterMenu.{enemy.ChampionName}.{interruptibleSpell.SpellSlot}.Delay", true],
                                    enemies, hp, Core.GameTickCount), enemy);
                        }
                    }
                }
            }

            if (MenuManager.GapclosersFound == 0)
                return;

            if (!menu["MenuManager.GapcloserMenu.Enabled"] ||
                (menu["MenuManager.GapcloserMenu.OnlyInCombo"] &&
                 !Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)) ||
                !Gapcloser.GapCloserList.Exists(e => e.ChampName == enemy.ChampionName))
                return;

            if (enemy.Hero == Champion.JarvanIV &&
                args.SData.Name.ToLower() == "jarvanivdemacianstandard" &&
                args.End.Distance(Player.Instance.Position) < 1000)
            {
                _flagPos.X = args.End.X;
                _flagPos.Y = args.End.Y;
                _flagPos.Z = NavMesh.GetHeightForPosition(args.End.X, args.End.Y);
                _flagCreateTick = Core.GameTickCount;
            }

            if (args.SData.Name.Equals("shene", StringComparison.InvariantCultureIgnoreCase))
            {
                if (CanInvoke(enemy.ChampionName, SpellSlot.E))
                {
                    var hp = menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.E.Hp", true];
                    var enemies = menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.E.Enemies", true];

                    PluginInstance.OnGapcloser(enemy,
                            new GapCloserEventArgs(args.Target, args.Slot,
                                args.Target == null ? GapcloserTypes.Skillshot : GapcloserTypes.Targeted,
                                args.Start, GetGapcloserEndPosition(args.Start, args.End, "shene", Gapcloser.GapcloserType.Skillshot),
                                menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.E.Delay",true], enemies, hp, Core.GameTickCount));

                    return;
                }
            }

            foreach (
                var gapcloser in
                    Gapcloser.GapCloserList.Where(
                        x =>
                            x.ChampName == enemy.ChampionName &&
                            string.Equals(x.SpellName, args.SData.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (CanInvoke(enemy.ChampionName, args.Slot))
                {
                    var hp = menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.{gapcloser.SpellSlot}.Hp", true];
                    var enemies = menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.{gapcloser.SpellSlot}.Enemies", true];

                    if (enemy.Hero == Champion.Nidalee && args.SData.Name.ToLowerInvariant() == "pounce")
                    {
                        PluginInstance.OnGapcloser(enemy,
                            new GapCloserEventArgs(args.Target, args.Slot,
                                args.Target == null ? GapcloserTypes.Skillshot : GapcloserTypes.Targeted,
                                args.Start, GetGapcloserEndPosition(args.Start, args.End, gapcloser.SpellName, gapcloser.SkillType),
                                menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.{gapcloser.SpellSlot}.Delay",true], enemies, hp, Core.GameTickCount));
                    }
                    else if (enemy.Hero == Champion.JarvanIV &&
                             args.SData.Name.ToLower() == "jarvanivdragonstrike" && GetGapcloserEndPosition(args.Start, args.End, "jarvanivdragonstrike", Gapcloser.GapcloserType.Skillshot).Distance(Player.Instance.Position) < 1000)
                    {
                        var flagpolygon = new Geometry.Polygon.Circle(_flagPos, 150);
                        var playerpolygon = new Geometry.Polygon.Circle(Player.Instance.Position, 150);

                        for (var i = 900; i > 0; i -= 100)
                        {
                            if (flagpolygon.IsInside(enemy.Position.Extend(args.End, i)) &&
                                playerpolygon.IsInside(enemy.ServerPosition.Extend(args.End, i)))
                            {

                                PluginInstance.OnGapcloser(enemy,
                                    new GapCloserEventArgs(args.Target, args.Slot,
                                        args.Target == null ? GapcloserTypes.Skillshot : GapcloserTypes.Targeted,
                                        args.Start, GetGapcloserEndPosition(args.Start, args.End, gapcloser.SpellName, gapcloser.SkillType),
                                        menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.{gapcloser.SpellSlot}.Delay", true], enemies, hp, Core.GameTickCount));
                                break;
                            }
                        }
                    }

                    else if (enemy.Hero != Champion.Nidalee && enemy.Hero != Champion.JarvanIV && enemy.Hero != Champion.Shen)
                    {
                        PluginInstance.OnGapcloser(enemy,
                            new GapCloserEventArgs(args.Target, args.Slot,
                                args.Target == null ? GapcloserTypes.Skillshot : GapcloserTypes.Targeted,
                                args.Start, GetGapcloserEndPosition(args.Start, args.End, gapcloser.SpellName, gapcloser.SkillType),
                                menu[$"MenuManager.GapcloserMenu.{enemy.ChampionName}.{gapcloser.SpellSlot}.Delay",
                                    true], enemies, hp, Core.GameTickCount));
                    }
                }
            }
        }

        private static bool CanInvoke(string championName, SpellSlot slot)
        {
            return MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{championName}.{slot}.Enabled"] &&
                   (Player.Instance.HealthPercent <=
                    MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{championName}.{slot}.Hp", true]) &&
                   (Player.Instance.CountEnemiesInRange(MenuManager.GapcloserScanRange) <=
                    MenuManager.MenuValues[$"MenuManager.GapcloserMenu.{championName}.{slot}.Enemies", true]);
        }

        public static Vector3 GetGapcloserEndPosition(Vector3 start, Vector3 end, string spellName, Gapcloser.GapcloserType type)
        {
            if (type == Gapcloser.GapcloserType.Targeted)
                return end;

            switch (spellName)
            {
                case "aatroxq": // Aatroxq Q
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 600 ? 600 : distance).To3D();
                }
                case "ahritumble": // Ahri R
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 450 ? 450 : distance).To3D();
                }
                case "caitlynentrapment": // Cait E
                {
                    return start.Extend(end, -400).To3D();
                }
                case "carpetbomb": // Corki W
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 600 ? 600 : distance).To3D();
                }
                case "ezrealarcaneshift": // Ezreal E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 475 ? 475 : distance).To3D();
                }
                case "fioraq": // Fiora Q
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 400 ? 400 : distance).To3D();
                }
                case "gnarbige": // Gnar E
                case "gnare": // Gnar E
                {
                    return start.Extend(end, 475).To3D();
                }
                case "gragase": // Gragas E
                {
                    return start.Extend(end, 600).To3D();
                }
                case "gravesmove": // Graves E
                {
                    return start.Extend(end, 425).To3D();
                }
                case "hecarimult": // Hecarim R
                {
                    return start.Extend(end, 1000).To3D();
                }
                case "jarvanivdragonstrike": // Jarvan Q
                {
                    return start.Extend(end, 790).To3D();
                }
                case "riftwalk": // Kassadin R
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 500 ? 500 : distance).To3D();
                }
                case "kindredq": // Kindred Q
                {
                    return start.Extend(end, 350).To3D();
                }
                case "khazixe": // Kha'Zix E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 700 ? 700 : distance).To3D();
                }
                case "khazixelong": // Kha'Zix E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 900 ? 900 : distance).To3D();
                }
                case "leblancslide": // LeBlanc W
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 600 ? 600 : distance).To3D();
                }
                case "leblancslidem": // LeBlanc R W
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 600 ? 600 : distance).To3D();
                }
                case "leonazenithblade": // Leona E
                {
                    return start.Extend(end, 900).To3D();
                }
                case "luciane": // Lucian E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 425 ? 425 : distance).To3D();
                }
                case "ufslash": // Malphite R
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 1000 ? 1000 : distance).To3D();
                }
                case "renektonsliceanddice": // Renekton E
                {
                    return start.Extend(end, 450).To3D();
                }
                case "reksaieburrowed": // Rek'Sai E
                {
                    return start.Extend(end, 250).To3D();
                }
                case "riventricleave": // Riven Q
                {
                    return start.Extend(end, 270).To3D();
                }
                case "rivenfeint": // Riven E
                {
                    return start.Extend(end, 330).To3D();
                }
                case "sejuaniarcticassault": // Sejuani Q
                {
                    return start.Extend(end, 650).To3D();
                }
                case "shene": // Shen E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 620 ? 620 : distance).To3D();
                }
                case "rocketjump": // Tristana W
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 900 ? 900 : distance).To3D();
                }
                case "slashcast": // Tryndamere E
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 660 ? 660 : distance).To3D();
                }
                case "vaynetumble": // Vayne Q
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 300 ? 300 : distance).To3D();
                }
                case "viq": // Vi Q
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 725 ? 725 : distance).To3D();
                }
                case "zace": // Zac R
                {
                    var distance = start.Distance(end);
                    return start.Extend(end, distance > 1900 ? 1900 : distance).To3D();
                }
                case "ziggsw": // Ziggs W
                {
                    return Vector3.Zero;
                }
                default:
                    return end;
            }
        }

        public static void LoadPlugin()
        {
            var typeName = "Marksman_Master.Plugins." + Player.Instance.ChampionName + "." +
                           Player.Instance.ChampionName;

            var type = Type.GetType(typeName);

            if (type == null)
                return;

            Misc.PrintDebugMessage("Getting saved colorpicker data");

            var colorFileContent = FileHandler.ReadDataFile(FileHandler.ColorFileName);

            Bootstrap.SavedColorPickerData = colorFileContent != null
                ? colorFileContent.ToObject<Dictionary<string, ColorBGRA>>()
                : new Dictionary<string, ColorBGRA>();

            //var constructorInfo = type.GetConstructor(new Type[] {});

            //_plugin = (IHeroAddon) constructorInfo?.Invoke(new object[] {});

            Misc.PrintDebugMessage("Creating plugins instance");

            PluginInstance = (IHeroAddon) System.Activator.CreateInstance(type);
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            PluginInstance.OnDraw();
        }

        private static void Game_OnTick(EventArgs args)
        {
            if (_flagCreateTick != 0 && _flagCreateTick + 8500 < Game.Time*1000)
            {
                _flagCreateTick = 0;
                _flagPos = Vector3.Zero;
            }

            if (InterruptibleSpellsFound.Count > 0)
            {
                foreach (
                    var index in
                        InterruptibleSpellsFound.Where(
                            e =>
                                (int) e.Key.GameTime + 9000 <= (int) Game.Time*1000 ||
                                (!e.Value.Spellbook.IsChanneling && !e.Value.Spellbook.IsCharging &&
                                 !e.Value.Spellbook.IsCastingSpell)).ToList())
                {
                    InterruptibleSpellsFound.Remove(index.Key);
                }
            }

            foreach (var interruptibleSpell in InterruptibleSpellsFound)
            {
                PluginInstance.OnInterruptible(interruptibleSpell.Value, interruptibleSpell.Key);
            }

            PluginInstance.PermaActive();

            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
            {
                PluginInstance.ComboMode();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass))
            {
                PluginInstance.HarassMode();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.JungleClear))
            {
                PluginInstance.JungleClear();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear))
            {
                PluginInstance.LaneClear();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Flee))
            {
                PluginInstance.Flee();
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LastHit))
            {
                PluginInstance.LastHit();
            }
        }
    }
}