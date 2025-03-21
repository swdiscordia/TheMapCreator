#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace MapCreator.Editor
{
    [InitializeOnLoad]
    public static class ForceRebuildTimestamp
    {
        // This timestamp will be updated every time the file is saved,
        // forcing Unity to recompile all dependent scripts
        private static readonly DateTime s_Timestamp = new DateTime(2023, 3, 20, 3, 15, 42);
        
        static ForceRebuildTimestamp()
        {
            Debug.Log($"Forcing rebuild of assemblies at {s_Timestamp}");
            
            // Force recognition of TileSpriteData type
            try
            {
                // Just accessing the type should be enough to force compilation
                var type = typeof(CreatorMap.Scripts.Data.TileSpriteData);
                Debug.Log($"Referenced TileSpriteData type: {type.FullName}");
                
                // Look for addressablePath field via reflection
                var field = type.GetField("addressablePath", 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    
                if (field != null)
                {
                    Debug.Log($"Found addressablePath field: {field.Name}");
                }
                else
                {
                    Debug.LogWarning("Could not find addressablePath field via reflection");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error referencing TileSpriteData: {ex.Message}");
            }
        }
    }
}
#endif 