using System.Threading;
using System.Timers;
using EloBuddy.SDK;

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

        public override bool IsEnabled { get; set; }

        public static bool EnabledByDefault { get; set; } = true;

        public override string Name { get; } = "SkinHack";

        public Dictionary<Champion, Dictionary<string, byte>> Skins { get; private set; }
        public Dictionary<KeyValuePair<Champion, byte>, Dictionary<string, byte>> Chromas { get; private set; }
        public Dictionary<Champion, string> BaseSkinNames { get; private set; }

        public ComboBox SkinId { get; set; }
        public Slider ChromaId { get; set; }

        public byte LoadSkinId { get; private set; }

        public byte CurrentSkin { get; set; }

        public override void Load()
        {
            LoadSkinId = (byte) Player.Instance.SkinId;

            IsEnabled = true;
            
            BaseSkinNames = new Dictionary<Champion, string>
            {
                [Champion.Ashe] = "Ashe",
                [Champion.Caitlyn] = "Caitlyn",
                [Champion.Corki] = "Corki",
                [Champion.Draven] = "Draven",
                [Champion.Ezreal] = "Ezreal",
                [Champion.Graves] = "Graves",
                [Champion.Jhin] = "Jhin",
                [Champion.Jinx] = "Jinx",
                [Champion.Kalista] = "Kalista",
                [Champion.KogMaw] = "KogMaw",
                [Champion.Lucian] = "Lucian",
                [Champion.MissFortune] = "MissFortune",
                [Champion.Quinn] = "Quinn",
                [Champion.Sivir] = "Sivir",
                [Champion.Tristana] = "Tristana",
                [Champion.Twitch] = "Twitch",
                [Champion.Urgot] = "Urgot",
                [Champion.Varus] = "Varus",
                [Champion.Vayne] = "Vayne"
            };

            Chromas = new Dictionary<KeyValuePair<Champion, byte>, Dictionary<string, byte>>
            {
                {new KeyValuePair<Champion, byte>(Champion.Ezreal, 7), new Dictionary<string, byte>
                    {
                        {"Amethyst", 7},
                        {"Meteorite", 10},
                        {"Obsidian", 11},
                        {"Pearl", 12},
                        {"Rose", 13},
                        {"Quartz", 14},
                        {"Ruby", 15},
                        {"Sandstone", 16},
                        {"Striped", 17}
                    }
                },
                {new KeyValuePair<Champion, byte>(Champion.Caitlyn, 0), new Dictionary<string, byte>
                    {
                        {"Default", 0},
                        {"Pink", 7},
                        {"Green", 8},
                        {"Blue", 9}
                    }
                },
                {new KeyValuePair<Champion, byte>(Champion.Lucian, 0), new Dictionary<string, byte>
                    {
                        {"Default", 0},
                        {"Yellow", 3},
                        {"Red", 4},
                        {"Blue", 5}
                    }
                },
                {new KeyValuePair<Champion, byte>(Champion.MissFortune, 7), new Dictionary<string, byte>
                    {
                        {"Amethyst", 7},
                        {"Aquamarine", 11},
                        {"Citrine", 12},
                        {"Peridot", 13},
                        {"Ruby", 14}
                    }
                },
                {new KeyValuePair<Champion, byte>(Champion.Vayne, 3), new Dictionary<string, byte>
                    {
                        {"Default", 3},
                        {"Green", 7},
                        {"Red", 8},
                        {"Silver", 9}
                    }
                },
                {new KeyValuePair<Champion, byte>(Champion.Tristana, 6), new Dictionary<string, byte>
                    {
                        {"Default", 6},
                        {"Navy", 7},
                        {"Purple", 8},
                        {"Orange", 9}
                    }
                }
            };

            Skins = new Dictionary<Champion, Dictionary<string, byte>>
            {
                {Champion.Ashe, new Dictionary<string, byte>
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
                { Champion.Caitlyn, new Dictionary<string, byte>
                {
                    {"Classic Caitlyn", 0},
                    {"Resistance Caitlyn", 1},
                    {"Sheriff Caitlyn", 2},
                    {"Safari Caitlyn", 3},
                    {"Arctic Warfare Caitlyn", 4},
                    {"Officer Caitlyn", 5},
                    {"Headhunter Caitlyn", 6},
                    {"Lunar Wraith Caitlyn", 10}
                }},
                { Champion.Corki, new Dictionary<string, byte>
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
                { Champion.Draven, new Dictionary<string, byte>
                {
                    {"Classic Draven", 0},
                    {"Soul Reaver Draven", 1},
                    {"Gladiator Draven", 2},
                    {"Primetime Draven", 3},
                    {"Pool Party Draven", 4},
                    {"Beast Hunter Draven", 5},
                    {"Draven Draven", 6}
                }},
                { Champion.Ezreal, new Dictionary<string, byte>
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
                { Champion.Graves, new Dictionary<string, byte>
                {
                    {"Classic Graves", 0},
                    {"Hired Gun Graves", 1},
                    {"Jailbreak Graves", 2},
                    {"Mafia Graves", 3},
                    {"Riot Graves", 4},
                    {"Pool Party Graves", 5},
                    {"Cutthroat Graves", 6}
                }},
                { Champion.Jhin, new Dictionary<string, byte>
                {
                    {"Classic Jhin", 0},
                    {"High Noon Jhin", 1}
                }},
                { Champion.Jinx, new Dictionary<string, byte>
                {
                    {"Classic Jinx", 0},
                    {"Mafia Jinx", 1},
                    {"Firecracker Jinx", 2},
                    {"Slayer Jinx", 3},
                    {"Star Guardian Jinx", 4}
                }},
                { Champion.Kalista, new Dictionary<string, byte>
                {
                    {"Classic Kalista", 0},
                    {"Blood Moon Kalista", 1},
                    {"Championship Kalista", 2},
                    {"SKT T1 Kalista", 3}
                }},
                { Champion.KogMaw, new Dictionary<string, byte>
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
                { Champion.Lucian, new Dictionary<string, byte>
                {
                    {"Classic Lucian", 0},
                    {"Hired Gun Lucian", 1},
                    {"Striker Lucian", 2},
                    {"PROJECT: Lucian", 6}
                }},
                { Champion.MissFortune, new Dictionary<string, byte>
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
                { Champion.Quinn, new Dictionary<string, byte>
                {
                    {"Classic Quinn", 0},
                    {"Phoenix Quinn", 1},
                    {"Woad Scout Quinn", 2},
                    {"Corsair Quinn (Taylor Swift)", 3}
                }},
                { Champion.Sivir, new Dictionary<string, byte>
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
                { Champion.Tristana, new Dictionary<string, byte>
                {
                    {"Classic Tristana", 0},
                    {"Riot Girl Tristana", 1},
                    {"Earnest Elf Tristana", 2},
                    {"Firefighter Tristana", 3},
                    {"Guerilla Tristana", 4},
                    {"Buccaneer Tristana", 5},
                    {"Rocket Girl Tristana", 6},
                    {"Dragon Trainer Tristana", 10}
                }},
                { Champion.Twitch, new Dictionary<string, byte>
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
                { Champion.Urgot, new Dictionary<string, byte>
                {
                    {"Classic Urgot", 0},
                    {"Giant Enemy Crabgot", 1},
                    {"Butcher Urgot", 2},
                    {"Battlecast Urgot", 3}
                }},
                { Champion.Varus, new Dictionary<string, byte>
                {
                    {"Classic Varus", 0},
                    {"Blight Crystal Varus", 1},
                    {"Arclight Varus", 2},
                    {"Arctic Ops Varus", 3},
                    {"Heartseeker Varus", 4},
                    {"Varus Swiftbolt", 5},
                    {"Dark Star Varus", 6}
                }},
                {Champion.Vayne, new Dictionary<string, byte>
                {
                    {"Classic Vayne", 0},
                    {"Vindicator Vayne", 1},
                    {"Aristocrat Vayne", 2},
                    {"Dragonslayer Vayne", 3},
                    {"Heartseeker Vayne", 4},
                    {"SKT T1 Vayne", 5},
                    {"Arclight Vayne", 6},
                    {"Soulsteler Vayne", 10}
                }}
            };

            if (!MenuManager.ExtensionsMenu.SubMenus.Any(x => x.UniqueMenuId.Contains("Extension.SkinHack")))
            {
                if (!MainMenu.IsOpen)
                {
                    SkinHackMenu = MenuManager.ExtensionsMenu.AddSubMenu("Skin Hack", "Extension.SkinHack");
                    BuildMenu();
                }
                else MainMenu.OnClose += MainMenu_OnClose;
            }
            else
            {
                var subMenu =
                    MenuManager.ExtensionsMenu.SubMenus.Find(x => x.UniqueMenuId.Contains("Extension.SkinHack"));

                if (subMenu?["SkinId." + Player.Instance.ChampionName] == null)
                    return;

                SkinId = subMenu["SkinId." + Player.Instance.ChampionName].Cast<ComboBox>();
                ChromaId = subMenu["ChromaId." + Player.Instance.ChampionName].Cast<Slider>();

                subMenu["SkinId." + Player.Instance.ChampionName].Cast<ComboBox>().OnValueChange += SkinId_OnValueChange;
                subMenu["ChromaId." + Player.Instance.ChampionName].Cast<Slider>().OnValueChange += ChromaId_OnValueChange;

                UpdateChromaSlider(SkinId.CurrentValue);

                if (HasChromaPack(SkinId.CurrentValue))
                {
                    ChangeSkin(SkinId.CurrentValue, ChromaId.CurrentValue);
                } else ChangeSkin(SkinId.CurrentValue);
            }

            Obj_AI_Base.OnUpdateModel += Obj_AI_Base_OnUpdateModel;
        }

        private void Obj_AI_Base_OnUpdateModel(Obj_AI_Base sender, UpdateModelEventArgs args)
        {
            if (!sender.IsMe || !IsEnabled)
                return;
            

            if (args.Model != BaseSkinNames[Player.Instance.Hero] || args.SkinId != SkinId.CurrentValue)
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

            SkinId = SkinHackMenu.Add("SkinId." + Player.Instance.ChampionName, new ComboBox("Skin : ", skins));
            SkinHackMenu.AddSeparator(5);

            BuildChroma();
        }

        private void BuildChroma()
        {
            ChromaId = SkinHackMenu.Add("ChromaId." + Player.Instance.ChampionName, new Slider("Chroma : "));
            ChromaId.IsVisible = false;
            ChromaId.OnValueChange += ChromaId_OnValueChange;
            SkinId.OnValueChange += SkinId_OnValueChange;

            if (HasChromaPack(SkinId.CurrentValue))
            {
                var dictionary = GetChromaList(SkinId.CurrentValue);

                if (dictionary == null)
                {
                    ChangeSkin(SkinId.CurrentValue);

                    return;
                }
                var maxValue = dictionary.Select(x => x.Key).Count();

                ChromaId.MaxValue = maxValue - 1;

                ChromaId.DisplayName = GetChromaName(SkinId.CurrentValue, ChromaId.CurrentValue);

                ChromaId.IsVisible = true;

                if (Player.Instance.SkinId == 0)
                    ChangeSkin(SkinId.CurrentValue, ChromaId.CurrentValue);
            }
            else if(Player.Instance.SkinId == 0)
                ChangeSkin(SkinId.CurrentValue);
        }

        private void ChromaId_OnValueChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
        {
            var currentId = SkinId.CurrentValue;

            ChromaId.DisplayName = GetChromaName(SkinId.CurrentValue, ChromaId.CurrentValue);
            
            ChangeSkin(currentId, args.NewValue);
        }

        private void UpdateChromaSlider(int id)
        {
            var dictionary = GetChromaList(id);

            if (dictionary == null)
            {
                ChromaId.IsVisible = false;
                return;
            }

            var maxValue = dictionary.Select(x => x.Key).Count();

            ChromaId.MaxValue = maxValue - 1;

            ChromaId.DisplayName = GetChromaName(SkinId.CurrentValue, ChromaId.CurrentValue);

            ChromaId.IsVisible = true;
        }
        
        private void SkinId_OnValueChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
        {
            if (HasChromaPack(args.NewValue))
            {
                UpdateChromaSlider(args.NewValue);

                ChangeSkin(args.NewValue, ChromaId.CurrentValue);
                return;
            }

            ChromaId.IsVisible = false;

            ChangeSkin(args.NewValue);
        }

        private bool HasChromaPack(int id)
            => Chromas != null && Chromas.ContainsKey(new KeyValuePair<Champion, byte>(Player.Instance.Hero, (byte) id));

        private string GetChromaName(int id, int chromaId)
        {
            if (Chromas == null || !Chromas.ContainsKey(new KeyValuePair<Champion, byte>(Player.Instance.Hero, (byte) id)))
                return string.Empty;

            var dictionary = GetChromaList(id);
            var baseSkinName = Skins.FirstOrDefault(x => x.Key == Player.Instance.Hero).Value.FirstOrDefault(x => x.Value == id).Key;

            if (dictionary == null)
                return baseSkinName;

            var chromaIdT = dictionary.ElementAtOrDefault(chromaId).Key;

            return chromaIdT != default(string) ? $"{baseSkinName} : {chromaIdT} chroma" : baseSkinName;
        }

        private Dictionary<string, byte> GetChromaList(int id)
            =>
                !HasChromaPack(id)
                    ? null
                    : Chromas.FirstOrDefault(x => x.Key.Key == Player.Instance.Hero && x.Key.Value == id).Value;

        private void ChangeSkin(int id, int? chromaId = null)
        {
            if (!IsEnabled)
                return;

            if (Skins.All(x => x.Key != Player.Instance.Hero))
            {
                return;
            }
            
            var skins = Skins.FirstOrDefault(x => x.Key == Player.Instance.Hero);

            if (skins.Value == null)
            {
                return;
            }

            var skinId = skins.Value.ElementAtOrDefault(id).Value;

            if (chromaId.HasValue && HasChromaPack(id))
            {
                var dictionary = GetChromaList(id);

                if (dictionary != null)
                {
                    var chromaIdT = dictionary.ElementAtOrDefault(chromaId.Value).Value;

                    if (chromaIdT != 0)
                    {
                        Player.Instance.SetSkin(BaseSkinNames[Player.Instance.Hero], chromaIdT);
                        return;
                    }
                }
            }

            Player.Instance.SetSkin(BaseSkinNames[Player.Instance.Hero], skinId);

            CurrentSkin = skinId;
        }

        public override void Dispose()
        {
            IsEnabled = false;
            
            SkinId.OnValueChange -= SkinId_OnValueChange;
            ChromaId.OnValueChange -= ChromaId_OnValueChange;

            MainMenu.OnClose -= MainMenu_OnClose;

            Obj_AI_Base.OnUpdateModel -= Obj_AI_Base_OnUpdateModel;

            Player.Instance.SetSkin(BaseSkinNames[Player.Instance.Hero], LoadSkinId);
        }
    }
}