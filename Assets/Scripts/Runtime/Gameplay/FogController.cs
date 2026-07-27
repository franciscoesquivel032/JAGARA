using Jagara.Runtime.Data;
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    [RequireComponent(typeof(Renderer))]
    public class FogController : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int Drift1Id = Shader.PropertyToID("_Drift1");
        private static readonly int Drift2Id = Shader.PropertyToID("_Drift2");

        private const string FogSortingLayer = "Fog";

        [SerializeField] private Renderer fogRenderer;

        private void Awake()
        {
            EnsureRenderer();
            fogRenderer.sortingLayerName = FogSortingLayer;
        }

        public void Apply(NightmareThemeSO theme)
        {
            if (theme == null)
            {
                Debug.LogError("FogController: theme is null; fog keeps its material defaults.");
                return;
            }

            EnsureRenderer();

            FogSettings settings = theme.Fog;
            fogRenderer.enabled = settings.enabled;
            if (!settings.enabled)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            fogRenderer.GetPropertyBlock(block);
            block.SetColor(FogColorId, settings.color);
            block.SetFloat(DensityId, settings.density);
            block.SetFloat(NoiseScaleId, settings.noiseScale);
            block.SetVector(Drift1Id, settings.layer1Drift);
            block.SetVector(Drift2Id, settings.layer2Drift);
            fogRenderer.SetPropertyBlock(block);
        }

        private void EnsureRenderer()
        {
            if (fogRenderer == null)
            {
                fogRenderer = GetComponent<Renderer>();
            }
        }
    }
}
