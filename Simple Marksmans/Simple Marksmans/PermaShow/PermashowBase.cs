namespace Simple_Marksmans.PermaShow
{
    using Interfaces;

    internal abstract class PermashowBase
    {
        internal abstract T AddItem<T>(string uniqueId, T value) where T : IPermaShowItem;
    }
}