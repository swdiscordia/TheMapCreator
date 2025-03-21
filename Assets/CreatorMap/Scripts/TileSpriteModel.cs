using UnityEngine;
using CreatorMap.Scripts.Data;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// A model class for tile sprites in the CreatorMap project
    /// Compatible with ploup's TileSpriteModel but independent from it
    /// </summary>
    public class TileSpriteModel
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Order { get; set; }
        public bool ShouldFlipX { get; set; }
        public bool ShouldFlipY { get; set; }
        public ColorMultiplicator ColorMultiplicator { get; set; }
        
        public TileSpriteModel()
        {
            ColorMultiplicator = new ColorMultiplicator();
        }
    }
} 