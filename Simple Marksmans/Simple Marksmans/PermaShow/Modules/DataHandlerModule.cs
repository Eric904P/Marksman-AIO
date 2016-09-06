namespace Simple_Marksmans.PermaShow.Modules
{
    using System.Collections.Generic;
    
    using Values;

    using Interfaces;

    internal sealed class DataHandlerModule : Wrapper
    {
        public Dictionary<ValueBase, IPermaShowItem> PermaShowItems { get; set; } = new Dictionary<ValueBase, IPermaShowItem>();
        public List<Separator> Separators { get; set; } = new List<Separator>();
        public List<Separator> Underlines { get; set; } = new List<Separator>();
        public Text HeaderText { get; set; }
        
        public override void Load()
        {
        }

        public void AddPermashowItem(KeyValuePair<ValueBase, IPermaShowItem> item)
        {
            PermaShowItems.Add(item.Key, item.Value);
        }

        public void AddSeparator(Separator separator)
        {
            Separators.Add(separator);
        }

        public void AddUnderline(Separator underline)
        {
            Underlines.Add(underline);
        }
    }
}