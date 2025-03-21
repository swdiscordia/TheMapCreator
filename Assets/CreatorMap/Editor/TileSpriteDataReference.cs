#if UNITY_EDITOR
using UnityEngine;
using System.Reflection;
using CreatorMap.Scripts.Data;

namespace MapCreator.Editor
{
    // This class ensures Unity recognizes the TileSpriteData type and its addressablePath property
    public static class TileSpriteDataReference
    {
        // Force the compiler to recognize addressablePath using reflection
        public static void EnsureAddressablePathIsRecognized()
        {
            var testData = new TileSpriteData();
            
            // Use reflection to check if the property exists
            var propertyInfo = typeof(TileSpriteData).GetField("addressablePath", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
            if (propertyInfo != null)
            {
                Debug.Log($"TileSpriteData.addressablePath exists via reflection");
                // Try to set it via reflection
                try 
                {
                    propertyInfo.SetValue(testData, "test-path");
                    Debug.Log($"Successfully set addressablePath via reflection");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to set addressablePath: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("Could not find addressablePath property via reflection");
            }
        }
    }
}
#endif 