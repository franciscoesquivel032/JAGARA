using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.TurnSystem;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Runtime.Gameplay
{
    public class NightmareBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonGenerationParamsSO generationParams;
        [SerializeField] private FloorInstantiator floorInstantiator;
        [SerializeField] private GridOverlayInstantiator gridOverlayInstantiator;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private CameraFollow cameraFollow;

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

            Debug.Log($"Nightmare floor generated — seed: {seed}\n{floor.ToAsciiArt()}");

            SpawnPlayer(floor);
        }

        private void SpawnPlayer(FloorData floor)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("NightmareBootstrap: playerPrefab reference is not assigned.");
                return;
            }

            if (!floor.PlayerSpawn.HasValue)
            {
                Debug.LogError("NightmareBootstrap: floor has no PlayerSpawn position; cannot spawn player.");
                return;
            }

            Vector2Int spawnCell = floor.PlayerSpawn.Value;
            Tilemap tilemap = floorInstantiator.Tilemap;
            Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(spawnCell.x, spawnCell.y, 0));

            GameObject playerInstance = Instantiate(playerPrefab, worldPos, Quaternion.identity);
            var controller = playerInstance.GetComponent<PlayerController>();
            if (controller == null)
            {
                Debug.LogError("NightmareBootstrap: playerPrefab is missing a PlayerController component.");
                return;
            }

            controller.Initialize(floor, tilemap, spawnCell, new TurnResolver());

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(playerInstance.transform);
            }
            else
            {
                Debug.LogWarning("NightmareBootstrap: cameraFollow reference is not assigned; camera will not follow the player.");
            }
        }
    }
}
