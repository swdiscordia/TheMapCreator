using UnityEngine;
using Managers.Maps.MapCreator;

namespace CreatorMap.Scripts
{
    /// <summary>
    /// Initializes the map component and prepares it for loading at runtime
    /// This component should be added to the same GameObject as the MapComponent
    /// </summary>
    [RequireComponent(typeof(MapComponent))]
    public class MapComponentInitializer : MonoBehaviour
    {
        private LoadMapAdapter _loadAdapter;
        
        private void Awake()
        {
            // Add the load adapter if it doesn't exist
            _loadAdapter = GetComponent<LoadMapAdapter>();
            if (_loadAdapter == null)
            {
                _loadAdapter = gameObject.AddComponent<LoadMapAdapter>();
                Debug.Log("Added LoadMapAdapter component to prepare map for loading");
            }
        }
        
        private void Start()
        {
            // Prepare the map for loading at runtime
            _loadAdapter.PrepareMapForLoading();
            Debug.Log("Map prepared for loading at runtime");
        }
    }
} 