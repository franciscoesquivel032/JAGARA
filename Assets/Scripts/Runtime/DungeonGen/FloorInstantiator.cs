using Jagara.Runtime.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.DungeonGen
{
    public class FloorInstantiator : MonoBehaviour
    {
        private const string SpawnedEntitiesRootName = "SpawnedEntities";

        [SerializeField] private Tilemap tilemap;
        [SerializeField] private Tilemap decorationTilemap;

        [Header("Visual Tileset")]
        [SerializeField] private DungeonTilesetSO tileset;

        [Header("Marker Prefabs")]
        [SerializeField] private GameObject itemMarkerPrefab;

        public Tilemap Tilemap => tilemap;
        public Tilemap DecorationTilemap => decorationTilemap;

        public void InstantiateFloor(FloorData floor)
        {
            if (tilemap == null)
            {
                Debug.LogError("FloorInstantiator: Tilemap reference is not assigned.");
                return;
            }

            if (tileset == null)
            {
                Debug.LogError("FloorInstantiator: DungeonTilesetSO reference is not assigned.");
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

        public void ApplyEnvironmentTint(NightmareThemeSO theme)
        {
            if (tilemap == null)
            {
                Debug.LogError("FloorInstantiator: Tilemap reference is not assigned.");
                return;
            }

            if (theme == null)
            {
                Debug.LogError("FloorInstantiator: theme is null; environment tint left unchanged.");
                return;
            }

            tilemap.color = theme.EnvironmentTint;
            if (decorationTilemap != null)
            {
                decorationTilemap.color = theme.EnvironmentTint;
            }
        }

        private void PaintTiles(FloorData floor)
        {
            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);
            int[] decorationWeights = BuildDecorationWeights();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // FloorData's grid has no inherent up/down orientation, so grid (x, y)
                    // maps straight to Tilemap cell (x, y) with no flip. This means the
                    // in-scene layout is vertically mirrored relative to FloorData.ToAsciiArt()'s
                    // printed text (row 0 is top-of-text but bottom-of-world, since +Y is up) -
                    // that mismatch is cosmetic only and not a bug.
                    tilemap.SetTile(new Vector3Int(x, y, 0), GetTileBase(floor, x, y));
                    PaintDecoration(floor, x, y, decorationWeights);
                }
            }
        }

        /// <summary>
        /// Weights array built once per floor so the per-cell decoration roll
        /// allocates nothing. Null when decorations are unconfigured or unusable
        /// (also null - with an error - when configured but the tilemap is missing).
        /// </summary>
        private int[] BuildDecorationWeights()
        {
            var decorations = tileset.Decorations;
            if (decorations.Count == 0)
            {
                return null;
            }

            if (decorationTilemap == null)
            {
                Debug.LogError("FloorInstantiator: tileset has decorations but decorationTilemap is not assigned.");
                return null;
            }

            var weights = new int[decorations.Count];
            for (int i = 0; i < decorations.Count; i++)
            {
                weights[i] = decorations[i].weight;
            }

            return weights;
        }

        private void PaintDecoration(FloorData floor, int x, int y, int[] weights)
        {
            if (weights == null)
            {
                return;
            }

            TileType tileType = floor.Grid[x, y];
            if (tileType != TileType.Floor && tileType != TileType.Corridor)
            {
                return;
            }

            int index = TileVisualResolver.ResolveDecorationIndex(
                floor.Seed, x, y, tileset.DecorationDensityPercent, weights);
            if (index < 0)
            {
                return;
            }

            TileBase tile = tileset.Decorations[index].tile;
            if (tile != null)
            {
                decorationTilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private void SpawnMarkers(FloorData floor)
        {
            var spawnedRoot = new GameObject(SpawnedEntitiesRootName).transform;
            spawnedRoot.SetParent(transform, worldPositionStays: false);

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

        private TileBase GetTileBase(FloorData floor, int x, int y)
        {
            TileType tileType = floor.Grid[x, y];
            switch (tileType)
            {
                case TileType.Wall:
                    WallShape shape = TileVisualResolver.ResolveWallShape(floor.Grid, x, y);
                    if (shape == WallShape.Fill)
                    {
                        return tileset.GetWallFillVariantTile(TileVisualResolver.ResolveWallFillVariant(
                            floor.Seed, x, y,
                            tileset.WallFillWeightA, tileset.WallFillWeightB,
                            tileset.WallFillWeightC, tileset.WallFillWeightD));
                    }

                    return tileset.GetWallTile(shape);
                case TileType.Floor:
                case TileType.Corridor:
                    // Corridors intentionally share the room floor pool (PMD-style
                    // unified ground); the data model still distinguishes them.
                    return tileset.GetFloorVariantTile(TileVisualResolver.ResolveFloorVariant(
                        floor.Seed, x, y,
                        tileset.PrimaryWeight, tileset.SecondaryWeight, tileset.TertiaryWeight));
                case TileType.StairsDown:
                    return tileset.StairsDownTile;
                default:
                    Debug.LogWarning($"FloorInstantiator: unhandled TileType {tileType}; defaulting to wall fill tile.");
                    return tileset.GetWallTile(WallShape.Fill);
            }
        }

        private void ClearPreviousFloor()
        {
            tilemap.ClearAllTiles();

            if (decorationTilemap != null)
            {
                decorationTilemap.ClearAllTiles();
            }

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
