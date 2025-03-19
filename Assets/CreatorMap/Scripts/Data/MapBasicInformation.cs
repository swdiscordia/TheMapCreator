using System.Collections.Generic;
using CreatorMap.Scripts.Data;

namespace MapCreator.Data.Models
{
    public class SerializableDictionary<TKey, TValue>
    {
        public Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
    }

    public class MapBasicInformation
    {
        public long id;
        public long leftNeighbourId = -1;
        public long rightNeighbourId = -1;
        public long topNeighbourId = -1;
        public long bottomNeighbourId = -1;
        public SerializableDictionary<ushort, ushort> cells = new SerializableDictionary<ushort, ushort>();
        
        // Sprite data
        public MapSpriteData SpriteData = new MapSpriteData();
    }
} 