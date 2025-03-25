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
        // Stocke les valeurs comme dans le projet principal
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
        
        /// <summary>
        /// Convertit en ColorMultiplicator compatible avec le projet principal
        /// </summary>
        public ColorMultiplicator ToColorMultiplicator()
        {
            if (IsOne)
            {
                return new ColorMultiplicator(0, 0, 0);
            }
            
            // Conversion en valeurs entières comme dans le projet principal
            int red = Red > 1f ? (int)Red : (int)(Red * 255f);
            int green = Green > 1f ? (int)Green : (int)(Green * 255f);
            int blue = Blue > 1f ? (int)Blue : (int)(Blue * 255f);
            
            return new ColorMultiplicator(red, green, blue, !IsOne);
        }
        
        /// <summary>
        /// Crée un TileColorData à partir d'un ColorMultiplicator
        /// </summary>
        public static TileColorData FromColorMultiplicator(ColorMultiplicator colorMultiplicator)
        {
            if (colorMultiplicator == null || colorMultiplicator.IsOne)
            {
                return new TileColorData(); // Valeurs par défaut (1,1,1,1)
            }
            
            return new TileColorData
            {
                Red = colorMultiplicator.Red,
                Green = colorMultiplicator.Green,
                Blue = colorMultiplicator.Blue,
                Alpha = 1f // Alpha est généralement 1 pour les tiles
            };
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
        public bool FlipX;
        public bool FlipY;
        public TileColorData Color = new TileColorData();
    }
    
    /// <summary>
    /// Container for all sprite data in a map
    /// </summary>
    [Serializable]
    public class MapSpriteData
    {
        public List<TileSpriteData> tiles = new List<TileSpriteData>();
        public List<FixtureSpriteData> fixtures = new List<FixtureSpriteData>();
    }
} 