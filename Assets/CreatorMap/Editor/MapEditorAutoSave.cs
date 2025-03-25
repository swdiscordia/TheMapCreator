#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;

namespace CreatorMap.Scripts.Editor
{
    /// <summary>
    /// Automatically saves map data before entering play mode
    /// </summary>
    [InitializeOnLoad]
    public static class MapEditorAutoSave
    {
        // Static constructor is called when Unity loads the script
        static MapEditorAutoSave()
        {
            // Register for the playModeStateChanged event
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Debug.Log("Map Editor AutoSave initialized");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // If we're about to enter play mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Find all map components in the scene
                var mapComponents = Object.FindObjectsOfType<Components.Maps.MapComponent>();
                
                if (mapComponents.Length > 0)
                {
                    Debug.Log($"AutoSave: Found {mapComponents.Length} map components to save before play mode");
                    
                    // Mark all map components as dirty
                    foreach (var component in mapComponents)
                    {
                        EditorUtility.SetDirty(component);
                    }
                    
                    // Save the active scene
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    Debug.Log("AutoSave: Scene saved successfully before entering play mode");
                }
            }
        }
        
        // Ensure map data is saved when going from edit to play mode
        [PostProcessScene]
        public static void OnPostprocessScene()
        {
            // This gets called when building or when entering play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                var mapComponents = Object.FindObjectsOfType<Components.Maps.MapComponent>();
                
                if (mapComponents.Length > 0)
                {
                    Debug.Log($"PostProcessScene: Found {mapComponents.Length} map components");
                    
                    // Additional check to ensure data is preserved
                    foreach (var component in mapComponents)
                    {
                        if (component.mapInformation != null && component.mapInformation.cells != null)
                        {
                            int cellCount = component.mapInformation.cells.dictionary.Count;
                            Debug.Log($"PostProcessScene: Map {component.mapInformation.id} has {cellCount} cells");
                        }
                    }
                }
            }
        }
    }
}
#endif 