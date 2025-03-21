using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Simple data model for tile sprite colors
    /// </summary>
    [Serializable]
    public class TileColorData
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
    }
    
    /// <summary>
    /// Basic sprite data for tiles
    /// </summary>
    [Serializable]
    public class TileSpriteData
    {
        public string Id;
        public Vector2 Position;
        public float Scale = 1f;
        public int Order;
        public bool FlipX;
        public bool FlipY;
        public TileColorData Color = new TileColorData();
        public bool IsFixture = false;
        
        // Path to the sprite asset in the project
        [NonSerialized] 
        public string addressablePath = string.Empty; // Initialize with empty string to ensure it's always available
    }
    
    /// <summary>
    /// Basic sprite data for fixtures (decorative elements)
    /// </summary>
    [Serializable]
    public class FixtureSpriteData
    {
        public string Id;
        public Vector2 Position;
        public Vector2 Scale = Vector2.one;
        public float Rotation;
        public int Order;
        public TileColorData Color = new TileColorData();
    }
    
    /// <summary>
    /// Container for all sprite data in a map
    /// </summary>
    [Serializable]
    public class MapSpriteData
    {
        public List<TileSpriteData> Tiles = new List<TileSpriteData>();
        public List<FixtureSpriteData> Fixtures = new List<FixtureSpriteData>();
    }
} 