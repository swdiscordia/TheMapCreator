using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using CreatorMap.Scripts.Data;
using Managers.Maps.MapCreator;
using MapCreator.Data.Models;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Adapter class that handles map loading at runtime.
    /// This provides compatibility with the loaded map format without 
    /// directly referencing the other project's code.
    /// </summary>
    public class LoadMapAdapter : MonoBehaviour
    {
        // Reference to our map component
        private MapComponent mapComponent;
        
        // Reference to shader property IDs for better performance
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int UseDefaultShapeProperty = Shader.PropertyToID("_UseDefaultShape");
        private static readonly int CircleRadiusProperty = Shader.PropertyToID("_CircleRadius");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");

        // Handles for Addressable assets
        private AsyncOperationHandle<IList<Texture2D>> _textureHandle;
        private AsyncOperationHandle<Shader> _shaderHandle;
        
        private void Awake()
        {
            mapComponent = GetComponent<MapComponent>();
        }

        private void OnDestroy()
        {
            // Release addressable handles to prevent memory leaks
            if (_textureHandle.IsValid())
            {
                Addressables.Release(_textureHandle);
            }
            
            if (_shaderHandle.IsValid())
            {
                Addressables.Release(_shaderHandle);
            }
        }
        
        /// <summary>
        /// Prepares the map for runtime loading by setting up all TileSprite components
        /// with the correct properties for the runtime system to load.
        /// </summary>
        public void PrepareMapForLoading()
        {
            if (mapComponent == null || mapComponent.mapInformation == null || 
                mapComponent.mapInformation.SpriteData == null)
            {
                Debug.LogWarning("Cannot prepare map for loading: Map component, map information, or sprite data is null");
                return;
            }
                
            var allTiles = GetComponentsInChildren<TileSprite>();
            if (allTiles == null || allTiles.Length == 0)
            {
                Debug.LogWarning("No tile sprites found in children");
                return;
            }
            
            Debug.Log($"Preparing {allTiles.Length} tiles for loading");
            
            // Make sure TileSprite components have the correct properties
            foreach (var tile in allTiles)
            {
                // Find matching tile data
                var tileData = mapComponent.mapInformation.SpriteData.Tiles.FirstOrDefault(t => 
                    t.Id == tile.id && 
                    Mathf.Approximately(t.Position.x, tile.transform.position.x) && 
                    Mathf.Approximately(t.Position.y, tile.transform.position.y));
                    
                if (tileData != null)
                {
                    // Update TileSprite component with the correct properties
                    tile.id = tileData.Id;
                    tile.key = GetTileAddressablePath(tileData.Id, tileData.IsFixture);
                    tile.type = (byte)(tileData.IsFixture ? 1 : 0);
                    
                    // Set flipping properties
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.flipX = tileData.FlipX;
                        renderer.flipY = tileData.FlipY;
                        tile.flipX = tileData.FlipX;
                        tile.flipY = tileData.FlipY;
                    }
                    
                    // Set color multiplier
                    tile.colorMultiplicatorIsOne = tileData.Color.IsOne;
                    
                    if (tileData.Color.IsOne)
                    {
                        // Use default values from main project (256, 256, 256, 0)
                        tile.colorMultiplicatorR = 256f;
                        tile.colorMultiplicatorG = 256f;
                        tile.colorMultiplicatorB = 256f;
                        tile.colorMultiplicatorA = 0f;
                    }
                    else
                    {
                        // Use custom color values
                        tile.colorMultiplicatorR = tileData.Color.Red;
                        tile.colorMultiplicatorG = tileData.Color.Green;
                        tile.colorMultiplicatorB = tileData.Color.Blue;
                        tile.colorMultiplicatorA = tileData.Color.Alpha;
                    }
                }
            }
            
            // Apply the shader to all tiles (async)
            LoadTileTexturesAsync();
        }
        
        // Helper method to get the full path based on a tile ID
        private string GetTileAddressablePath(string tileId, bool isFixture)
        {
            // Clean the ID to just numbers
            string numericId = new string(tileId.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(numericId))
                return tileId; // Fallback to original
            
            // Get the first two digits for the subfolder (or just use the first digit if only one digit)
            string subfolder = numericId.Length >= 2 ? numericId.Substring(0, 2) : numericId;
            
            // Format the full path according to pattern
            string prefix = isFixture ? "Fixture Assets" : "Tiles Assets";
            return $"{prefix}/Tiles/{subfolder}/{numericId}.png";
        }

        /// <summary>
        /// Loads all tile textures asynchronously using Addressables
        /// </summary>
        private async void LoadTileTexturesAsync()
        {
            var allTiles = GetComponentsInChildren<TileSprite>();
            if (allTiles == null || allTiles.Length == 0) return;
            
            Debug.Log($"Loading shader and textures for {allTiles.Length} tiles using Addressables");
            
            try
            {
                // Load shader asynchronously using Addressables (exactly like main project)
                _shaderHandle = Addressables.LoadAssetAsync<Shader>("Shaders/ColorMatrixShader.shader");
                var colorMatrixShader = await _shaderHandle.Task;
                
                if (colorMatrixShader == null)
                {
                    Debug.LogError("[LoadMapAdapter] Failed to load ColorMatrixShader via Addressables");
                    return;
                }
                
                Debug.Log("[LoadMapAdapter] Successfully loaded ColorMatrixShader via Addressables");
                
                // Collect all distinct keys for tile textures
                var keys = allTiles.Select(x => x.key).Distinct().ToList();
                Debug.Log($"[LoadMapAdapter] Loading {keys.Count} distinct textures");
                
                // Load all textures in one batch
                _textureHandle = Addressables.LoadAssetsAsync<Texture2D>(keys, null, Addressables.MergeMode.Union);
                var textureAssets = await _textureHandle.Task;
                
                if (textureAssets == null || textureAssets.Count == 0)
                {
                    Debug.LogError("[LoadMapAdapter] Failed to load any textures via Addressables");
                    return;
                }
                
                Debug.Log($"[LoadMapAdapter] Successfully loaded {textureAssets.Count} textures");
                
                // Create dictionary mapping texture name to texture (like main project)
                var textures = textureAssets.ToDictionary(texture => texture.name);
                
                // Apply textures to tiles
                foreach (var tile in allTiles)
                {
                    if (string.IsNullOrEmpty(tile.id)) continue;
                    
                    var spriteRenderer = tile.GetComponent<SpriteRenderer>();
                    if (spriteRenderer == null) continue;
                    
                    // Extract the ID from the key to look up in the dictionary
                    string textureId = tile.id;
                    
                    // Find texture in dictionary
                    Texture2D texture = null;
                    if (textures.TryGetValue(textureId, out texture))
                    {
                        // Create sprite with appropriate pivot based on tile type
                        Vector2 pivot = tile.type == 0 ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot);
                        spriteRenderer.sprite = sprite;
                        
                        Debug.Log($"[LoadMapAdapter] Applied texture for tile {tile.id}");
                    }
                    else
                    {
                        Debug.LogWarning($"[LoadMapAdapter] No texture found for tile {tile.id}");
                    }
                    
                    // Only apply material with shader if colorMultiplicatorIsOne is false (exactly like main project)
                    if (!tile.colorMultiplicatorIsOne)
                    {
                        var material = new Material(colorMatrixShader);
                        
                        if (tile.type == 0) // Ground tile
                        {
                            // Ground tiles always divide by 255f
                            material.SetColor(ColorProperty, new Color(
                                tile.colorMultiplicatorR / 255f,
                                tile.colorMultiplicatorG / 255f,
                                tile.colorMultiplicatorB / 255f,
                                1f
                            ));
                        }
                        else // Fixture
                        {
                            // Check if values are in high range (like 256)
                            bool useHighRange = tile.colorMultiplicatorR > 1.0f || 
                                              tile.colorMultiplicatorG > 1.0f || 
                                              tile.colorMultiplicatorB > 1.0f;
                            
                            if (useHighRange)
                            {
                                // Normalize high range values
                                material.SetColor(ColorProperty, new Color(
                                    tile.colorMultiplicatorR / 255f,
                                    tile.colorMultiplicatorG / 255f,
                                    tile.colorMultiplicatorB / 255f,
                                    tile.colorMultiplicatorA > 1.0f ? tile.colorMultiplicatorA / 255f : tile.colorMultiplicatorA
                                ));
                            }
                            else
                            {
                                // Use raw values for normal range
                                material.SetColor(ColorProperty, new Color(
                                    tile.colorMultiplicatorR,
                                    tile.colorMultiplicatorG,
                                    tile.colorMultiplicatorB,
                                    tile.colorMultiplicatorA
                                ));
                            }
                        }
                        
                        // Apply the material to the renderer
                        spriteRenderer.sharedMaterial = material;
                        Debug.Log($"[LoadMapAdapter] Applied custom material to tile {tile.id}");
                    }
                    else
                    {
                        Debug.Log($"[LoadMapAdapter] Using default material for tile {tile.id} (colorMultiplicatorIsOne=true)");
                    }
                }
                
                Debug.Log("[LoadMapAdapter] Finished loading all tiles successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoadMapAdapter] Error loading tiles: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
} 