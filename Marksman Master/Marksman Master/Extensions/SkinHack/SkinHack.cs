namespace Marksman_Master.Extensions.SkinHack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EloBuddy;
    using EloBuddy.SDK.Menu;
    using EloBuddy.SDK.Menu.Values;

    internal sealed class SkinHack : ExtensionBase
    {
        private Menu SkinHackMenu { get; set; }

        public override bool IsEnabled { get; set; } = true;

        public override string Name { get; } = "SkinHack";

        public Dictionary<Champion, Dictionary<string, int>> Skins { get; private set; }

        public ComboBox SkinId { get; set; }

        public override void Load()
        {
            IsEnabled = true;

            Skins = new Dictionary<Champion, Dictionary<string, int>>
            {
                {Champion.Ashe, new Dictionary<string, int>
                {
                    {"Classic Ashe", 0},
                    {"Freljord Ashe", 1},
                    {"Sherwood Forest Ashe", 2},
                    {"Woad Ashe", 3},
                    {"Queen Ashe", 4},
                    {"Amethyst Ashe", 5},
                    {"Heartseeker Ashe", 6},
                    {"Marauder Ashe", 7},
                    {"PROJECT: Ashe", 8}
                }},
                { Champion.Caitlyn, new Dictionary<string, int>
                {
                    {"Classic Caitlyn", 0},
                    {"Resistance Caitlyn", 1},
                    {"Sheriff Caitlyn", 2},
                    {"Safari Caitlyn", 3},
                    {"Arctic Warfare Caitlyn", 4},
                    {"Officer Caitlyn", 5},
                    {"Headhunter Caitlyn", 6},
                    {"Classic Caitlyn : Pink Chroma", 7},
                    {"Classic Caitlyn : Green Chroma", 8},
                    {"Classic Caitlyn : Blue Chroma", 9},
                    {"Lunar Wraith Caitlyn", 10}
                }},
                { Champion.Corki, new Dictionary<string, int>
                {
                    {"Classic Corki", 0},
                    {"UFO Corki", 1},
                    {"Ice Toboggan Corki", 2},
                    {"Red Baron Corki", 3},
                    {"Hot Rod Corki", 4},
                    {"Urfrider Corki", 5},
                    {"Dragonwing Corki", 6},
                    {"Fnatic Corki", 7},
                    {"Arcade Corki", 8}
                }},
                { Champion.Draven, new Dictionary<string, int>
                {
                    {"Classic Draven", 0},
                    {"Soul Reaver Draven", 1},
                    {"Gladiator Draven", 2},
                    {"Primetime Draven", 3},
                    {"Pool Party Draven", 4},
                    {"Beast Hunter Draven", 5},
                    {"Draven Draven", 6}
                }},
                { Champion.Ezreal, new Dictionary<string, int>
                {
                    {"Classic Ezreal", 0},
                    {"Nottingham Ezreal", 1},
                    {"Striker Ezreal", 2},
                    {"Frosted Ezreal", 3},
                    {"Explorer Ezreal", 4},
                    {"Pulsefire Ezreal", 5},
                    {"TPA Ezreal", 6},
                    {"Debonair Ezreal", 7},
                    {"Ace of Spades Ezreal", 8},
                    {"Arcade Ezreal", 9}
                }},
                { Champion.Graves, new Dictionary<string, int>
                {
                    {"Classic Graves", 0},
                    {"Hired Gun Graves", 1},
                    {"Jailbreak Graves", 2},
                    {"Mafia Graves", 3},
                    {"Riot Graves", 4},
                    {"Pool Party Graves", 5},
                    {"Cutthroat Graves", 6}
                }},
                { Champion.Jhin, new Dictionary<string, int>
                {
                    {"Classic Jhin", 0},
                    {"High Noon Jhin", 1}
                }},
                { Champion.Jinx, new Dictionary<string, int>
                {
                    {"Classic Jinx", 0},
                    {"Mafia Jinx", 1},
                    {"Firecracker Jinx", 2},
                    {"Slayer Jinx", 3}
                }},
                { Champion.Kalista, new Dictionary<string, int>
                {
                    {"Classic Kalista", 0},
                    {"Blood Moon Kalista", 1},
                    {"Championship Kalista", 2},
                    {"SKT T1 Kalista", 3}
                }},
                { Champion.KogMaw, new Dictionary<string, int>
                {
                    {"Classic Kog'Maw", 0},
                    {"Caterpillar Kog'Maw", 1},
                    {"Monarch Kog'Maw", 2},
                    {"Sonoran Kog'Maw", 3},
                    {"Reindeer Kog'Maw", 4},
                    {"Lion Dance Kog'Maw", 5},
                    {"Deep Sea Kog'Maw", 6},
                    {"Jurassic Kog'Maw", 7},
                    {"Battlecast Kog'Maw", 8}
                }},
                { Champion.Lucian, new Dictionary<string, int>
                {
                    {"Classic Lucian", 0},
                    {"Hired Gun Lucian", 1},
                    {"Striker Lucian", 2},
                    {"Classic Lucian : Yellow Chroma", 3},
                    {"Classic Lucian : Red Chroma", 4},
                    {"Classic Lucian : Blue Chroma", 5},
                    {"PROJECT: Lucian", 6}
                }},
                { Champion.MissFortune, new Dictionary<string, int>
                {
                    {"Classic Miss Fortune", 0},
                    {"Cowgirl Miss Fortune", 1},
                    {"Waterloo Miss Fortune", 2},
                    {"Secret Agent Miss Fortune", 3},
                    {"Candy Cane Miss Fortune", 4},
                    {"Road Warrior Miss Fortune", 5},
                    {"Mafia Miss Fortune", 6},
                    {"Arcade Miss Fortune ", 7},
                    {"Captain Fortune", 8},
                    {"Pool Party Miss Fortune", 9},
                    {"New Arcade Miss Fortune", 10}
                }},
                { Champion.Quinn, new Dictionary<string, int>
                {
                    {"Classic Quinn", 0},
                    {"Phoenix Quinn", 1},
                    {"Woad Scout Quinn", 2},
                    {"Corsair Quinn (Taylor Swift)", 3}
                }},
                { Champion.Sivir, new Dictionary<string, int>
                {
                    {"Classic Sivir", 0},
                    {"Warrior Princess Sivir", 1},
                    {"Spectacular Sivir", 2},
                    {"Huntress Sivir", 3},
                    {"Bandit Sivir", 4},
                    {"PAX Sivir", 5},
                    {"Snowstorm Sivir", 6},
                    {"Warden Sivir", 7},
                    {"Victorious Sivir", 8}
                }},
                { Champion.Tristana, new Dictionary<string, int>
                {
                    {"Classic Tristana", 0},
                    {"Riot Girl Tristana", 1},
                    {"Earnest Elf Tristana", 2},
                    {"Firefighter Tristana", 3},
                    {"Guerilla Tristana", 4},
                    {"Buccaneer Tristana", 5},
                    {"Rocket Girl Tristana", 6},
                    {"Rocket Girl Tristana : Blue Chroma", 7},
                    {"Rocket Girl Tristana : Sea blue Chroma", 8},
                    {"Rocket Girl Tristana : Red hair Chroma", 9},
                    {"Dragon Trainer Tristana", 10}
                }},
                { Champion.Twitch, new Dictionary<string, int>
                {
                    {"Classic Twitch", 0},
                    {"Kingpin Twitch", 1},
                    {"Whistler Village Twitch", 2},
                    {"Medieval Twitch", 3},
                    {"Gangster Twitch", 4},
                    {"Vandal Twitch", 5},
                    {"Pickpocket Twitch", 6},
                    {"SSW Twitch", 7}
                }},
                { Champion.Urgot, new Dictionary<string, int>
                {
                    {"Classic Urgot", 0},
                    {"Giant Enemy Crabgot", 1},
                    {"Butcher Urgot", 2},
                    {"Battlecast Urgot", 3}
                }},
                { Champion.Varus, new Dictionary<string, int>
                {
                    {"Classic Varus", 0},
                    {"Blight Crystal Varus", 1},
                    {"Arclight Varus", 2},
                    {"Arctic Ops Varus", 3},
                    {"Heartseeker Varus", 4},
                    {"Varus Swiftbolt", 5},
                    {"Dark Star Varus", 6}
                }},
                {Champion.Vayne, new Dictionary<string, int>
                {
                    {"Classic Vayne", 0},
                    {"Vindicator Vayne", 1},
                    {"Aristocrat Vayne", 2},
                    {"Dragonslayer Vayne", 3},
                    {"Heartseeker Vayne", 4},
                    {"SKT T1 Vayne", 5},
                    {"Arclight Vayne", 6},
                    {"Dragonslayer Vayne : Green Chroma", 7},
                    {"Dragonslayer Vayne : Red Chroma", 8},
                    {"Dragonslayer Vayne : Silver Chroma", 9},
                    {"Soulsteler Vayne", 10}
                }}
            };

            if (!MenuManager.ExtensionsMenu.SubMenus.Any(x => x.UniqueMenuId.Contains("Extension.SkinHack")))
            {
                if (!MainMenu.IsOpen)
                {
                    SkinHackMenu = MenuManager.ExtensionsMenu.AddSubMenu("Skin Hack", "Extension.SkinHack");
                    BuildMenu();
                } else MainMenu.OnClose += MainMenu_OnClose;
            }
            else
            {
                foreach (var subMenu in MenuManager.ExtensionsMenu.SubMenus)
                {
                    if (subMenu["SkinId." + Player.Instance.ChampionName] == null)
                        return;
                    
                    SkinId = subMenu["SkinId." + Player.Instance.ChampionName].Cast<ComboBox>();
                    subMenu["SkinId." + Player.Instance.ChampionName].Cast<ComboBox>().OnValueChange += SkinId_OnValueChange;

                    Player.Instance.SetSkin(Player.Instance.BaseSkinName, SkinId.CurrentValue);
                }
            }

            Obj_AI_Base.OnUpdateModel += Obj_AI_Base_OnUpdateModel;
        }

        private void Obj_AI_Base_OnUpdateModel(Obj_AI_Base sender, UpdateModelEventArgs args)
        {
            if (!sender.IsMe || !IsEnabled)
                return;

            if (args.Model == Player.Instance.BaseSkinName && args.SkinId != SkinId.CurrentValue)
                args.Process = false;
        }

        private void MainMenu_OnClose(object sender, EventArgs args)
        {
            if (MenuManager.ExtensionsMenu.SubMenus.Any(x => x.UniqueMenuId.Contains("Extension.SkinHack")))
                return;

            SkinHackMenu = MenuManager.ExtensionsMenu.AddSubMenu("Skin Hack", "Extension.SkinHack");
            BuildMenu();

            MainMenu.OnClose -= MainMenu_OnClose;
        }

        private void BuildMenu()
        {
            var skins =
                Skins.Where(x => x.Key == Player.Instance.Hero)
                    .Select(x => x.Value)
                    .Select(x => x.Keys)
                    .ToList()
                    .FirstOrDefault();

            if (skins == null)
                return;

            SkinHackMenu.AddGroupLabel("Skin hack settings : ");

            SkinId = SkinHackMenu.Add("SkinId."+Player.Instance.ChampionName, new ComboBox("Skin : ", skins));

            Player.Instance.SetSkin(Player.Instance.BaseSkinName, SkinId.CurrentValue);

            SkinId.OnValueChange += SkinId_OnValueChange;
        }

        private void SkinId_OnValueChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
        {
            ChangeSkin(args.NewValue);
        }

        private void ChangeSkin(int id)
        {
            if (!IsEnabled)
                return;

            Player.SetSkin(Player.Instance.BaseSkinName, id);
        }

        public override void Dispose()
        {
            IsEnabled = false;
            
            SkinId.OnValueChange -= SkinId_OnValueChange;
            MainMenu.OnClose -= MainMenu_OnClose;
            Obj_AI_Base.OnUpdateModel -= Obj_AI_Base_OnUpdateModel;
        }
    }
}