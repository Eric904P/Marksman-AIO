namespace Marksman_Master.PermaShow.Values
{
    using System;

    using Interfaces;

    internal class BoolItem : IValue<bool>
    {
        private bool _value;

        public string ItemName { get; set; }

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

        public BoolItem(string itemName, bool value)
        {
            ItemName = itemName;
            _value = value;
        }

        public T Get<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }
    }
}