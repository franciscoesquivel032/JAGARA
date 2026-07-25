using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Owns the player's grid position and the visual tween between cells.
    /// Knows nothing about input or turn order - TryMove commits CurrentCell
    /// immediately (grid-state truth) and OnMoveCompleted fires once the visual
    /// tween finishes, which is what the caller should treat as "turn resolved".
    /// </summary>
    public class PlayerGridMover : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.13f;

        private FloorData floor;
        private Tilemap tilemap;

        public Vector2Int CurrentCell { get; private set; }
        public bool IsMoving { get; private set; }

        /// <summary>Raw (un-eased) progress of the current tween, 0..1.</summary>
        public float MoveProgress { get; private set; }

        public event Action OnMoveCompleted = delegate { };
        public event Action<Vector2Int> OnMoveStarted = delegate { };

        public void Initialize(FloorData floorData, Tilemap tilemapRef, Vector2Int startCell)
        {
            floor = floorData;
            tilemap = tilemapRef;
            CurrentCell = startCell;
            transform.position = tilemap.GetCellCenterWorld(new Vector3Int(startCell.x, startCell.y, 0));
        }

        public bool TryMove(Vector2Int direction)
        {
            if (IsMoving || direction == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int target = CurrentCell + direction;
            if (!TileVisualResolver.IsOpen(floor.Grid, target.x, target.y))
            {
                return false;
            }

            CurrentCell = target;
            MoveProgress = 0f;
            OnMoveStarted(direction);
            StartCoroutine(MoveRoutine(tilemap.GetCellCenterWorld(new Vector3Int(target.x, target.y, 0))));
            return true;
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
