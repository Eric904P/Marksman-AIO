namespace Simple_Marksmans.PermaShow.Interfaces
{
    using SharpDX;

    internal interface IPermaShow
    {
        bool IsMoving { get; }
        Vector2 Position { get; }
        ColorBGRA BackgroundColor { get; }
        ColorBGRA SeparatorColor { get; }
        ColorBGRA EnabledUnderlineColor { get; }
        ColorBGRA DisabledUnderlineColor { get; }
        ColorBGRA TextColor { get; }
        bool Enabled { get; }
        int DefaultSpacing { get; }
        int Opacity { get; }
    }
}