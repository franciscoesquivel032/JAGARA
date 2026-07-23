namespace Jagara.Runtime.DungeonGen
{
    /// <summary>
    /// Visual shape of a wall tile, derived from which neighboring cells are open
    /// (walkable) ground. Edge names refer to the side the open ground is on
    /// (EdgeN = floor to the north). Outer corners have two adjacent open cardinals;
    /// inner corners have only a diagonal open.
    /// Shapes without dedicated art (1-wide slivers, caps, pillars) currently fall
    /// back to the nearest of these 13; giving them dedicated art later only means
    /// new enum values, new rows in TileVisualResolver's lookup, and new tileset
    /// slots - no architectural change.
    /// </summary>
    public enum WallShape
    {
        Fill,
        EdgeN,
        EdgeS,
        EdgeE,
        EdgeW,
        OuterNE,
        OuterNW,
        OuterSE,
        OuterSW,
        InnerNE,
        InnerNW,
        InnerSE,
        InnerSW,
    }
}
