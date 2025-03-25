using UnityEngine;
using CreatorMap.Scripts.Data;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Component that represents a tile sprite in the map creator.
    /// This is designed to be compatible with the main project's TileSprite.
    /// </summary>
    public class TileSprite : MonoBehaviour
    {
        // Basic tile properties - ces propriétés doivent correspondre exactement à celles du projet principal
        [Header("Tile Properties")]
        public string id;
        public string key;
        public byte type;
        public bool flipX;
        public bool flipY;
        
        // Color properties - ces propriétés doivent correspondre exactement à celles du projet principal
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

        /// <summary>
        /// Updates the color multiplicator values and applies them to the material
        /// </summary>
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
        
        /// <summary>
        /// Convertit les données du TileSprite en TileSpriteData pour la sérialisation
        /// </summary>
        public TileSpriteData ToTileSpriteData()
        {
            var data = new TileSpriteData
            {
                Id = id,
                Position = transform.position,
                Scale = transform.localScale.x,
                Order = GetComponent<SpriteRenderer>()?.sortingOrder ?? 0,
                FlipX = flipX,
                FlipY = flipY,
                IsFixture = type == 1,
                Color = new TileColorData
                {
                    Red = colorMultiplicatorR,
                    Green = colorMultiplicatorG,
                    Blue = colorMultiplicatorB,
                    Alpha = colorMultiplicatorA
                }
            };
            
            return data;
        }
        
        /// <summary>
        /// Applique les données d'un TileSpriteData à ce TileSprite
        /// </summary>
        public void ApplyTileSpriteData(TileSpriteData data)
        {
            if (data == null) return;
            
            id = data.Id;
            type = data.IsFixture ? (byte)1 : (byte)0;
            transform.position = data.Position;
            transform.localScale = new Vector3(data.Scale, data.Scale, 1f);
            flipX = data.FlipX;
            flipY = data.FlipY;
            
            // Apply color
            colorMultiplicatorR = data.Color.Red;
            colorMultiplicatorG = data.Color.Green;
            colorMultiplicatorB = data.Color.Blue;
            colorMultiplicatorA = data.Color.Alpha;
            colorMultiplicatorIsOne = data.Color.IsOne;
            
            // Apply to sprite renderer
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flipX;
                spriteRenderer.flipY = flipY;
                spriteRenderer.sortingOrder = data.Order;
            }
        }
    }
} 