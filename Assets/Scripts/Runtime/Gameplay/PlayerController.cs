using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Enemies;
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
        [SerializeField] private PlayerStatsSO stats;

        [Tooltip("Blocks movement while a menu or dialogue is on screen. A ScriptableObject so the prefab can hold it - scene objects can't be serialized here.")]
        [SerializeField] private GameplayInputGateSO inputGate;

        [Header("Log messages")]
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO itemPickedUpMessage;
        [SerializeField] private MessageTemplateSO inventoryFullMessage;
        [SerializeField] private MessageTemplateSO itemDroppedMessage;
        [SerializeField] private MessageTemplateSO attackMessage;
        [SerializeField] private MessageTemplateSO enemyDefeatedMessage;
        [SerializeField] private MessageTemplateSO playerDefeatedMessage;

        public GridMover Mover => mover;
        public PlayerStatsSO Stats => stats;

        private InputAction moveAction;
        private TurnResolver turnResolver;
        private OccupancyGrid occupancy;
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

            if (stats == null)
            {
                Debug.LogError($"PlayerController on {name}: stats reference is not assigned; the player will have no HP/Paranoia and cannot attack.");
            }
        }

        private void OnEnable()
        {
            moveAction.Enable();
            mover.OnMoveCompleted += HandleMoveCompleted;

            if (stats != null)
            {
                stats.Health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            moveAction.Disable();
            mover.OnMoveCompleted -= HandleMoveCompleted;

            if (stats != null)
            {
                stats.Health.OnDeath -= HandleDeath;
            }

            if (turnResolver != null)
            {
                turnResolver.OnPlayerTurnEnded -= HandleTurnEnded;
            }
        }

        public void Initialize(FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, OccupancyGrid occupancy, Dictionary<Vector2Int, ItemMarker> itemsOnFloor, GameObject itemPrefab)
        {
            turnResolver = resolver;
            this.occupancy = occupancy;
            this.itemsOnFloor = itemsOnFloor;
            this.tilemap = tilemap;
            this.itemPrefab = itemPrefab;
            mover.Initialize(floor, tilemap, startCell, occupancy);

            turnResolver.OnPlayerTurnEnded += HandleTurnEnded;
        }

        private void Update()
        {
            if (mover.IsMoving || (turnResolver != null && turnResolver.IsResolving) || (inputGate != null && inputGate.IsBlocked))
            {
                return;
            }

            Vector2Int direction = CardinalDirectionResolver.Resolve(moveAction.ReadValue<Vector2>());
            if (direction == Vector2Int.zero)
            {
                return;
            }

            Vector2Int target = mover.CurrentCell + direction;
            if (occupancy != null && occupancy.TryGetOccupant(target, out GameObject occupant) && occupant.TryGetComponent(out EnemyController enemy))
            {
                PerformAttack(enemy);
                return;
            }

            mover.TryMove(direction);
        }

        /// <summary>
        /// Resolves a basic bump-attack (no PP cost) against an adjacent enemy
        /// instead of moving into its tile. Ends the turn directly since there's
        /// no move tween to wait for (unlike HandleMoveCompleted's movement path).
        /// </summary>
        private void PerformAttack(EnemyController enemy)
        {
            if (stats == null)
            {
                return;
            }

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(stats.Poder, enemy.Health);

            if (result.DefenderDied)
            {
                Post(enemyDefeatedMessage, StyledName.Enemy(enemy.DisplayName));
                enemy.Die();
            }
            else
            {
                Post(attackMessage, StyledName.Enemy(enemy.DisplayName), result.Damage);
            }

            turnResolver?.EndPlayerTurn();
        }

        /// <summary>Applies one turn's worth of Paranoia gain. Fires from every player action that ends a turn (move, bump-attack, item use).</summary>
        private void HandleTurnEnded()
        {
            stats?.ApplyTurnParanoiaGain();
        }

        /// <summary>
        /// Placeholder death handling: [PENDIENTE] the GDD's nightmare-failure
        /// system (bank loss, Paranoia reset to half) needs the Hub, which
        /// doesn't exist yet. For now, just log it and freeze input.
        /// </summary>
        private void HandleDeath()
        {
            Post(playerDefeatedMessage);
            inputGate?.PushBlock();
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
