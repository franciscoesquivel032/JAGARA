using System.Collections.Generic;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Enemies;
using Jagara.Runtime.Narrative;
using Jagara.Runtime.TurnSystem;
using Jagara.Runtime.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.Gameplay
{
    public class NightmareBootstrap : MonoBehaviour
    {
        private const string EnemiesRootName = "Enemies";
        private const string ItemsRootName = "Items";

        [SerializeField] private DungeonGenerationParamsSO generationParams;
        [SerializeField] private FloorInstantiator floorInstantiator;
        [SerializeField] private GridOverlayInstantiator gridOverlayInstantiator;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private PlayerStatsSO playerStats;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private NightmareThemeSO nightmareTheme;
        [SerializeField] private FogController fogController;
        [SerializeField] private ActionMenuController actionMenu;
        [SerializeField] private ItemActionPanelController itemActionPanel;
        [SerializeField] private PlayerStatusHudController statusHud;

        [Header("Narrative")]
        [Tooltip("Threaded into PlayerController at spawn (a scene object can't be serialized into the player prefab). Used for the Voice's line on death.")]
        [SerializeField] private DialoguePanelController dialoguePanel;

        [SerializeField] private GameplayInputGateSO inputGate;
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO floorEnteredMessage;

        private readonly TurnResolver turnResolver = new TurnResolver();
        private readonly Dictionary<Vector2Int, ItemMarker> itemsOnFloor = new();
        private OccupancyGrid occupancy;
        private EnemyAIContext aiContext;

        private void Start()
        {
            // All three survive Play Mode sessions and scene loads (they're
            // assets), so a new floor has to start from a clean log, an open
            // input gate, and fresh HP/Paranoia - otherwise the previous run's
            // messages, any block left behind by a panel destroyed mid-transition,
            // or even a dead HealthState (Current stuck at 0, TakeDamage a no-op)
            // would carry over. OnEnable alone doesn't cover this: with "Enter
            // Play Mode Options" set to skip domain reload, these assets' OnEnable
            // does not re-run between Play sessions.
            messageLog?.Clear();
            inputGate?.ResetGate();
            playerStats?.ResetRuntimeState();

            if (generationParams == null)
            {
                Debug.LogError("NightmareBootstrap: generationParams reference is not assigned.");
                return;
            }

            if (floorInstantiator == null)
            {
                Debug.LogError("NightmareBootstrap: floorInstantiator reference is not assigned.");
                return;
            }

            int seed = new System.Random().Next();
            FloorData floor = new DungeonGenerator().GenerateFloor(seed, generationParams.ToParams());
            floorInstantiator.InstantiateFloor(floor);
            floorInstantiator.ApplyEnvironmentTint(nightmareTheme);
            gridOverlayInstantiator?.RenderOverlay(floor);

            ApplyFog();

            Debug.Log($"Nightmare floor generated — seed: {seed}\n{floor.ToAsciiArt()}");

            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);
            occupancy = new OccupancyGrid(width, height);

            // Items must be spawned (and itemsOnFloor populated) before the player,
            // since PlayerController.Initialize needs the registry to check pickups
            // against. SpawnItems has no dependency on the player, so this order is safe.
            SpawnItems(floor);

            PlayerController player = SpawnPlayer(floor);
            if (player == null)
            {
                return;
            }

            aiContext = new EnemyAIContext(floor, occupancy, turnResolver, player.Mover, player.Stats);
            SpawnEnemies(floor);

            // Posted last so it isn't immediately buried by anything spawning does.
            if (messageLog != null && floorEnteredMessage != null)
            {
                messageLog.Post(floorEnteredMessage);
            }
        }

        private void ApplyFog()
        {
            if (fogController == null)
            {
                Debug.LogWarning("NightmareBootstrap: fogController reference is not assigned; skipping fog setup.");
                return;
            }

            fogController.Apply(nightmareTheme);
        }

        /// <summary>
        /// Spawns and initializes the player. Returns the player's
        /// PlayerController (needed to construct EnemyAIContext via its Mover
        /// and Stats), or null if spawning failed - callers must treat null as
        /// "abort the rest of Start()".
        /// </summary>
        private PlayerController SpawnPlayer(FloorData floor)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("NightmareBootstrap: playerPrefab reference is not assigned.");
                return null;
            }

            if (!floor.PlayerSpawn.HasValue)
            {
                Debug.LogError("NightmareBootstrap: floor has no PlayerSpawn position; cannot spawn player.");
                return null;
            }

            Vector2Int spawnCell = floor.PlayerSpawn.Value;
            Tilemap tilemap = floorInstantiator.Tilemap;
            Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(spawnCell.x, spawnCell.y, 0));

            GameObject playerInstance = Instantiate(playerPrefab, worldPos, Quaternion.identity);
            var controller = playerInstance.GetComponent<PlayerController>();
            if (controller == null)
            {
                Debug.LogError("NightmareBootstrap: playerPrefab is missing a PlayerController component.");
                return null;
            }

            if (actionMenu == null)
            {
                Debug.LogWarning("NightmareBootstrap: actionMenu reference is not assigned; the player will have no action menu.");
            }

            actionMenu?.Initialize(turnResolver);
            itemActionPanel?.Initialize(controller);

            if (dialoguePanel == null)
            {
                Debug.LogWarning("NightmareBootstrap: dialoguePanel reference is not assigned; the Voice will not speak when the player dies.");
            }

            controller.Initialize(floor, tilemap, spawnCell, turnResolver, occupancy, itemsOnFloor, itemPrefab, dialoguePanel);
            statusHud?.Bind(controller.Stats.Health, controller.Stats.Paranoia);

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(playerInstance.transform);
            }
            else
            {
                Debug.LogWarning("NightmareBootstrap: cameraFollow reference is not assigned; camera will not follow the player.");
            }

            return controller;
        }

        private void SpawnEnemies(FloorData floor)
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("NightmareBootstrap: enemyPrefab reference is not assigned; floor will have no enemies.");
                return;
            }

            var roster = nightmareTheme != null ? nightmareTheme.EnemyRoster : null;
            if (roster == null || roster.Count == 0)
            {
                Debug.LogWarning("NightmareBootstrap: nightmareTheme has no enemyRoster entries; floor will have no enemies.");
                return;
            }

            var rng = new System.Random(floor.Seed);
            Tilemap tilemap = floorInstantiator.Tilemap;

            var enemiesRoot = new GameObject(EnemiesRootName).transform;
            enemiesRoot.SetParent(transform, worldPositionStays: false);

            int[] weights = new int[roster.Count];
            int totalWeight = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                // A null config would let PickIndex select an entry that
                // later NREs in EnemyController.Initialize, so treat it as
                // structurally unweighted (never selectable).
                weights[i] = roster[i].config != null ? roster[i].weight : 0;
                totalWeight += weights[i];
            }

            if (totalWeight <= 0)
            {
                Debug.LogWarning("NightmareBootstrap: nightmareTheme's enemyRoster has no valid (non-null config, non-zero weight) entries; floor will have no enemies.");
                return;
            }

            for (int i = 0; i < floor.EnemySpawnPositions.Count; i++)
            {
                Vector2Int cell = floor.EnemySpawnPositions[i];

                int index = EnemyRosterPicker.PickIndex(weights, rng);
                if (index == -1)
                {
                    Debug.LogWarning($"NightmareBootstrap: EnemyRosterPicker returned no valid index for cell {cell}; skipping spawn.");
                    continue;
                }

                Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                GameObject enemyInstance = Instantiate(enemyPrefab, worldPos, Quaternion.identity, enemiesRoot);
                var controller = enemyInstance.GetComponent<EnemyController>();
                if (controller == null)
                {
                    Debug.LogError("NightmareBootstrap: enemyPrefab is missing an EnemyController component.");
                    continue;
                }

                controller.Initialize(roster[index].config, floor, tilemap, cell, turnResolver, aiContext);
            }
        }

        private void SpawnItems(FloorData floor)
        {
            itemsOnFloor.Clear();

            if (itemPrefab == null)
            {
                Debug.LogWarning("NightmareBootstrap: itemPrefab reference is not assigned; floor will have no items.");
                return;
            }

            var itemTable = nightmareTheme != null ? nightmareTheme.ItemTable : null;
            if (itemTable == null || itemTable.Count == 0)
            {
                Debug.LogWarning("NightmareBootstrap: nightmareTheme has no itemTable entries; floor will have no items.");
                return;
            }

            var rng = new System.Random(floor.Seed);
            Tilemap tilemap = floorInstantiator.Tilemap;

            var itemsRoot = new GameObject(ItemsRootName).transform;
            itemsRoot.SetParent(transform, worldPositionStays: false);

            int[] weights = new int[itemTable.Count];
            int totalWeight = 0;
            for (int i = 0; i < itemTable.Count; i++)
            {
                // A null item would let PickIndex select an entry that later
                // NREs in ItemMarker.Initialize, so treat it as structurally
                // unweighted (never selectable).
                weights[i] = itemTable[i].item != null ? itemTable[i].weight : 0;
                totalWeight += weights[i];
            }

            if (totalWeight <= 0)
            {
                Debug.LogWarning("NightmareBootstrap: nightmareTheme's itemTable has no valid (non-null item, non-zero weight) entries; floor will have no items.");
                return;
            }

            for (int i = 0; i < floor.ItemSpawnPositions.Count; i++)
            {
                Vector2Int cell = floor.ItemSpawnPositions[i];

                int index = EnemyRosterPicker.PickIndex(weights, rng);
                if (index == -1)
                {
                    Debug.LogWarning($"NightmareBootstrap: EnemyRosterPicker returned no valid index for cell {cell}; skipping spawn.");
                    continue;
                }

                Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                GameObject itemInstance = Instantiate(itemPrefab, worldPos, Quaternion.identity, itemsRoot);
                var marker = itemInstance.GetComponent<ItemMarker>();
                if (marker == null)
                {
                    Debug.LogError("NightmareBootstrap: itemPrefab is missing an ItemMarker component.");
                    continue;
                }

                marker.Initialize(itemTable[index].item);
                itemsOnFloor[cell] = marker;
            }
        }
    }
}
