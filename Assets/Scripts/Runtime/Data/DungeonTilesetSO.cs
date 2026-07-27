using System.Collections.Generic;
using Jagara.Runtime.DungeonGen;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Visual tileset for a dungeon theme: one tile per wall shape, weighted
    /// floor variants (shared by rooms and corridors), and the staircase tile.
    /// FloorInstantiator resolves shapes/variants via TileVisualResolver and
    /// looks the tiles up here.
    /// </summary>
    [CreateAssetMenu(fileName = "New Dungeon Tileset", menuName = "Jagara/Dungeon Tileset")]
    public class DungeonTilesetSO : ScriptableObject
    {
        [System.Serializable]
        public struct WeightedDecoration
        {
            public TileBase tile;
            [Min(0)] public int weight;
        }

        [Header("Wall - Fill variants (weighted; unassigned fall back to A)")]
        [SerializeField] private TileBase wallFill;
        [SerializeField] private TileBase wallFillB;
        [SerializeField] private TileBase wallFillC;
        [SerializeField] private TileBase wallFillD;
        [SerializeField, Min(0)] private int wallFillWeightA = 25;
        [SerializeField, Min(0)] private int wallFillWeightB = 25;
        [SerializeField, Min(0)] private int wallFillWeightC = 25;
        [SerializeField, Min(0)] private int wallFillWeightD = 25;

        [Header("Wall - Edges (named by the side the floor is on)")]
        [SerializeField] private TileBase wallEdgeN;
        [SerializeField] private TileBase wallEdgeS;
        [SerializeField] private TileBase wallEdgeE;
        [SerializeField] private TileBase wallEdgeW;

        [Header("Wall - Outer corners")]
        [SerializeField] private TileBase wallOuterNE;
        [SerializeField] private TileBase wallOuterNW;
        [SerializeField] private TileBase wallOuterSE;
        [SerializeField] private TileBase wallOuterSW;

        [Header("Wall - Inner corners")]
        [SerializeField] private TileBase wallInnerNE;
        [SerializeField] private TileBase wallInnerNW;
        [SerializeField] private TileBase wallInnerSE;
        [SerializeField] private TileBase wallInnerSW;

        [Header("Floor Variants")]
        [SerializeField] private TileBase floorPrimary;
        [SerializeField] private TileBase floorSecondary;
        [SerializeField] private TileBase floorTertiary;
        [SerializeField, Min(0)] private int primaryWeight = 75;
        [SerializeField, Min(0)] private int secondaryWeight = 20;
        [SerializeField, Min(0)] private int tertiaryWeight = 5;

        [Header("Decorations (painted on a separate tilemap over walkable ground)")]
        [SerializeField] private List<WeightedDecoration> decorations = new();
        [SerializeField, Range(0, 100)] private int decorationDensityPercent = 10;

        [Header("Special")]
        [SerializeField] private TileBase stairsDownTile;

        public TileBase StairsDownTile => stairsDownTile;
        public int PrimaryWeight => primaryWeight;
        public int SecondaryWeight => secondaryWeight;
        public int TertiaryWeight => tertiaryWeight;
        public int WallFillWeightA => wallFillWeightA;
        public int WallFillWeightB => wallFillWeightB;
        public int WallFillWeightC => wallFillWeightC;
        public int WallFillWeightD => wallFillWeightD;
        public IReadOnlyList<WeightedDecoration> Decorations => decorations;
        public int DecorationDensityPercent => decorationDensityPercent;

        public TileBase GetWallTile(WallShape shape)
        {
            switch (shape)
            {
                case WallShape.EdgeN:
                    return wallEdgeN;
                case WallShape.EdgeS:
                    return wallEdgeS;
                case WallShape.EdgeE:
                    return wallEdgeE;
                case WallShape.EdgeW:
                    return wallEdgeW;
                case WallShape.OuterNE:
                    return wallOuterNE;
                case WallShape.OuterNW:
                    return wallOuterNW;
                case WallShape.OuterSE:
                    return wallOuterSE;
                case WallShape.OuterSW:
                    return wallOuterSW;
                case WallShape.InnerNE:
                    return wallInnerNE;
                case WallShape.InnerNW:
                    return wallInnerNW;
                case WallShape.InnerSE:
                    return wallInnerSE;
                case WallShape.InnerSW:
                    return wallInnerSW;
                default:
                    return wallFill;
            }
        }

        /// <summary>
        /// Wall fill variant lookup (0 = A ... 3 = D). Variants without an
        /// assigned tile fall back to variant A so the dungeon renders before
        /// all four assets exist.
        /// </summary>
        public TileBase GetWallFillVariantTile(int variantIndex)
        {
            TileBase tile;
            switch (variantIndex)
            {
                case 1:
                    tile = wallFillB;
                    break;
                case 2:
                    tile = wallFillC;
                    break;
                case 3:
                    tile = wallFillD;
                    break;
                default:
                    tile = wallFill;
                    break;
            }

            return tile != null ? tile : wallFill;
        }

        public TileBase GetFloorVariantTile(int variantIndex)
        {
            switch (variantIndex)
            {
                case 1:
                    return floorSecondary;
                case 2:
                    return floorTertiary;
                default:
                    return floorPrimary;
            }
        }
    }
}
