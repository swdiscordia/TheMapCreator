using UnityEngine;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Component that represents a tile sprite in the map creator.
    /// This is designed to be compatible with the main project's TileSprite.
    /// </summary>
    public class TileSprite : MonoBehaviour
    {
        // Basic tile properties
        [Header("Tile Properties")]
        public string id;
        public string key;
        public byte type;
        public bool flipX;
        public bool flipY;
        
        // Color properties
        [Header("Color Properties")]
        public float colorMultiplicatorR = 1.0f;
        public float colorMultiplicatorG = 1.0f;
        public float colorMultiplicatorB = 1.0f;
        public float colorMultiplicatorA = 1.0f;
        public bool colorMultiplicatorIsOne = true;
        
        // Cached shader property IDs
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int UseDefaultShapeProperty = Shader.PropertyToID("_UseDefaultShape");
        private static readonly int CircleRadiusProperty = Shader.PropertyToID("_CircleRadius");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        
        private void Start()
        {
            // Just apply flip properties - all texture and material handling is done by LoadMapAdapter
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
            
            // Sync flip properties with the sprite renderer
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
            
            // Debug log for verification
            Debug.Log($"[TileSprite] {id} initialized. Type: {type}, ColorMultiplicatorIsOne: {colorMultiplicatorIsOne}");
        }

        public void UpdateColors(float r, float g, float b, float a = 1.0f, bool isOne = false)
        {
            colorMultiplicatorR = r;
            colorMultiplicatorG = g;
            colorMultiplicatorB = b;
            colorMultiplicatorA = a;
            colorMultiplicatorIsOne = isOne;
            
            // Update material if already created
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            {
                // Only update if we're not using the default material
                if (!colorMultiplicatorIsOne)
                {
                    Color color;
                    if (type == 0) // Ground tile
                    {
                        color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                    }
                    else // Fixture
                    {
                        bool useHighRange = r > 1.0f || g > 1.0f || b > 1.0f;
                        if (useHighRange)
                        {
                            color = new Color(r / 255f, g / 255f, b / 255f, 
                                a > 1.0f ? a / 255f : a);
                        }
                        else
                        {
                            color = new Color(r, g, b, a);
                        }
                    }
                    
                    spriteRenderer.sharedMaterial.SetColor(ColorProperty, color);
                }
            }
        }
    }
} 