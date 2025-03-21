#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;

namespace MapCreator.Editor
{
    // This class forces Unity to recompile all scripts
    [InitializeOnLoad]
    public static class ForceRecompile
    {
        // Compilation timestamp
        private static readonly DateTime s_CompileTime = DateTime.Now;

        static ForceRecompile()
        {
            Debug.Log($"ForceRecompile: Initialized at {s_CompileTime}");
            
            try
            {
                // Validate references to ensure they're properly included in compilation
                var drawMode = NewMapCreatorWindow.DrawMode.None;
                
                // Only try to get current draw mode if it's safe to do so
                var helper = MapCreatorWindowHelper.GetCurrentDrawMode();
                
                // Log class names to verify correct references
                Debug.Log($"ForceRecompile: Referencing NewMapCreatorWindow.DrawMode: {drawMode}");
                Debug.Log($"ForceRecompile: Helper mode: {helper}");
                
                // Force SceneGUI update
                SceneView.RepaintAll();
            }
            catch (Exception ex)
            {
                // Log the error but don't let it crash the initialization
                Debug.LogWarning($"ForceRecompile: Exception during initialization (this is usually normal on first load): {ex.Message}");
            }
        }
    }
}
#endif 