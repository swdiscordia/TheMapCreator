using UnityEngine;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Represents a tile sprite in the map editor.
    /// This is our own implementation independent from ploup.
    /// </summary>
    public class TileSprite : MonoBehaviour
    {
        public string id;
        public string key;
        public byte type; // 0 = ground tile, 1 = fixture
        
        // Color multiplier properties
        public bool colorMultiplicatorIsOne = true;
        public float colorMultiplicatorR = 1f;
        public float colorMultiplicatorG = 1f;
        public float colorMultiplicatorB = 1f;
        public float colorMultiplicatorA = 1f;
        
        // Flipping properties
        public bool flipX = false;
        public bool flipY = false;
        
        private void Start()
        {
            // Sync flip properties with the sprite renderer
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flipX;
                spriteRenderer.flipY = flipY;
            }
        }
    }
} 