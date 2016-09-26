#region Licensing
// ---------------------------------------------------------------------
// <copyright file="Bootstrap.cs" company="EloBuddy">
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
using EloBuddy;
using EloBuddy.Sandbox;
using EloBuddy.SDK;
using Marksman_Master.Utils;
using SharpDX;

namespace Marksman_Master
{
    internal class Bootstrap
    {
        public static bool MenuLoaded { get; set; }

        public static Dictionary<string, ColorBGRA> SavedColorPickerData { get; set; }

        public static void Initialize()
        {
            Misc.PrintDebugMessage("Initializing cache");

            StaticCacheProvider.Initialize();

            Misc.PrintDebugMessage("Initializing addon");

            var pluginInitialized = InitializeAddon.Initialize();

            if (!pluginInitialized)
                return;

            Core.DelayAction(
                () =>
                {
                    Misc.PrintDebugMessage("Creating Menu");

                    MenuManager.CreateMenu();
                    
                    Misc.PrintDebugMessage("Initializing activator");

                    Activator.Activator.InitializeActivator();

                    MenuLoaded = true;

                    Misc.PrintInfoMessage(
                        $"<b><font color=\"#5ED43D\">{Player.Instance.ChampionName}</font></b> loaded successfully. Welcome back <b><font color=\"{(SandboxConfig.IsBuddy ? "#BF1B49" : "#1BBF91")}\">{(SandboxConfig.IsBuddy ? "[VIP] " + (SandboxConfig.Username == "intr" ? "intr you boosted animal from Latvia <3" : SandboxConfig.Username) : SandboxConfig.Username == "intr" ? "cunt" : SandboxConfig.Username)}</font></b> !");

                    Misc.PrintDebugMessage("Marksman AIO  fully loaded");
                }, 250);
        }
    }
}