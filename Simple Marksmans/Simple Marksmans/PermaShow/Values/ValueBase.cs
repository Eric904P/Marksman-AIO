namespace Simple_Marksmans.PermaShow.Values
{
    using SharpDX;

    internal class ValueBase
    {
        public string UniqueId { get; }
        public Color Color { get; }
        public Text ItemNameText { get; }
        public Text ItemValueText { get; }
        
        public ValueBase(string uniqueId, Text itemNameText, Text itemValueText, Color color)
        {
            UniqueId = uniqueId;
            ItemNameText = itemNameText;
            ItemValueText = itemValueText;
            Color = color;
        }
    }
}