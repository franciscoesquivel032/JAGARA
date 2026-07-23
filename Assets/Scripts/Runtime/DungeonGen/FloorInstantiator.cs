using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.DungeonGen
{
    public class FloorInstantiator : MonoBehaviour
    {
        private const string SpawnedEntitiesRootName = "SpawnedEntities";

        [SerializeField] private Tilemap tilemap;

        [Header("Tile Assets (TileType -> TileBase)")]
        [SerializeField] private TileBase wallTile;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase corridorTile;
        [SerializeField] private TileBase stairsDownTile;

        [Header("Marker Prefabs")]
        [SerializeField] private GameObject playerSpawnMarkerPrefab;
        [SerializeField] private GameObject enemyMarkerPrefab;
        [SerializeField] private GameObject itemMarkerPrefab;

        public void InstantiateFloor(FloorData floor)
        {
            if (tilemap == null)
            {
                Debug.LogError("FloorInstantiator: Tilemap reference is not assigned.");
                return;
            }

            if (floor == null)
            {
                Debug.LogError("FloorInstantiator: FloorData is null.");
                return;
            }

            ClearPreviousFloor();
            PaintTiles(floor);
            SpawnMarkers(floor);
        }

        private void PaintTiles(FloorData floor)
        {
            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // FloorData's grid has no inherent up/down orientation, so grid (x, y)
                    // maps straight to Tilemap cell (x, y) with no flip. This means the
                    // in-scene layout is vertically mirrored relative to FloorData.ToAsciiArt()'s
                    // printed text (row 0 is top-of-text but bottom-of-world, since +Y is up) -
                    // that mismatch is cosmetic only and not a bug.
                    tilemap.SetTile(new Vector3Int(x, y, 0), GetTileBase(floor.Grid[x, y]));
                }
            }
        }

        private void SpawnMarkers(FloorData floor)
        {
            var spawnedRoot = new GameObject(SpawnedEntitiesRootName).transform;
            spawnedRoot.SetParent(transform, worldPositionStays: false);

            if (floor.PlayerSpawn.HasValue)
            {
                SpawnMarker(playerSpawnMarkerPrefab, floor.PlayerSpawn.Value, spawnedRoot);
            }

            foreach (var pos in floor.EnemySpawnPositions)
            {
                SpawnMarker(enemyMarkerPrefab, pos, spawnedRoot);
            }

            foreach (var pos in floor.ItemSpawnPositions)
            {
                SpawnMarker(itemMarkerPrefab, pos, spawnedRoot);
            }
        }

        private void SpawnMarker(GameObject prefab, Vector2Int gridPos, Transform parent)
        {
            if (prefab == null)
            {
                return;
            }

            Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(gridPos.x, gridPos.y, 0));
            Instantiate(prefab, worldPos, Quaternion.identity, parent);
        }

        private TileBase GetTileBase(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Wall:
                    return wallTile;
                case TileType.Floor:
                    return floorTile;
                case TileType.Corridor:
                    return corridorTile;
                case TileType.StairsDown:
                    return stairsDownTile;
                default:
                    Debug.LogWarning($"FloorInstantiator: unhandled TileType {tileType}; defaulting to wall tile.");
                    return wallTile;
            }
        }

        private void ClearPreviousFloor()
        {
            tilemap.ClearAllTiles();

            // Not a per-frame call - only runs when InstantiateFloor is invoked, so this
            // Find is fine to leave uncached.
            Transform existingRoot = transform.Find(SpawnedEntitiesRootName);
            if (existingRoot != null)
            {
                SafeDestroy(existingRoot.gameObject);
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
