namespace Simple_Marksmans.PermaShow.Values
{
    using System;

    using Interfaces;

    internal class StringItem : IValue<string>
    {
        private string _value;

        public string ItemName { get; set; }

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnValueChange?.Invoke(this, value);
            }
        }

        public event EventHandler<string> OnValueChange;

        public StringItem(string itemName, string itemValue)
        {
            ItemName = itemName;
            _value = itemValue;
        }
        
        public T Get<T>()
        {
            return (T)Convert.ChangeType(this, typeof(T));
        }
    }
}