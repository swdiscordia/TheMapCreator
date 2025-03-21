#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Reflection;

namespace MapCreator.Editor
{
    /// <summary>
    /// This is a shim class to ensure TileSpriteData is recognized properly
    /// </summary>
    [Serializable]
    public class TileSpriteDataShim
    {
        // Create a direct copy of the TileSpriteData class fields
        public string Id;
        public Vector2 Position;
        public float Scale = 1f;
        public int Order;
        public bool FlipX;
        
        // Store path to the sprite asset in the project
        [NonSerialized] 
        public string PathValue = string.Empty;
        
        // Method to convert a TileSpriteData to our shim using reflection to safely get addressablePath
        public static TileSpriteDataShim FromOriginal(CreatorMap.Scripts.Data.TileSpriteData original)
        {
            string pathValue = string.Empty;
            
            // Use reflection to safely get the addressablePath
            try
            {
                var field = typeof(CreatorMap.Scripts.Data.TileSpriteData).GetField("addressablePath",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (field != null)
                {
                    var value = field.GetValue(original);
                    if (value != null)
                    {
                        pathValue = value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting addressablePath via reflection: {ex.Message}");
            }
            
            var shim = new TileSpriteDataShim
            {
                Id = original.Id,
                Position = original.Position,
                Scale = original.Scale,
                Order = original.Order,
                FlipX = original.FlipX,
                PathValue = pathValue
            };
            return shim;
        }
        
        // Method to convert back using reflection to safely set addressablePath
        public CreatorMap.Scripts.Data.TileSpriteData ToOriginal()
        {
            var original = new CreatorMap.Scripts.Data.TileSpriteData
            {
                Id = this.Id,
                Position = this.Position,
                Scale = this.Scale,
                Order = this.Order,
                FlipX = this.FlipX
            };
            
            // Use reflection to safely set the addressablePath
            try
            {
                var field = typeof(CreatorMap.Scripts.Data.TileSpriteData).GetField("addressablePath",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (field != null)
                {
                    field.SetValue(original, this.PathValue);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting addressablePath via reflection: {ex.Message}");
            }
            
            return original;
        }
    }
}
#endif 