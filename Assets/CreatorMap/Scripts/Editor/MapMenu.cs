using UnityEngine;
using UnityEditor;
using CreatorMap.Scripts.Core;
using CreatorMap.Scripts.Core.Grid;
using Components.Maps;

namespace CreatorMap.Scripts.Editor
{
    /// <summary>
    /// Menu pour créer rapidement les objets nécessaires à l'édition de carte
    /// </summary>
    public static class MapMenu
    {
        [MenuItem("GameObject/Map Creator/Create Map Setup", false, 10)]
        public static void CreateMapSetup()
        {
            // Vérifier si les objets existent déjà
            if (GameObject.Find("Map"))
            {
                if (!EditorUtility.DisplayDialog("Éléments existants", 
                    "Un objet 'Map' existe déjà dans la scène. Voulez-vous quand même créer un nouveau setup?", 
                    "Oui", "Non"))
                {
                    return;
                }
            }
            
            // Créer l'objet parent Map
            var mapObject = new GameObject("Map");
            Undo.RegisterCreatedObjectUndo(mapObject, "Create Map Object");
            
            // Créer le GridManager
            var gridManagerObject = new GameObject("GridManager");
            gridManagerObject.transform.SetParent(mapObject.transform);
            var gridManager = gridManagerObject.AddComponent<MapCreatorGridManager>();
            Undo.RegisterCreatedObjectUndo(gridManagerObject, "Create GridManager Object");
            
            // Ajouter le controller pour l'éditeur
            var editorController = gridManagerObject.AddComponent<MapEditorController>();
            
            // Ajouter le MapComponent pour compatibilité avec le projet principal
            var mapComponent = mapObject.AddComponent<MapComponent>();
            mapComponent.backgroundColor = Color.black;
            
            // Sélectionner l'objet Map
            Selection.activeGameObject = mapObject;
            
            Debug.Log("Configuration Map Creator créée avec succès !");
            
            // Ouvrir l'éditeur de carte
            MapEditorWindow.ShowWindow();
        }
    }
} 