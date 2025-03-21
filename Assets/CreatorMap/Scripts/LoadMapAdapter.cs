using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using CreatorMap.Scripts.Data;
using Managers.Maps.MapCreator;
using MapCreator.Data.Models;

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
        
        private void Awake()
        {
            mapComponent = GetComponent<MapComponent>();
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
                    tile.colorMultiplicatorR = tileData.Color.Red;
                    tile.colorMultiplicatorG = tileData.Color.Green;
                    tile.colorMultiplicatorB = tileData.Color.Blue;
                    tile.colorMultiplicatorA = tileData.Color.Alpha;
                }
            }
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
    }
} 