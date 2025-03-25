#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Components.Maps;
using CreatorMap.Scripts.Data;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace CreatorMap.Scripts.Editor
{
    /// <summary>
    /// Editor utility to clean map components by removing unwanted fields
    /// </summary>
    public static class MapComponentCleaner
    {
        [MenuItem("Window/Map Creator/Clean Map Components")]
        public static void CleanAllMapComponents()
        {
            MapComponent[] mapComponents = GameObject.FindObjectsOfType<MapComponent>();
            
            if (mapComponents.Length == 0)
            {
                Debug.LogWarning("No MapComponent found in the scene to clean.");
                return;
            }
            
            Debug.Log($"Found {mapComponents.Length} MapComponent(s) to clean.");
            
            foreach (MapComponent mapComponent in mapComponents)
            {
                CleanMapComponent(mapComponent);
            }
            
            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            Debug.Log($"Cleaned {mapComponents.Length} MapComponent(s) successfully.");
        }
        
        public static void CleanMapComponent(MapComponent mapComponent)
        {
            if (mapComponent == null || mapComponent.mapInformation == null)
            {
                Debug.LogWarning("Cannot clean null MapComponent or mapInformation.");
                return;
            }
            
            int mapId = mapComponent.mapInformation.id;
            
            // Make a backup of cell data
            Dictionary<ushort, uint> cellBackup = new Dictionary<ushort, uint>();
            if (mapComponent.mapInformation.cells != null && mapComponent.mapInformation.cells.dictionary != null)
            {
                foreach (var pair in mapComponent.mapInformation.cells.dictionary)
                {
                    cellBackup[pair.Key] = pair.Value;
                }
            }
            
            // Create a clean map information instance
            MapBasicInformation cleanInfo = new MapBasicInformation();
            cleanInfo.id = mapId;
            cleanInfo.leftNeighbourId = -1;
            cleanInfo.rightNeighbourId = -1;
            cleanInfo.topNeighbourId = -1;
            cleanInfo.bottomNeighbourId = -1;
            
            // Initialize the cells dictionary
            if (cleanInfo.cells == null)
            {
                cleanInfo.cells = new SerializableDictionary<ushort, uint>();
            }
            
            // Restore cell data from backup or initialize with defaults
            for (ushort i = 0; i < 560; i++)
            {
                uint cellValue = 0x0040; // Default: visible cell (bit 6) + walkable (bit 0 = 0)
                
                // Use backup value if available
                if (cellBackup.ContainsKey(i))
                {
                    cellValue = cellBackup[i];
                }
                
                // Add to dictionary
                if (cleanInfo.cells.dictionary.ContainsKey(i))
                {
                    cleanInfo.cells.dictionary[i] = cellValue;
                }
                else
                {
                    cleanInfo.cells.dictionary.Add(i, cellValue);
                }
            }
            
            // Initialize identifiedElements dictionary
            if (cleanInfo.identifiedElements == null)
            {
                cleanInfo.identifiedElements = new SerializableDictionary<uint, uint>();
            }
            
            // Replace the mapInformation with our clean version
            mapComponent.mapInformation = cleanInfo;
            
            // Set background color to black
            mapComponent.backgroundColor = Color.black;
            
            // Mark the component as dirty
            EditorUtility.SetDirty(mapComponent);
            
            Debug.Log($"Cleaned MapComponent with ID {mapId}");
        }
    }
}
#endif 