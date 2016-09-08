namespace Marksman_Master.PermaShow.Interfaces
{
    using System;

    internal interface IValue<T> :  IPermaShowItem
    {
        string ItemName { get; set; }
        T Value { get; set; }

        event EventHandler<T> OnValueChange;
    }
}