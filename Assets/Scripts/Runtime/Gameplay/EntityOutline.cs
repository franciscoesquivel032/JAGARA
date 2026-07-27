using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Writes a per-instance outline color into the sprite's MaterialPropertyBlock
    /// so a single shared EntityFactionOutline material can render a different
    /// outline per entity without instancing the material (see GridVisualAnimator,
    /// which lives on the same "Visual" child GameObject, for the sibling pattern).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EntityOutline : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color outlineColor = Color.white;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            block.SetColor(OutlineColorId, outlineColor);
            spriteRenderer.SetPropertyBlock(block);
        }
    }
}
