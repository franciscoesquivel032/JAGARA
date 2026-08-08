using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Enemies;
using Jagara.Runtime.Narrative;
using Jagara.Runtime.Narrative.Dialogue;
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
        [SerializeField] private MessageTemplateSO incomingAttackMessage;
        [SerializeField] private MessageTemplateSO incomingMultiAttackMessage;

        [Header("Defeat")]
        [Tooltip("Spoken by the Voice when the player's HP reaches 0. A dialogue rather than a log line - death is a narrative beat, not another combat entry scrolling past.")]
        [SerializeField] private DialogueSO playerDefeatedDialogue;

        public GridMover Mover => mover;
        public PlayerStatsSO Stats => stats;

        private HealthBarBinder healthBarBinder;
        private ParanoiaBarBinder paranoiaBarBinder;
        private HpPopupBinder hpPopupBinder;
        private GridVisualAnimator visualAnimator;

        // Scene object, so it cannot be serialized into this prefab - threaded
        // in through Initialize, the same way dialoguePanel is.
        private CameraShake cameraShake;

        private InputAction moveAction;

        // True from PerformAttack starting the lunge until its completion
        // callback fires. Joins mover.IsMoving in Update's gate so a held
        // key can't commit a second action while the lunge is still playing.
        private bool attackAnimationInProgress;

        // Keeps a bump-attack to one action per press. Without it a held key
        // commits an attack (and a full turn of enemy retaliation) every frame,
        // because - unlike a move - an attack has no tween to gate on.
        private readonly BumpAttackLatch attackLatch = new();

        private TurnResolver turnResolver;
        private OccupancyGrid occupancy;
        private Dictionary<Vector2Int, ItemMarker> itemsOnFloor;
        private Tilemap tilemap;
        private GameObject itemPrefab;

        // Scene object, so it cannot be serialized into this prefab - threaded in
        // through Initialize, the same way the tilemap and occupancy grid are.
        private DialoguePanelController dialoguePanel;

        // Set the instant HP hits 0, consumed when the turn finishes. Death is
        // raised from inside an enemy's attack, halfway through turn resolution,
        // but the player has to read the blow that killed them before the Voice
        // speaks over it - so only the input freeze happens immediately.
        private bool deathPending;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Gameplay", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);

            healthBarBinder = GetComponentInChildren<HealthBarBinder>();
            paranoiaBarBinder = GetComponentInChildren<ParanoiaBarBinder>();
            hpPopupBinder = GetComponentInChildren<HpPopupBinder>();
            visualAnimator = GetComponentInChildren<GridVisualAnimator>();

            if (healthBarBinder == null)
            {
                Debug.LogError($"PlayerController on {name}: no HealthBarBinder found in children; the player will have no health bar.");
            }

            if (paranoiaBarBinder == null)
            {
                Debug.LogError($"PlayerController on {name}: no ParanoiaBarBinder found in children; the player will have no paranoia bar.");
            }

            if (hpPopupBinder == null)
            {
                Debug.LogError($"PlayerController on {name}: no HpPopupBinder found in children; the player will show no HP popups.");
            }

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
        }

        private void OnDisable()
        {
            moveAction.Disable();
            mover.OnMoveCompleted -= HandleMoveCompleted;

            if (stats != null && stats.Health != null)
            {
                stats.Health.OnDeath -= HandleDeath;
                stats.Health.OnHPChanged -= HandleHPChanged;
                healthBarBinder?.Unbind();
                paranoiaBarBinder?.Unbind();
                hpPopupBinder?.Unbind();
            }

            if (turnResolver != null)
            {
                turnResolver.OnPlayerTurnEnded -= HandleTurnEnded;
            }
        }

        public void Initialize(FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, OccupancyGrid occupancy, Dictionary<Vector2Int, ItemMarker> itemsOnFloor, GameObject itemPrefab, DialoguePanelController dialoguePanel, CameraShake cameraShake)
        {
            turnResolver = resolver;
            this.occupancy = occupancy;
            this.itemsOnFloor = itemsOnFloor;
            this.tilemap = tilemap;
            this.itemPrefab = itemPrefab;
            this.dialoguePanel = dialoguePanel;
            this.cameraShake = cameraShake;
            attackLatch.Clear();
            attackAnimationInProgress = false;
            deathPending = false;
            mover.Initialize(floor, tilemap, startCell, occupancy);

            turnResolver.OnPlayerTurnEnded += HandleTurnEnded;

            BindStats();
        }

        /// <summary>
        /// Subscribes to Health/Paranoia and binds the HP/Paranoia bars. Called
        /// from Initialize rather than OnEnable: OnEnable fires implicitly the
        /// instant NightmareBootstrap.SpawnPlayer's Instantiate() call runs,
        /// with no guarantee PlayerStatsSO.ResetRuntimeState() (which creates
        /// Health/Paranoia) has completed by then - it races, and losing that
        /// race means stats.Health is null here, throwing and silently
        /// aborting the rest of OnEnable (Unity swallows exceptions thrown
        /// from lifecycle methods), which permanently orphans both bars for
        /// the rest of the session. Initialize is called explicitly by
        /// SpawnPlayer strictly after NightmareBootstrap.Start() has already
        /// called ResetRuntimeState, so there is no ordering ambiguity here.
        /// </summary>
        private void BindStats()
        {
            if (stats == null)
            {
                return;
            }

            if (stats.Health == null || stats.Paranoia == null)
            {
                Debug.LogError($"PlayerController on {name}: stats.Health/Paranoia is still null at Initialize; HP/Paranoia bars will not be bound.");
                return;
            }

            stats.Health.OnDeath += HandleDeath;
            stats.Health.OnHPChanged += HandleHPChanged;
            healthBarBinder?.Bind(stats.Health);
            paranoiaBarBinder?.Bind(stats.Paranoia);
            hpPopupBinder?.Bind(stats.Health);
        }

        private void Update()
        {
            Vector2Int direction = CardinalDirectionResolver.Resolve(moveAction.ReadValue<Vector2>());

            // Observed before the "can I act?" checks below, so a key released while
            // a tween or an enemy turn is still resolving still clears the latch -
            // otherwise the player's next press would be swallowed.
            attackLatch.Observe(direction);

            if (mover.IsMoving || attackAnimationInProgress || (turnResolver != null && turnResolver.IsResolving) || (inputGate != null && inputGate.IsBlocked))
            {
                return;
            }

            if (direction == Vector2Int.zero || attackLatch.IsHeld)
            {
                return;
            }

            Vector2Int target = mover.CurrentCell + direction;
            if (TryGetAttackTarget(target, out EnemyController enemy))
            {
                // Attacking and moving are separate actions: latching this direction
                // is what stops the same held key from also walking the player into
                // the tile once the enemy dies and vacates it.
                attackLatch.Latch(direction);
                PerformAttack(enemy, direction);
                return;
            }

            mover.TryMove(direction);
        }

        /// <summary>
        /// True when <paramref name="cell"/> holds a living enemy - i.e. input toward
        /// it means "attack", not "step". An enemy destroyed earlier this frame can
        /// still be listed in the occupancy grid (Destroy is deferred to end of frame)
        /// and a dying one can still be mid-teardown, so the destroyed/dead checks here
        /// are what keep a corpse from soaking a second attack and a second turn.
        /// </summary>
        private bool TryGetAttackTarget(Vector2Int cell, out EnemyController enemy)
        {
            enemy = null;

            if (occupancy == null || !occupancy.TryGetOccupant(cell, out GameObject occupant) || occupant == null)
            {
                return false;
            }

            return occupant.TryGetComponent(out enemy) && enemy.Health != null && !enemy.Health.IsDead;
        }

        /// <summary>
        /// Resolves a basic bump-attack (no PP cost) against an adjacent enemy
        /// instead of moving into its tile. Damage resolves synchronously, but
        /// the turn now ends via HandleAttackAnimationCompleted once the attack
        /// lunge (see GridVisualAnimator.PlayAttack) returns to rest - unless no
        /// visual animator is present, in which case the turn ends immediately
        /// as before. The damage line is posted on a killing blow too, not just
        /// a surviving hit - otherwise the one attack whose number matters most
        /// is the one the log never shows.
        /// </summary>
        private void PerformAttack(EnemyController enemy, Vector2Int direction)
        {
            if (stats == null)
            {
                return;
            }

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(stats.AttackDamage, enemy.Health);
            Post(attackMessage, StyledName.Enemy(enemy.DisplayName), result.Damage);

            if (result.DefenderDied)
            {
                Post(enemyDefeatedMessage, StyledName.Enemy(enemy.DisplayName));
                enemy.Die();
            }

            if (visualAnimator != null)
            {
                attackAnimationInProgress = true;
                visualAnimator.PlayAttack(direction, HandleAttackAnimationCompleted);
            }
            else
            {
                turnResolver?.EndPlayerTurn();
            }
        }

        /// <summary>
        /// Fires once the player's attack lunge returns to rest. The turn only
        /// ends here, not synchronously inside PerformAttack, so a held key
        /// can't commit a second action while the lunge is still playing (see
        /// attackAnimationInProgress in Update).
        /// </summary>
        private void HandleAttackAnimationCompleted()
        {
            attackAnimationInProgress = false;
            turnResolver?.EndPlayerTurn();
        }

        /// <summary>
        /// Applies one turn's worth of Paranoia gain and posts a combined
        /// incoming-attack message. Fires from every player action that ends a turn
        /// (move, bump-attack, item use). A death that happened during this turn is
        /// presented last, so the killing blow's damage line is already on screen
        /// before the Voice speaks.
        /// </summary>
        private void HandleTurnEnded()
        {
            stats?.ApplyTurnParanoiaGain();
            PostIncomingAttackMessage();

            if (deathPending)
            {
                deathPending = false;
                PlayDefeatDialogue();
            }
        }

        /// <summary>
        /// Posts a single message summarizing every enemy bump-attack landed on
        /// the player this turn (see PlayerStatsSO.RecordIncomingAttack) - one
        /// line per turn regardless of how many enemies were adjacent, since the
        /// text box only shows its most recent line and would otherwise make it
        /// look like only the last attacker's damage was taken.
        /// </summary>
        private void PostIncomingAttackMessage()
        {
            if (stats == null || !stats.TryConsumeTurnDamage(out int totalDamage, out int attackerCount, out string lastAttacker))
            {
                return;
            }

            if (attackerCount == 1)
            {
                Post(incomingAttackMessage, StyledName.Enemy(lastAttacker), totalDamage);
            }
            else
            {
                Post(incomingMultiAttackMessage, attackerCount, totalDamage);
            }
        }

        /// <summary>
        /// Raised the moment HP reaches 0 - which is mid-turn, from inside the
        /// attack that killed the player. Only the input freeze belongs here; the
        /// Voice's line waits for HandleTurnEnded so it lands after the damage
        /// message rather than on top of it. The freeze is deliberately NOT
        /// deferred: nothing the player presses between the killing blow and the
        /// Voice speaking should reach the grid.
        /// <para>
        /// [PENDIENTE] the GDD's nightmare-failure system (bank loss, Paranoia
        /// reset to half) needs the Hub, which doesn't exist yet - the block is
        /// never released, so the run simply stops here.
        /// </para>
        /// </summary>
        private void HandleDeath()
        {
            deathPending = true;
            inputGate?.PushBlock();
        }

        /// <summary>
        /// Plays the player's hit-reaction flash+shake and a small camera
        /// shake whenever HP actually drops (HealthState.OnHPChanged only
        /// fires on a genuine hit - see HealthState.TakeDamage). Purely
        /// cosmetic: does not gate input or interact with deathPending/
        /// HandleDeath in any way.
        /// </summary>
        private void HandleHPChanged(int current, int max)
        {
            visualAnimator?.PlayHitReaction();
            cameraShake?.Shake();
        }

        /// <summary>
        /// Hands the player's death to the Voice. The dialogue panel takes and
        /// releases its own input block, which is why HandleDeath pushes a separate
        /// one that outlives it - otherwise dismissing the Voice's last line would
        /// hand control back to a corpse.
        /// </summary>
        private void PlayDefeatDialogue()
        {
            if (playerDefeatedDialogue == null)
            {
                Debug.LogError($"PlayerController on {name}: playerDefeatedDialogue is not assigned; the player's death will pass unremarked.");
                return;
            }

            if (dialoguePanel == null)
            {
                Debug.LogError($"PlayerController on {name}: no dialogue panel was supplied to Initialize; the player's death will pass unremarked.");
                return;
            }

            dialoguePanel.Play(playerDefeatedDialogue);
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
