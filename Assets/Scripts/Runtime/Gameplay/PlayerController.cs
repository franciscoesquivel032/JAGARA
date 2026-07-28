using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.TurnSystem;
using Jagara.Runtime.UI;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Reads grid-movement input and drives GridMover. Input is resolved
    /// fresh every Update, so holding a direction produces continuous stepping
    /// once the mover's current tween finishes - no explicit input queue needed.
    /// </summary>
    [RequireComponent(typeof(GridMover))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GridMover mover;
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private InventorySO inventory;

        public GridMover Mover => mover;

        private InputAction moveAction;
        private TurnResolver turnResolver;
        private ActionMenuController actionMenu;
        private Dictionary<Vector2Int, ItemMarker> itemsOnFloor;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Gameplay", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);

            if (inventory == null)
            {
                Debug.LogError($"PlayerController on {name}: inventory reference is not assigned; picked-up items will be lost.");
            }
        }

        private void OnEnable()
        {
            moveAction.Enable();
            mover.OnMoveCompleted += HandleMoveCompleted;
        }

        private void OnDisable()
        {
            moveAction.Disable();
            mover.OnMoveCompleted -= HandleMoveCompleted;
        }

        public void Initialize(FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, OccupancyGrid occupancy, ActionMenuController menu, Dictionary<Vector2Int, ItemMarker> itemsOnFloor)
        {
            turnResolver = resolver;
            actionMenu = menu;
            this.itemsOnFloor = itemsOnFloor;
            mover.Initialize(floor, tilemap, startCell, occupancy);
        }

        private void Update()
        {
            if (mover.IsMoving || (turnResolver != null && turnResolver.IsResolving) || (actionMenu != null && actionMenu.IsOpen))
            {
                return;
            }

            Vector2Int direction = CardinalDirectionResolver.Resolve(moveAction.ReadValue<Vector2>());
            if (direction != Vector2Int.zero)
            {
                mover.TryMove(direction);
            }
        }

        private void HandleMoveCompleted()
        {
            TryPickUpItem();
            turnResolver?.EndPlayerTurn();
        }

        /// <summary>
        /// Auto-pickup on tile entry, per the GDD: an item on the tile the player
        /// just stepped onto is added to the inventory with no separate action.
        /// If the inventory is full, the item is simply left on the floor - no
        /// drop/swap prompt (that would be a design addition beyond this task).
        /// </summary>
        private void TryPickUpItem()
        {
            if (itemsOnFloor == null || inventory == null)
            {
                return;
            }

            if (!itemsOnFloor.TryGetValue(mover.CurrentCell, out ItemMarker marker))
            {
                return;
            }

            if (!inventory.TryAddItem(marker.Item))
            {
                return;
            }

            itemsOnFloor.Remove(mover.CurrentCell);
            Destroy(marker.gameObject);
        }
    }
}
