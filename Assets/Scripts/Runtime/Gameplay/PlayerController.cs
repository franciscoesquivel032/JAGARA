using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Narrative;
using Jagara.Runtime.TurnSystem;

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

        [Tooltip("Blocks movement while a menu or dialogue is on screen. A ScriptableObject so the prefab can hold it - scene objects can't be serialized here.")]
        [SerializeField] private GameplayInputGateSO inputGate;

        [Header("Log messages")]
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO itemPickedUpMessage;
        [SerializeField] private MessageTemplateSO inventoryFullMessage;
        [SerializeField] private MessageTemplateSO itemDroppedMessage;

        public GridMover Mover => mover;

        private InputAction moveAction;
        private TurnResolver turnResolver;
        private Dictionary<Vector2Int, ItemMarker> itemsOnFloor;
        private Tilemap tilemap;
        private GameObject itemPrefab;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Gameplay", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);

            if (inventory == null)
            {
                Debug.LogError($"PlayerController on {name}: inventory reference is not assigned; picked-up items will be lost.");
            }

            if (inputGate == null)
            {
                Debug.LogError($"PlayerController on {name}: inputGate reference is not assigned; the player will keep moving while menus and dialogue are open.");
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

        public void Initialize(FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, OccupancyGrid occupancy, Dictionary<Vector2Int, ItemMarker> itemsOnFloor, GameObject itemPrefab)
        {
            turnResolver = resolver;
            this.itemsOnFloor = itemsOnFloor;
            this.tilemap = tilemap;
            this.itemPrefab = itemPrefab;
            mover.Initialize(floor, tilemap, startCell, occupancy);
        }

        private void Update()
        {
            if (mover.IsMoving || (turnResolver != null && turnResolver.IsResolving) || (inputGate != null && inputGate.IsBlocked))
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
        /// drop/swap prompt (that would be a design addition beyond this task) -
        /// but the player is told why, since a silent failure is indistinguishable
        /// from the pickup not being implemented.
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

            ItemSO item = marker.Item;
            if (!inventory.TryAddItem(item))
            {
                Post(inventoryFullMessage, StyledName.Item(item.DisplayName));
                return;
            }

            itemsOnFloor.Remove(mover.CurrentCell);
            Destroy(marker.gameObject);
            Post(itemPickedUpMessage, StyledName.Item(item.DisplayName));
        }

        /// <summary>
        /// Posts a log message if this player has a log wired up. Logging is
        /// optional wiring, not a hard dependency - a missing log should cost the
        /// player a line of text, not break movement or pickups.
        /// </summary>
        private void Post(MessageTemplateSO template, params object[] args)
        {
            if (messageLog == null || template == null)
            {
                return;
            }

            messageLog.Post(template, args);
        }

        /// <summary>
        /// Drops the inventory item at slotIndex onto the player's current
        /// tile. Fails (no side effects) if the slot is empty/invalid or the
        /// tile already has an item on it - callers are responsible for
        /// surfacing that failure (e.g. keeping a confirmation panel open).
        /// </summary>
        public bool TryDropItem(int slotIndex)
        {
            if (inventory == null || itemsOnFloor == null)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= inventory.Slots.Count || inventory.Slots[slotIndex] == null)
            {
                return false;
            }

            Vector2Int cell = mover.CurrentCell;
            if (itemsOnFloor.ContainsKey(cell))
            {
                return false;
            }

            if (itemPrefab == null)
            {
                Debug.LogError($"PlayerController on {name}: itemPrefab reference is not assigned; cannot drop items.");
                return false;
            }

            ItemSO item = inventory.Slots[slotIndex];
            Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            GameObject itemInstance = Instantiate(itemPrefab, worldPos, Quaternion.identity);
            var marker = itemInstance.GetComponent<ItemMarker>();
            if (marker == null)
            {
                Debug.LogError("PlayerController: itemPrefab is missing an ItemMarker component.");
                Destroy(itemInstance);
                return false;
            }

            marker.Initialize(item);
            itemsOnFloor[cell] = marker;
            inventory.RemoveAt(slotIndex);
            Post(itemDroppedMessage, StyledName.Item(item.DisplayName));
            return true;
        }
    }
}
