using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.Data
{
    [System.Serializable]
    public struct FogSettings
    {
        public bool enabled;
        public Color color;
        [Range(0f, 1f)] public float density;
        [Tooltip("World-space scale of the fog noise. Smaller values = larger fog shapes.")]
        public float noiseScale;
        public Vector2 layer1Drift;
        public Vector2 layer2Drift;

        public static FogSettings Default => new FogSettings
        {
            enabled = true,
            color = new Color(0.62f, 0.62f, 0.72f, 1f),
            density = 0.35f,
            noiseScale = 0.15f,
            layer1Drift = new Vector2(0.02f, 0.012f),
            layer2Drift = new Vector2(-0.016f, 0.025f)
        };
    }

    [CreateAssetMenu(fileName = "New Nightmare Theme", menuName = "Jagara/Nightmare Theme")]
    public class NightmareThemeSO : ScriptableObject
    {
        [System.Serializable]
        public struct WeightedEnemy
        {
            public EnemyConfigSO config;
            [Min(0)] public int weight;
        }

        [SerializeField] private FogSettings fog = FogSettings.Default;

        [Header("Environment Tint")]
        [Tooltip("Multiply-tinted onto the floor and decoration Tilemaps to dim the environment relative to entities. White = no dimming.")]
        [SerializeField] private Color environmentTint = new Color(140f / 255f, 140f / 255f, 153f / 255f, 1f);

        [Header("Enemy Roster (weighted)")]
        [SerializeField] private List<WeightedEnemy> enemyRoster = new();

        public FogSettings Fog => fog;
        public Color EnvironmentTint => environmentTint;
        public IReadOnlyList<WeightedEnemy> EnemyRoster => enemyRoster;
    }
}
