using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Owns an entity's grid position and the visual tween between cells.
    /// Shared by the player and enemies. Knows nothing about input or turn
    /// order - TryMove/StepTo commit CurrentCell (and OccupancyGrid) immediately
    /// (grid-state truth) and OnMoveCompleted fires once the visual tween
    /// finishes, which is what the caller should treat as "turn resolved".
    /// </summary>
    public class GridMover : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.13f;

        private FloorData floor;
        private Tilemap tilemap;
        private OccupancyGrid occupancy;

        public Vector2Int CurrentCell { get; private set; }
        public bool IsMoving { get; private set; }

        /// <summary>Raw (un-eased) progress of the current tween, 0..1.</summary>
        public float MoveProgress { get; private set; }

        /// <summary>
        /// Duration in seconds of the grid-to-grid tween. Settable so callers (e.g. an
        /// enemy controller applying EnemyConfigSO.MoveDuration) can override the
        /// serialized default per-instance.
        /// </summary>
        public float MoveDuration
        {
            get => moveDuration;
            set => moveDuration = value;
        }

        public event Action OnMoveCompleted = delegate { };
        public event Action<Vector2Int> OnMoveStarted = delegate { };

        public void Initialize(FloorData floorData, Tilemap tilemapRef, Vector2Int startCell, OccupancyGrid occupancyGrid)
        {
            floor = floorData;
            tilemap = tilemapRef;
            occupancy = occupancyGrid;
            CurrentCell = startCell;
            occupancy.Occupy(startCell);
            transform.position = tilemap.GetCellCenterWorld(new Vector3Int(startCell.x, startCell.y, 0));
        }

        /// <summary>
        /// Validates the step against occupancy/terrain via OccupancyGrid.CanEnter, then
        /// commits and starts the tween via StepTo. Used by input-driven callers (the player)
        /// where the target cell isn't known to be legal in advance.
        /// </summary>
        public bool TryMove(Vector2Int direction)
        {
            if (IsMoving || direction == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int target = CurrentCell + direction;
            if (!occupancy.CanEnter(floor.Grid, target))
            {
                return false;
            }

            StepTo(target);
            return true;
        }

        /// <summary>
        /// Commits CurrentCell and OccupancyGrid to targetCell and starts the visual tween,
        /// WITHOUT validating that the move is legal - the caller is expected to have already
        /// validated it (e.g. via OccupancyGrid.CanEnter driven by its own AI logic). Intended
        /// for enemy controllers that pick an already-validated destination. Still guards against
        /// re-entrancy while a previous tween is in flight (calling this mid-tween would start a
        /// second MoveRoutine driving transform.position concurrently with the first) - that would
        /// be a caller bug, so it's logged rather than silently ignored.
        /// </summary>
        public void StepTo(Vector2Int targetCell)
        {
            if (IsMoving)
            {
                Debug.LogError($"GridMover.StepTo called on {name} while already moving; ignoring targetCell {targetCell}.");
                return;
            }

            Vector2Int direction = targetCell - CurrentCell;

            occupancy.Move(CurrentCell, targetCell);
            CurrentCell = targetCell;
            MoveProgress = 0f;
            OnMoveStarted(direction);
            StartCoroutine(MoveRoutine(tilemap.GetCellCenterWorld(new Vector3Int(targetCell.x, targetCell.y, 0))));
        }

        private IEnumerator MoveRoutine(Vector3 targetWorldPos)
        {
            IsMoving = true;
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                MoveProgress = t;
                float eased = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, targetWorldPos, eased);
                yield return null;
            }

            transform.position = targetWorldPos;
            MoveProgress = 1f;
            IsMoving = false;
            OnMoveCompleted();
        }
    }
}
