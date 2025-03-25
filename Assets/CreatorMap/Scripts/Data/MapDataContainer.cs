using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CreatorMap.Scripts.Core.Grid;
using CreatorMap.Scripts.Data;

namespace CreatorMap.Scripts.Data
{
    /// <summary>
    /// Container class for storing multiple map data in one file, matching the main project's approach.
    /// </summary>
    [Serializable]
    public class MapDataContainer
    {
        /// <summary>
        /// Dictionary mapping map IDs to their serialized data
        /// </summary>
        public Dictionary<long, MapEntry> Maps { get; private set; } = new Dictionary<long, MapEntry>();

        /// <summary>
        /// Updates a map in the container with data from the GridManager
        /// </summary>
        public void UpdateMap(MapCreatorGridManager.GridData gridData)
        {
            if (!Maps.TryGetValue(gridData.id, out var entry))
            {
                entry = new MapEntry { Id = gridData.id };
                Maps[gridData.id] = entry;
            }
            
            // Clear existing cells and update from grid data
            entry.Cells.Clear();
            foreach (var cell in gridData.cells)
            {
                entry.Cells[cell.cellId] = cell.flags;
            }
        }

        /// <summary>
        /// Serialize the container to a binary writer
        /// </summary>
        public void Serialize(BinaryWriter writer)
        {
            try
            {
                // Write version
                writer.Write((byte)1);
                
                // Write map count
                writer.Write(Maps.Count);
                
                // Write each map
                foreach (var pair in Maps)
                {
                    var entry = pair.Value;
                    
                    // Write basic map info
                    writer.Write(entry.Id);
                    
                    // Write cell data
                    writer.Write(entry.Cells.Count);
                    foreach (var cellPair in entry.Cells)
                    {
                        writer.Write(cellPair.Key);
                        writer.Write(cellPair.Value);
                    }
                    
                    // Write tile sprite data
                    writer.Write(entry.SpriteData.tiles.Count);
                    foreach (var tile in entry.SpriteData.tiles)
                    {
                        writer.Write(tile.Id ?? string.Empty);
                        writer.Write(tile.Position.x);
                        writer.Write(tile.Position.y);
                        writer.Write(tile.Scale);
                        writer.Write(tile.Order);
                        writer.Write(tile.FlipX);
                        writer.Write(tile.Color.Red);
                        writer.Write(tile.Color.Green);
                        writer.Write(tile.Color.Blue);
                        writer.Write(tile.Color.Alpha);
                    }
                    
                    // Write fixture sprite data
                    writer.Write(entry.SpriteData.fixtures.Count);
                    foreach (var fixture in entry.SpriteData.fixtures)
                    {
                        writer.Write(fixture.Id ?? string.Empty);
                        writer.Write(fixture.Position.x);
                        writer.Write(fixture.Position.y);
                        writer.Write(fixture.Scale.x);
                        writer.Write(fixture.Scale.y);
                        writer.Write(fixture.Rotation);
                        writer.Write(fixture.Order);
                        writer.Write(fixture.Color.Red);
                        writer.Write(fixture.Color.Green);
                        writer.Write(fixture.Color.Blue);
                        writer.Write(fixture.Color.Alpha);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error serializing map data container: {ex.Message}");
            }
        }

        /// <summary>
        /// Deserialize the container from a binary reader
        /// </summary>
        public static MapDataContainer Deserialize(BinaryReader reader)
        {
            var container = new MapDataContainer();
            
            try
            {
                // Read version
                var version = reader.ReadByte();
                
                // Read map count
                var mapCount = reader.ReadInt32();
                
                // Read each map
                for (int i = 0; i < mapCount; i++)
                {
                    var entry = new MapEntry();
                    
                    // Read basic map info
                    entry.Id = reader.ReadInt64();
                    
                    // Read cell count
                    var cellCount = reader.ReadInt32();
                    
                    // Read cell data
                    for (int j = 0; j < cellCount; j++)
                    {
                        var cellId = reader.ReadUInt16();
                        var cellData = reader.ReadUInt16();
                        entry.Cells[cellId] = cellData;
                    }
                    
                    // Read tile sprite data
                    var tileCount = reader.ReadInt32();
                    for (int j = 0; j < tileCount; j++)
                    {
                        var tile = new TileSpriteData();
                        tile.Id = reader.ReadString();
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        tile.Position = new Vector2(x, y);
                        tile.Scale = reader.ReadSingle();
                        tile.Order = reader.ReadInt32();
                        tile.FlipX = reader.ReadBoolean();
                        tile.Color.Red = reader.ReadSingle();
                        tile.Color.Green = reader.ReadSingle();
                        tile.Color.Blue = reader.ReadSingle();
                        tile.Color.Alpha = reader.ReadSingle();
                        
                        entry.SpriteData.tiles.Add(tile);
                    }
                    
                    // Read fixture sprite data
                    var fixtureCount = reader.ReadInt32();
                    for (int j = 0; j < fixtureCount; j++)
                    {
                        var fixture = new FixtureSpriteData();
                        fixture.Id = reader.ReadString();
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        fixture.Position = new Vector2(x, y);
                        float scaleX = reader.ReadSingle();
                        float scaleY = reader.ReadSingle();
                        fixture.Scale = new Vector2(scaleX, scaleY);
                        fixture.Rotation = reader.ReadSingle();
                        fixture.Order = reader.ReadInt32();
                        fixture.Color.Red = reader.ReadSingle();
                        fixture.Color.Green = reader.ReadSingle();
                        fixture.Color.Blue = reader.ReadSingle();
                        fixture.Color.Alpha = reader.ReadSingle();
                        
                        entry.SpriteData.fixtures.Add(fixture);
                    }
                    
                    // Add to container
                    container.Maps[entry.Id] = entry;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing map data container: {ex.Message}");
            }
            
            return container;
        }
        
        /// <summary>
        /// Converts a map entry to GridData format
        /// </summary>
        public MapCreatorGridManager.GridData ToGridData(long mapId)
        {
            if (!Maps.TryGetValue(mapId, out var entry))
            {
                Debug.LogWarning($"No map data found for ID {mapId}");
                return new MapCreatorGridManager.GridData { id = (int)mapId };
            }
            
            var gridData = new MapCreatorGridManager.GridData
            {
                id = (int)mapId,
                cells = new List<MapCreatorGridManager.CellData>()
            };
            
            // Copy cell data from entry to gridData
            foreach (var cellPair in entry.Cells)
            {
                gridData.cells.Add(new MapCreatorGridManager.CellData(cellPair.Key, cellPair.Value));
                gridData.cellsDict[cellPair.Key] = cellPair.Value;
            }
            
            return gridData;
        }
    }

    /// <summary>
    /// Entry for a single map in the container
    /// </summary>
    [Serializable]
    public class MapEntry
    {
        public long Id { get; set; }
        public Dictionary<ushort, uint> Cells { get; set; } = new Dictionary<ushort, uint>();
        public MapSpriteData SpriteData { get; set; } = new MapSpriteData();
    }
} 