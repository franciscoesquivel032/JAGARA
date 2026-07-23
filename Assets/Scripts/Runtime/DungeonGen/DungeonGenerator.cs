using System;
using System.Collections.Generic;

namespace Jagara.Runtime.DungeonGen
{
    public class DungeonGenerator
    {
        private const int MaxAttemptsPerRoom = 100;

        /// <summary>
        /// Generates a set of non-overlapping rectangular rooms within the grid bounds
        /// described by <paramref name="parameters"/>. Deterministic: the same seed and
        /// params always produce the same room list.
        ///
        /// The returned count is structurally guaranteed to never exceed MaxRoomCount.
        /// It is NOT mathematically guaranteed to reach MinRoomCount for arbitrary params —
        /// this is naive random-placement-with-retry, so pathological params (e.g. room
        /// sizes close to the grid size) can legitimately terminate early with fewer rooms
        /// once free space fragments. Callers should leave generous headroom between room
        /// sizes/counts and grid size if the lower bound matters to them.
        /// </summary>
        public List<RoomData> GenerateRooms(int seed, DungeonGenerationParams parameters)
        {
            ValidateParams(parameters);

            var rng = new Random(seed);
            var rooms = new List<RoomData>();

            int targetCount = rng.Next(parameters.MinRoomCount, parameters.MaxRoomCount + 1);
            int maxTotalAttempts = targetCount * MaxAttemptsPerRoom;
            int attempts = 0;

            while (rooms.Count < targetCount && attempts < maxTotalAttempts)
            {
                attempts++;

                int width = rng.Next(parameters.MinRoomWidth, parameters.MaxRoomWidth + 1);
                int height = rng.Next(parameters.MinRoomHeight, parameters.MaxRoomHeight + 1);
                int x = rng.Next(0, parameters.GridWidth - width + 1);
                int y = rng.Next(0, parameters.GridHeight - height + 1);

                var candidate = new RoomData(x, y, width, height);

                if (!OverlapsAny(candidate, rooms))
                {
                    rooms.Add(candidate);
                }
            }

            return rooms;
        }

        private static bool OverlapsAny(RoomData candidate, List<RoomData> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                if (candidate.Overlaps(rooms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateParams(DungeonGenerationParams p)
        {
            if (p.MinRoomCount < 1 || p.MaxRoomCount < p.MinRoomCount)
                throw new ArgumentException("Invalid room count range.", nameof(p));

            if (p.MinRoomWidth < 1 || p.MaxRoomWidth < p.MinRoomWidth)
                throw new ArgumentException("Invalid room width range.", nameof(p));

            if (p.MinRoomHeight < 1 || p.MaxRoomHeight < p.MinRoomHeight)
                throw new ArgumentException("Invalid room height range.", nameof(p));

            if (p.MaxRoomWidth > p.GridWidth || p.MaxRoomHeight > p.GridHeight)
                throw new ArgumentException("Room size exceeds grid bounds.", nameof(p));
        }
    }
}
