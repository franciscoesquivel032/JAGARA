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

        // Tuned for the first nightmare's first floor; later nightmares will need
        // different ranges, so these stay parameters rather than hardcoded constants.
        public int MinEnemyCount { get; set; } = 5;
        public int MaxEnemyCount { get; set; } = 8;

        public int MinItemCount { get; set; } = 4;
        public int MaxItemCount { get; set; } = 6;
    }
}
