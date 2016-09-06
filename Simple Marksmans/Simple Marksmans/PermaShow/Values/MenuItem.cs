namespace Simple_Marksmans.PermaShow.Values
{
    using System;

    using EloBuddy;

    using Interfaces;

    internal class MenuItem : IValue<bool>
    {
        private bool _value;

        public string ItemName { get; set; }
        public string MenuItemName { get; set; }

        public bool Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnValueChange?.Invoke(this, value);
            }
        }

        public event EventHandler<bool> OnValueChange;

        public MenuItem(string itemName, string menuItemName)
        {
            ItemName = itemName;
            MenuItemName = menuItemName;
            Game.OnTick += Game_OnTick;
        }

        private void Game_OnTick(EventArgs args)
        {
            Value = MenuManager.MenuValues[MenuItemName];
        }

        public T Get<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }
    }
}