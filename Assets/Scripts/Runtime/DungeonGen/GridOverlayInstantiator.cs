using Jagara.Runtime.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.DungeonGen
{
    /// <summary>
    /// Draws a cell-border overlay on top of the terrain tilemap, covering only
    /// walkable cells (Floor/Corridor/StairsDown). Lives on its own Tilemap so it
    /// can be layered above the terrain purely via Sorting Order, and keeps its
    /// transform aligned to the terrain tilemap at Start.
    ///
    /// Each cell always draws its West+South edges; it additionally draws its
    /// East/North edges only when that neighbor isn't walkable. This way every
    /// boundary - whether between two floor cells or against a wall - is drawn
    /// by exactly one cell, so lines never double up at shared edges.
    /// </summary>
    public class GridOverlayInstantiator : MonoBehaviour
    {
        [SerializeField] private Tilemap overlayTilemap;
        [SerializeField] private Tilemap terrainTilemap;

        [Header("West+South border, extended per open neighbor")]
        [SerializeField] private TileBase tileBase;
        [SerializeField] private TileBase tileEast;
        [SerializeField] private TileBase tileNorth;
        [SerializeField] private TileBase tileNorthEast;

        private void Start()
        {
            if (overlayTilemap == null || terrainTilemap == null)
            {
                Debug.LogError("GridOverlayInstantiator: overlayTilemap/terrainTilemap reference is not assigned.");
                return;
            }

            overlayTilemap.transform.position = terrainTilemap.transform.position;
        }

        public void RenderOverlay(FloorData floor)
        {
            if (overlayTilemap == null)
            {
                Debug.LogError("GridOverlayInstantiator: overlayTilemap reference is not assigned.");
                return;
            }

            if (tileBase == null || tileEast == null || tileNorth == null || tileNorthEast == null)
            {
                Debug.LogError("GridOverlayInstantiator: one or more border tile references is not assigned.");
                return;
            }

            if (floor == null)
            {
                Debug.LogError("GridOverlayInstantiator: FloorData is null.");
                return;
            }

            overlayTilemap.ClearAllTiles();

            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (TileVisualResolver.IsOpen(floor.Grid, x, y))
                    {
                        overlayTilemap.SetTile(new Vector3Int(x, y, 0), ResolveBorderTile(floor.Grid, x, y));
                    }
                }
            }
        }

        private TileBase ResolveBorderTile(TileType[,] grid, int x, int y)
        {
            bool needsEast = !TileVisualResolver.IsOpen(grid, x + 1, y);
            bool needsNorth = !TileVisualResolver.IsOpen(grid, x, y + 1);

            if (needsEast && needsNorth) return tileNorthEast;
            if (needsEast) return tileEast;
            if (needsNorth) return tileNorth;
            return tileBase;
        }
    }
}
