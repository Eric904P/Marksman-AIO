namespace Marksman_Master.PermaShow
{
    using Interfaces;

    using SharpDX;

    internal class Separator : IDrawable
    {
        public ColorBGRA Color { get; set; }
        public uint Width;
        public Vector2[] Positions { get; set; }
    }
}