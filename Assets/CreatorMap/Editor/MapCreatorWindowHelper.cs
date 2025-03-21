#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MapCreator.Editor
{
    // This helper class ensures that NewMapCreatorWindow is properly referenced
    [InitializeOnLoad]
    public static class MapCreatorWindowHelper
    {
        static MapCreatorWindowHelper()
        {
            Debug.Log("MapCreatorWindowHelper: Ensuring reference to NewMapCreatorWindow is properly established");
        }
        
        // Helper method to ensure the type is referenced
        public static NewMapCreatorWindow.DrawMode GetCurrentDrawMode()
        {
            try
            {
                var instance = NewMapCreatorWindow.Instance;
                if (instance != null)
                {
                    return NewMapCreatorWindow.CurrentDrawMode;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"MapCreatorWindowHelper: Could not get current draw mode: {ex.Message}");
            }
            
            return NewMapCreatorWindow.DrawMode.None;
        }
    }
}
#endif 