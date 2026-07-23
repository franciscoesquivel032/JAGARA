using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    public class NightmareBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonGenerationParamsSO generationParams;
        [SerializeField] private FloorInstantiator floorInstantiator;

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

            Debug.Log($"Nightmare floor generated — seed: {seed}\n{floor.ToAsciiArt()}");
        }
    }
}
