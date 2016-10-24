#region Licensing
// ---------------------------------------------------------------------
// <copyright file="MenuManager.cs" company="EloBuddy">
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
namespace Marksman_Master
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using EloBuddy;
    using EloBuddy.SDK;
    using EloBuddy.SDK.Enumerations;
    using EloBuddy.SDK.Events;
    using EloBuddy.SDK.Menu;
    using EloBuddy.SDK.Menu.Values;
    using Extensions;
    using Utils;
    using SharpDX;

    internal static class MenuManager
    {
        internal static Menu Menu { get; set; }
        internal static Menu ExtensionsMenu { get; set; }
        internal static Menu CacheMenu { get; set; }
        internal static PermaShow.PermaShow PermaShow { get; private set; } = new PermaShow.PermaShow("PermaShow", new Vector2(200, 200));
        internal static Menu GapcloserMenu { get; set; }
        internal static Menu InterrupterMenu { get; set; }
        internal static int GapclosersFound { get; private set; }
        internal static int InterruptibleSpellsFound { get; private set; }
        internal static int GapcloserScanRange { get; set; } = 1250;

        internal static MenuValues MenuValues { get; set; } = new MenuValues();

        private static readonly List<ExtensionBase> Extensions = new List<ExtensionBase>();

        private static CheckBox _cache;
        private static CheckBox _debug;

        internal static bool IsDebugEnabled => _debug?.CurrentValue ?? false;
        internal static bool IsCacheEnabled => _cache?.CurrentValue ?? false;

        internal static void CreateMenu()
        {
            ExtensionsMenu = MainMenu.AddMenu("Marksman AIO : Extensions", "MarksmanAIO.Extensions");
            
            _cache = ExtensionsMenu.Add("MenuManager.ExtensionsMenu.EnableCache", new CheckBox("Enable Cache"));
            _cache.OnValueChange += (sender, args) =>
            {
                if (args.NewValue)
                    StaticCacheProvider.Initialize();
            };

            if (Misc.IsMe)
            {
                System.Threading.Tasks.Task.Factory.StartNew(LoadEvadeIc);

                _debug = ExtensionsMenu.Add("MenuManager.ExtensionsMenu.EnableDebug", new CheckBox("Enable Debug", false));
            }

            foreach (var source in Assembly.GetAssembly(typeof(ExtensionBase)).GetTypes().Where(x=>x.IsSubclassOf(typeof(ExtensionBase)) && x.IsSealed))
            {
                var property = source.GetProperty("EnabledByDefault");

                bool enabledByDefault;

                if (property == null)
                {
                    enabledByDefault = false;
                }
                else enabledByDefault = (bool) property.GetValue(source);

                var menuItem = ExtensionsMenu.Add("MenuManager.ExtensionsMenu." + source.Name, new CheckBox("Load " + source.Name, enabledByDefault));

                if (menuItem.CurrentValue)
                {
                    if (Extensions.All(x => x.Name != source.Name))
                    {
                        var instance = System.Activator.CreateInstance(source);

                        Extensions.Add(instance as ExtensionBase);

                        source.GetMethod("Load").Invoke(instance, null);
                    }
                }

                menuItem.OnValueChange += (sender, args) =>
                {
                    if (args.NewValue)
                    {
                        if (Extensions.Any(x => x.Name == source.Name))
                            return;

                        var instance = System.Activator.CreateInstance(source);

                        if (instance == null)
                            return;

                        Extensions.Add(instance as ExtensionBase);

                        source.GetMethod("Load").Invoke(instance, null);
                    }
                    else if (Extensions.Any(x => x.Name == source.Name))
                    {
                        var extension = Extensions.FirstOrDefault(x => x.Name == source.Name);

                        if (extension == null)
                            return;

                        extension.Dispose();
                        Extensions.RemoveAll(x => x.Name == source.Name);
                    }
                };
            }

            CacheMenu = ExtensionsMenu.AddSubMenu("Cache settings", "MenuManager.ExtensionsMenu.CacheMenu");
            CacheMenu.Add("MenuManager.ExtensionsMenu.MinionCacheRefreshRate", new Slider("Minion cache refresh rate : {0} milisecounds", 200, 0, 1000));
            CacheMenu.AddLabel("\nRecomended value : 100 - 200\nThis setting sets the delay between each minion based calculations.");

            Menu = MainMenu.AddMenu("Marksman AIO", "MarksmanAIO");
            Menu.AddGroupLabel("Welcome back, Buddy !");
            Menu.AddSeparator(5);
            Menu.AddLabel("This addon comes in handy for anyone who wants to have\nall marksmans plugins in just one addon. This AIO comes also with beautiful drawings\nand an activator. I just " +
                          "hope you will have fun. Good luck !");
            Menu.AddSeparator(40);
            Menu.AddLabel("Marksman AIO is currently in early beta phase.\nIf you experienced any bugs please report them in the forum thread.");
            
            InitializeAddon.PluginInstance.CreateMenu();
        }

        private static void LoadEvadeIc()
        {
            var timePassed = System.Environment.TickCount;

            var timer = new System.Timers.Timer {Interval = 1000};

            timer.Elapsed += (sender, args) =>
            {
                var s = sender as System.Timers.Timer;

                if (System.Environment.TickCount - timePassed > 3000) // abort the mission :(
                {
                    (s ?? timer).Close();
                    (s ?? timer).Dispose();

                    Misc.PrintDebugMessage("Time has passed. EvadeIC hasn't been found.");
                }

                var evadeMenu = MainMenu.MenuInstances.FirstOrDefault(x => x.Key.Contains("EvadeIC")).Value;

                var enemySpellsMenu = evadeMenu?.FirstOrDefault(x => x.DisplayName.Equals("Enemy spells"));

                if (enemySpellsMenu == null)
                    return;

                var evadeIc = ExtensionsMenu.Add("MenuManager.ExtensionsMenu.EvadeIC",
                    new CheckBox("Set all enemy spells to fast evade in EvadeIC", false));

                evadeIc.OnValueChange += (a, b) =>
                {
                    foreach (var menu in enemySpellsMenu.LinkedValues.Where(x => x.Key.Contains("fastevade")))
                    {
                        menu.Value.Cast<CheckBox>().CurrentValue = b.NewValue;
                    }
                };

                if (evadeIc.CurrentValue)
                {
                    foreach (var menu in enemySpellsMenu.LinkedValues.Where(x => x.Key.Contains("fastevade")))
                    {
                        menu.Value.Cast<CheckBox>().CurrentValue = true;
                    }
                }

                (s ?? timer).Close();
                (s ?? timer).Dispose();

                Misc.PrintDebugMessage("EvadeIC found.");
            };
            timer.Start();
        }

        internal static void BuildInterrupterMenu()
        {
            if (!EntityManager.Heroes.Enemies.Any(x => Utils.Interrupter.InterruptibleList.Exists(e => e.ChampionName == x.ChampionName)))
            {
                return;
            }

            InterrupterMenu = Menu.AddSubMenu("Interrupter");
            InterrupterMenu.AddGroupLabel("Global settings");
            InterrupterMenu.Add("MenuManager.InterrupterMenu.Enabled", new CheckBox("Interrupter Enabled"));
            InterrupterMenu.Add("MenuManager.InterrupterMenu.OnlyInCombo", new CheckBox("Active only in Combo mode", false));
            InterrupterMenu.AddSeparator(15);

            foreach (var enemy in EntityManager.Heroes.Enemies.Where(x => Utils.Interrupter.InterruptibleList.Exists(e => e.ChampionName == x.ChampionName)))
            {
                var interruptibleSpells = Utils.Interrupter.InterruptibleList.FindAll(e => e.ChampionName == enemy.ChampionName);

                if (interruptibleSpells.Count <= 0)
                    continue;

                InterrupterMenu.AddGroupLabel(enemy.ChampionName);

                foreach (var interruptibleSpell in interruptibleSpells)
                {
                    int healthPercent;

                    switch (interruptibleSpell.DangerLevel)
                    {
                        case DangerLevel.High:
                            healthPercent = 100;
                            break;
                        case DangerLevel.Medium:
                            healthPercent = 75;
                            break;
                        case DangerLevel.Low:
                            healthPercent = 50;
                            break;
                        default:
                            healthPercent = 0;
                            break;
                    }

                    InterrupterMenu.AddLabel("[" + interruptibleSpell.SpellSlot + "] " + interruptibleSpell.SpellName + " | Danger Level : " + interruptibleSpell.DangerLevel);
                    InterrupterMenu.Add("MenuManager.InterrupterMenu." + enemy.ChampionName + "." + interruptibleSpell.SpellSlot + ".Delay", new Slider("Delay", 0, 0, 500));
                    InterrupterMenu.Add("MenuManager.InterrupterMenu." + enemy.ChampionName + "." + interruptibleSpell.SpellSlot + ".Hp", new Slider("Only if I'm below under {0} % of my HP", healthPercent));
                    InterrupterMenu.Add("MenuManager.InterrupterMenu." + enemy.ChampionName + "." + interruptibleSpell.SpellSlot + ".Enemies", new Slider("Only if {0} or less enemies are near", 5, 1, 5));
                    InterrupterMenu.Add("MenuManager.InterrupterMenu." + enemy.ChampionName + "." + interruptibleSpell.SpellSlot + ".Enabled", new CheckBox("Enabled"));

                    InterruptibleSpellsFound++;
                }
            }
        }

        internal static void BuildAntiGapcloserMenu()
        {
            if (!EntityManager.Heroes.Enemies.Any(x => Gapcloser.GapCloserList.Exists(e => e.ChampName == x.ChampionName) || (x.Hero == Champion.Rengar)))
            {
                return;
            }

            GapcloserMenu = Menu.AddSubMenu("Anti-Gapcloser");
            GapcloserMenu.AddGroupLabel("Global settings");
            GapcloserMenu.Add("MenuManager.GapcloserMenu.Enabled", new CheckBox("Anti-Gapcloser Enabled"));
            GapcloserMenu.Add("MenuManager.GapcloserMenu.OnlyInCombo", new CheckBox("Active only in Combo mode", false));
            GapcloserMenu.AddSeparator(15);
            
            foreach (var enemy in
                EntityManager.Heroes.Enemies.Where(x => Gapcloser.GapCloserList.Exists(e => e.ChampName == x.ChampionName)))
            {
                var gapclosers = Gapcloser.GapCloserList.FindAll(e => e.ChampName == enemy.ChampionName);

                if (gapclosers.Count <= 0)
                    continue;

                GapcloserMenu.AddGroupLabel(enemy.ChampionName);

                foreach (var gapcloser in gapclosers)
                {
                    GapcloserMenu.AddLabel("[" + gapcloser.SpellSlot + "] " + gapcloser.SpellName);
                    GapcloserMenu.Add("MenuManager.GapcloserMenu." + enemy.ChampionName + "." + gapcloser.SpellSlot + ".Delay", new Slider("Delay", 0, 0, 500));
                    GapcloserMenu.Add("MenuManager.GapcloserMenu." + enemy.ChampionName + "." + gapcloser.SpellSlot + ".Hp", new Slider("Only if I'm below under {0} % of my HP", 100));
                    GapcloserMenu.Add("MenuManager.GapcloserMenu." + enemy.ChampionName + "." + gapcloser.SpellSlot + ".Enemies", new Slider("Only if {0} or less enemies are near", 5, 1, 5));
                    GapcloserMenu.Add("MenuManager.GapcloserMenu." + enemy.ChampionName + "." + gapcloser.SpellSlot + ".Enabled", new CheckBox("Enabled"));

                    GapclosersFound++;
                }
            }

            if (EntityManager.Heroes.Enemies.All(x => x.Hero != Champion.Rengar))
                return;

            GapcloserMenu.AddGroupLabel("Rengar");

            GapcloserMenu.AddLabel("[Passive] Leap");
            GapcloserMenu.Add("MenuManager.GapcloserMenu.Rengar.Passive.Delay", new Slider("Delay", 0, 0, 500));
            GapcloserMenu.Add("MenuManager.GapcloserMenu.Rengar.Passive.Hp", new Slider("Only if I'm below under {0} % of my HP", 100));
            GapcloserMenu.Add("MenuManager.GapcloserMenu.Rengar.Passive.Enemies", new Slider("Only if {0} or less enemies are near", 5, 1, 5));
            GapcloserMenu.Add("MenuManager.GapcloserMenu.Rengar.Passive.Enabled", new CheckBox("Enabled"));

            GapclosersFound++;
        }
    }
}