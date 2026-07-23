namespace Jagara.Runtime.DungeonGen
{
    public class DungeonGenerationParams
    {
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }

        public int MinRoomCount { get; set; }
        public int MaxRoomCount { get; set; }

        public int MinRoomWidth { get; set; }
        public int MaxRoomWidth { get; set; }

        public int MinRoomHeight { get; set; }
        public int MaxRoomHeight { get; set; }
    }
}
