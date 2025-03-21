#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections.Generic;

namespace MapCreator.Editor
{
    /// <summary>
    /// This is a direct copy of TileSpriteData for editor-only use
    /// </summary>
    [Serializable]
    public class EditorTileSpriteData
    {
        public string Id;
        public Vector2 Position;
        public float Scale = 1f;
        public int Order;
        public bool FlipX;
        public EditorTileColorData Color = new EditorTileColorData();
        
        // Path to the sprite asset in the project
        [NonSerialized] 
        public string AddressablePath = string.Empty;
        
        // Convert from runtime TileSpriteData
        public static EditorTileSpriteData FromRuntime(CreatorMap.Scripts.Data.TileSpriteData runtimeData)
        {
            return new EditorTileSpriteData
            {
                Id = runtimeData.Id,
                Position = runtimeData.Position,
                Scale = runtimeData.Scale,
                Order = runtimeData.Order,
                FlipX = runtimeData.FlipX,
                Color = EditorTileColorData.FromRuntime(runtimeData.Color),
                // We can't directly access addressablePath, use empty string
                AddressablePath = string.Empty
            };
        }
        
        // Convert to runtime TileSpriteData
        public CreatorMap.Scripts.Data.TileSpriteData ToRuntime()
        {
            var result = new CreatorMap.Scripts.Data.TileSpriteData
            {
                Id = this.Id,
                Position = this.Position,
                Scale = this.Scale,
                Order = this.Order,
                FlipX = this.FlipX,
                Color = this.Color.ToRuntime()
            };
            
            // Can't set addressablePath directly, will be set by dictionary lookup
            return result;
        }
    }
    
    /// <summary>
    /// This is a direct copy of TileColorData for editor-only use
    /// </summary>
    [Serializable]
    public class EditorTileColorData
    {
        public float Red = 1f;
        public float Green = 1f;
        public float Blue = 1f;
        public float Alpha = 1f;
        
        public bool IsOne => 
            Mathf.Approximately(Red, 1f) && 
            Mathf.Approximately(Green, 1f) && 
            Mathf.Approximately(Blue, 1f) && 
            Mathf.Approximately(Alpha, 1f);
            
        public Color ToColor()
        {
            return new Color(Red, Green, Blue, Alpha);
        }
        
        // Convert from runtime TileColorData
        public static EditorTileColorData FromRuntime(CreatorMap.Scripts.Data.TileColorData runtimeData)
        {
            return new EditorTileColorData
            {
                Red = runtimeData.Red,
                Green = runtimeData.Green,
                Blue = runtimeData.Blue,
                Alpha = runtimeData.Alpha
            };
        }
        
        // Convert to runtime TileColorData
        public CreatorMap.Scripts.Data.TileColorData ToRuntime()
        {
            return new CreatorMap.Scripts.Data.TileColorData
            {
                Red = this.Red,
                Green = this.Green,
                Blue = this.Blue,
                Alpha = this.Alpha
            };
        }
    }
}
#endif 