namespace Marksman_Master.Extensions.SkinHack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EloBuddy;
    using EloBuddy.SDK.Menu;
    using EloBuddy.SDK.Menu.Values;

    internal sealed class SkinHack : ExtensionBase, IDisposable
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
                    {"Lunar Wraith Caitlyn", 7},
                    {"PROJECT: Ashe", 8}
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
                MainMenu.OnClose += MainMenu_OnClose;
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
            if (!sender.IsMe)
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

            Player.Instance.SetSkin(Player.Instance.BaseSkinName, SkinId.CurrentValue);
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

        ~SkinHack()
        {
            Dispose();
        }

        public override void Dispose()
        {
            IsEnabled = false;
            
            SkinId.OnValueChange -= SkinId_OnValueChange;
            MainMenu.OnClose -= MainMenu_OnClose;

            GC.SuppressFinalize(this);
        }
    }
}