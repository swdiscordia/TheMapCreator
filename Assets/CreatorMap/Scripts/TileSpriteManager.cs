using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CreatorMap.Scripts.Core.Grid;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Manager class that handles the creation and management of TileSprite GameObjects
    /// This aligns with the main project's approach of storing sprite data separate from MapBasicInformation
    /// </summary>
    public class TileSpriteManager : MonoBehaviour
    {
        // Singleton instance
        public static TileSpriteManager Instance { get; private set; }
        
        // Collection of all tile sprites in the current map
        private readonly List<TileSprite> _tileSprites = new List<TileSprite>();
        
        // Map GameObject that owns the sprites
        private GameObject _mapContainer;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Fix: Ensure we have a valid container by checking multiple options
            if (transform.parent != null)
            {
                // Option 1: Use the parent GameObject if this component is a child
                _mapContainer = transform.parent.gameObject;
                Debug.Log($"[TileSpriteManager] Initialized with parent as map container: {_mapContainer.name}");
            }
            else
            {
                // Option 2: Use this GameObject if it's not a child of another GameObject
                _mapContainer = gameObject;
                Debug.Log($"[TileSpriteManager] Initialized with self as map container: {_mapContainer.name}");
            }
            
            // Verify that _mapContainer is valid
            if (_mapContainer == null)
            {
                Debug.LogError("[TileSpriteManager] Failed to initialize map container! Tile creation may fail.");
                _mapContainer = gameObject; // Last resort fallback
            }
        }
        
        /// <summary>
        /// Creates a new tile sprite and adds it to the scene
        /// </summary>
        /// <param name="id">Unique ID of the sprite</param>
        /// <param name="key">Addressable key for loading the sprite texture</param>
        /// <param name="type">Type of sprite (0 = ground, 1 = fixture)</param>
        /// <param name="position">Position in world space</param>
        /// <param name="flipX">Whether to flip the sprite horizontally</param>
        /// <param name="flipY">Whether to flip the sprite vertically</param>
        /// <param name="colorR">Red color multiplier (0-1 or 0-255 depending on type)</param>
        /// <param name="colorG">Green color multiplier (0-1 or 0-255 depending on type)</param>
        /// <param name="colorB">Blue color multiplier (0-1 or 0-255 depending on type)</param>
        /// <param name="colorA">Alpha multiplier (0-1)</param>
        /// <param name="sortingOrder">Order in layer for rendering</param>
        /// <returns>The created TileSprite component</returns>
        public TileSprite CreateTileSprite(
            string id, 
            string key, 
            byte type,
            Vector3 position,
            bool flipX = false,
            bool flipY = false,
            float colorR = 1.0f,
            float colorG = 1.0f,
            float colorB = 1.0f,
            float colorA = 1.0f,
            int sortingOrder = 0)
        {
            // Ensure _mapContainer is not null
            if (_mapContainer == null)
            {
                Debug.LogError("Map container is null! Make sure TileSpriteManager is properly initialized.");
                
                // Try to find the GridManager in the scene to use as the container
                var gridManager = FindFirstObjectByType<MapCreatorGridManager>();
                if (gridManager != null)
                {
                    _mapContainer = gridManager.gameObject;
                    Debug.Log($"Found GridManager and using it as map container: {_mapContainer.name}");
                }
                else
                {
                    // Last resort: create a new container GameObject
                    _mapContainer = new GameObject("Map Container");
                    Debug.LogWarning("Created a new Map Container GameObject as fallback. Tiles may not be organized correctly.");
                }
            }

            // Use a more meaningful name including the tile key for better identification
            string tileName = string.IsNullOrEmpty(key) ? $"Tile_{id}" : key;
            
            // Create a new GameObject
            var tileObject = new GameObject(tileName);
            
            // Set parent and position
            try
            {
                tileObject.transform.SetParent(_mapContainer.transform);
                tileObject.transform.position = position;
                
                // Log successful parenting
                Debug.Log($"Created tile {tileName} as child of {_mapContainer.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parenting tile to map container: {ex.Message}");
                // If parenting fails, don't abort - continue with the tile at the world position
            }
            
            // Add a SpriteRenderer
            var spriteRenderer = tileObject.AddComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.flipX = flipX;
                spriteRenderer.flipY = flipY;
                // Make sure the sprite is visible with a default color
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                
                // Create and apply the ColorMatrixShader material
                Shader colorMatrixShader = Shader.Find("Custom/ColorMatrixShader");
                if (colorMatrixShader != null)
                {
                    Material material = new Material(colorMatrixShader);
                    
                    // Extract numeric ID to load texture directly
                    string numericId = string.Empty;
                    
                    // If key contains a path structure, extract the numeric ID
                    if (!string.IsNullOrEmpty(key) && key.Contains("/"))
                    {
                        string filename = System.IO.Path.GetFileNameWithoutExtension(key);
                        numericId = new string(filename.Where(char.IsDigit).ToArray());
                    }
                    else if (!string.IsNullOrEmpty(id))
                    {
                        // Use the ID directly
                        numericId = new string(id.Where(char.IsDigit).ToArray());
                    }
                    
                    // Try to load the texture if we have a valid numeric ID
                    Texture2D texture = null;
                    if (!string.IsNullOrEmpty(numericId))
                    {
                        string subfolder = numericId.Length >= 2 ? numericId.Substring(0, 2) : numericId;
                        
                        string[] possiblePaths = new string[]
                        {
                            $"Assets/CreatorMap/Content/Tiles/{subfolder}/{numericId}.png",
                            $"Assets/CreatorMap/Tiles/{subfolder}/{numericId}.png",
                            $"Assets/CreatorMap/Content/Tiles/{numericId}.png",
                            $"Assets/CreatorMap/Tiles/{numericId}.png",
                            $"Assets/CreatorMap/Tiles/Images/{numericId}.png"
                        };
                        
                        foreach (string path in possiblePaths)
                        {
                            #if UNITY_EDITOR
                            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            if (texture != null)
                            {
                                Debug.Log($"[TileSpriteManager] Loaded texture for tile {id} from {path}");
                                break;
                            }
                            #endif
                        }
                        
                        // If texture was loaded, create and assign sprite
                        if (texture != null)
                        {
                            // Create sprite with center pivot
                            Vector2 pivot = new Vector2(0.5f, 0.5f);
                            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot);
                            spriteRenderer.sprite = sprite;
                            
                            // Set texture on material
                            material.SetTexture("_MainTex", texture);
                            material.SetFloat("_UseDefaultShape", 0f); // Use texture
                        }
                        else
                        {
                            Debug.LogWarning($"[TileSpriteManager] Could not find texture for tile {id} with numeric ID {numericId}");
                            material.SetFloat("_UseDefaultShape", 1f); // Use shape
                            material.SetFloat("_CircleRadius", 0.5f);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[TileSpriteManager] Invalid numeric ID for tile {id}");
                        material.SetFloat("_UseDefaultShape", 1f); // Use shape
                        material.SetFloat("_CircleRadius", 0.5f);
                    }
                    
                    // Set default shader properties
                    
                    // Apply color to material if not using default color
                    bool isOne = colorR == 1.0f && colorG == 1.0f && colorB == 1.0f && colorA == 1.0f;
                    if (!isOne)
                    {
                        if (type == 0) // Ground tile
                        {
                            material.SetColor("_Color", new Color(
                                colorR / 255f,
                                colorG / 255f,
                                colorB / 255f,
                                1f
                            ));
                        }
                        else // Fixture
                        {
                            bool useHighRange = colorR > 1.0f || colorG > 1.0f || colorB > 1.0f;
                            if (useHighRange)
                            {
                                material.SetColor("_Color", new Color(
                                    colorR / 255f,
                                    colorG / 255f,
                                    colorB / 255f,
                                    colorA > 1.0f ? colorA / 255f : colorA
                                ));
                            }
                            else
                            {
                                material.SetColor("_Color", new Color(
                                    colorR,
                                    colorG,
                                    colorB,
                                    colorA
                                ));
                            }
                        }
                    }
                    else
                    {
                        // Default white color
                        material.SetColor("_Color", Color.white);
                    }
                    
                    // Apply material to sprite renderer
                    spriteRenderer.sharedMaterial = material;
                }
                else
                {
                    Debug.LogWarning($"Could not find ColorMatrixShader for tile {id}");
                }
            }
            else
            {
                Debug.LogError($"Failed to add SpriteRenderer to tile {tileName}");
            }
            
            // Add TileSprite component
            var tileSprite = tileObject.AddComponent<TileSprite>();
            if (tileSprite != null)
            {
                tileSprite.id = id;
                tileSprite.key = key;
                tileSprite.type = type;
                tileSprite.flipX = flipX;
                tileSprite.flipY = flipY;
                
                // Set color multiplicator
                bool isOne = colorR == 1.0f && colorG == 1.0f && colorB == 1.0f && colorA == 1.0f;
                tileSprite.colorMultiplicatorIsOne = isOne;
                tileSprite.colorMultiplicatorR = colorR;
                tileSprite.colorMultiplicatorG = colorG;
                tileSprite.colorMultiplicatorB = colorB;
                tileSprite.colorMultiplicatorA = colorA;
            }
            else
            {
                Debug.LogError($"Failed to add TileSprite component to tile {tileName}");
            }
            
            // Register in our list
            _tileSprites.Add(tileSprite);
            
            return tileSprite;
        }
        
        /// <summary>
        /// Removes a tile sprite from the scene
        /// </summary>
        /// <param name="id">ID of the sprite to remove</param>
        public void RemoveTileSprite(string id)
        {
            var sprite = _tileSprites.FirstOrDefault(s => s.id == id);
            if (sprite != null)
            {
                _tileSprites.Remove(sprite);
                Destroy(sprite.gameObject);
            }
        }
        
        /// <summary>
        /// Gets a tile sprite by ID
        /// </summary>
        /// <param name="id">ID of the sprite to find</param>
        /// <returns>The TileSprite component, or null if not found</returns>
        public TileSprite GetTileSprite(string id)
        {
            return _tileSprites.FirstOrDefault(s => s.id == id);
        }
        
        /// <summary>
        /// Gets all tile sprites managed by this instance
        /// </summary>
        /// <returns>List of TileSprite components</returns>
        public List<TileSprite> GetAllTileSprites()
        {
            return new List<TileSprite>(_tileSprites);
        }
        
        /// <summary>
        /// Clear all tile sprites from the scene
        /// </summary>
        public void ClearAllTileSprites()
        {
            foreach (var sprite in _tileSprites)
            {
                if (sprite != null && sprite.gameObject != null)
                {
                    Destroy(sprite.gameObject);
                }
            }
            
            _tileSprites.Clear();
        }

        // Public method to set the map container
        public void SetMapContainer(GameObject container)
        {
            if (container != null)
            {
                _mapContainer = container;
                Debug.Log($"Map container set to {container.name}");
            }
        }
    }
} 