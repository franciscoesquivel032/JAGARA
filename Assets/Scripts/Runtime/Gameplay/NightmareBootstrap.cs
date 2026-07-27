using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Enemies;
using Jagara.Runtime.TurnSystem;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.Gameplay
{
    public class NightmareBootstrap : MonoBehaviour
    {
        private const string EnemiesRootName = "Enemies";

        [SerializeField] private DungeonGenerationParamsSO generationParams;
        [SerializeField] private FloorInstantiator floorInstantiator;
        [SerializeField] private GridOverlayInstantiator gridOverlayInstantiator;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private NightmareThemeSO nightmareTheme;
        [SerializeField] private FogController fogController;

        private readonly TurnResolver turnResolver = new TurnResolver();
        private OccupancyGrid occupancy;
        private EnemyAIContext aiContext;

        private void Start()
        {
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
            gridOverlayInstantiator?.RenderOverlay(floor);

            ApplyFog();

            Debug.Log($"Nightmare floor generated — seed: {seed}\n{floor.ToAsciiArt()}");

            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);
            occupancy = new OccupancyGrid(width, height);

            GridMover playerMover = SpawnPlayer(floor);
            if (playerMover == null)
            {
                return;
            }

            aiContext = new EnemyAIContext(floor, occupancy, turnResolver, playerMover);
            SpawnEnemies(floor);
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
        /// Spawns and initializes the player. Returns the player's GridMover
        /// (needed to construct EnemyAIContext), or null if spawning failed -
        /// callers must treat null as "abort the rest of Start()".
        /// </summary>
        private GridMover SpawnPlayer(FloorData floor)
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

            controller.Initialize(floor, tilemap, spawnCell, turnResolver, occupancy);

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(playerInstance.transform);
            }
            else
            {
                Debug.LogWarning("NightmareBootstrap: cameraFollow reference is not assigned; camera will not follow the player.");
            }

            return controller.Mover;
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
    }
}
