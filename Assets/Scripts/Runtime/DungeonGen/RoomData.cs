namespace Jagara.Runtime.DungeonGen
{
    public readonly struct RoomData
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public RoomData(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Right => X + Width;
        public int Bottom => Y + Height;

        // Edge-touching rooms (zero-area intersection) are not considered overlapping.
        public bool Overlaps(RoomData other) =>
            X < other.Right && Right > other.X &&
            Y < other.Bottom && Bottom > other.Y;

        public override string ToString() => $"Room(x:{X}, y:{Y}, w:{Width}, h:{Height})";
    }
}
