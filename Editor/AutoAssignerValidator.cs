using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using bnj.auto_fields.Runtime;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// TODO:
// - Optimize performance by caching results where possible
// - Other collection types (List, etc.)

namespace bnj.auto_fields.Editor
{
    /// <summary>
    /// Automatically assigns fields marked with [Auto] attribute when hierarchy changes.
    /// Validates assignments on play mode entry.
    /// </summary>
    /// <remarks>
    /// Inspired by Tutorial by Freedom Coding: https://youtu.be/cVxjKphoi5o
    /// </remarks>
    [InitializeOnLoad]
    public static class AutoAssignerValidator
    {
        private const BindingFlags _fieldBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static int _lastHierarchyVersion = -1;
        private static string _lastScenePath = string.Empty;
        private static PrefabStage _lastPrefabStage = null;

        static AutoAssignerValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ValidateAndPauseIfNecessary();
            }
        }

        private static void OnHierarchyChanged()
        {
            if (HasHierarchyActuallyChanged())
            {
                AutoAssignAllFields();
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            _lastScenePath = scene.path;
            _lastHierarchyVersion = -1;
            AutoAssignAllFields();
        }

        private static void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            _lastPrefabStage = prefabStage;
            _lastHierarchyVersion = -1;
            AutoAssignAllFields();
        }

        private static void OnPrefabStageClosing(PrefabStage prefabStage)
        {
            _lastPrefabStage = null;
            _lastHierarchyVersion = -1;
        }

        private static void OnAfterAssemblyReload()
        {
            _lastHierarchyVersion = -1;
            AutoAssignAllFields();
        }

        /// <summary>
        /// Checks if the hierarchy has actually changed in a meaningful way.
        /// </summary>
        private static bool HasHierarchyActuallyChanged()
        {
            var activeScene = SceneManager.GetActiveScene();
            var currentScenePath = activeScene.path;
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            // Check if we switched scenes
            if (currentScenePath != _lastScenePath)
            {
                _lastScenePath = currentScenePath;
                _lastHierarchyVersion = -1;
                return true;
            }

            // Check if we switched prefab stages
            if (currentPrefabStage != _lastPrefabStage)
            {
                _lastPrefabStage = currentPrefabStage;
                _lastHierarchyVersion = -1;
                return true;
            }

            // Use a hash of the hierarchy to detect actual changes
            var currentHierarchyVersion = CalculateHierarchyHash();
            if (currentHierarchyVersion != _lastHierarchyVersion)
            {
                _lastHierarchyVersion = currentHierarchyVersion;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates a simple hash of the current hierarchy state.
        /// </summary>
        private static int CalculateHierarchyHash()
        {
            var hash = 0;
            var rootGameObjects = GetCurrentRootGameObjects();

            foreach (var root in rootGameObjects)
            {
                hash ^= root.GetInstanceID();
                hash ^= root.transform.childCount;
            }

            return hash;
        }

        /// <summary>
        /// Gets root GameObjects from either the current scene or prefab stage.
        /// </summary>
        private static GameObject[] GetCurrentRootGameObjects()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                return new[] { prefabStage.prefabContentsRoot };
            }

            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.GetRootGameObjects() : Array.Empty<GameObject>();
        }

        /// <summary>
        /// Finds all MonoBehaviours in the scene and auto-assigns their [Auto] fields.
        /// </summary>
        private static void AutoAssignAllFields()
        {
            MonoBehaviour[] monoBehaviors;

            // Check if we're in prefab stage
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // In prefab mode, get components from prefab root
                monoBehaviors = prefabStage.prefabContentsRoot.GetComponentsInChildren<MonoBehaviour>(true);
            }
            else
            {
                // In scene mode, find all MonoBehaviours in the scene
                monoBehaviors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            }

            if (monoBehaviors == null || monoBehaviors.Length == 0) return;

            var hasChanges = false;

            foreach (var behavior in monoBehaviors)
            {
                if (ProcessMonoBehaviour(behavior))
                {
                    hasChanges = true;
                }
            }

            // Only mark dirty if we actually made changes
            if (hasChanges)
            {
                if (prefabStage != null)
                {
                    // In prefab mode, mark the prefab stage dirty
                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                }
                else
                {
                    // In scene mode, mark the active scene dirty
                    var activeScene = SceneManager.GetActiveScene();
                    if (activeScene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                }
            }
        }

        /// <summary>
        /// Processes a single MonoBehaviour and assigns all fields with [Auto] attribute.
        /// Returns true if any changes were made.
        /// </summary>
        private static bool ProcessMonoBehaviour(MonoBehaviour behavior)
        {
            var fields = behavior.GetType().GetFields(_fieldBindingFlags);
            var autoAssignFields = fields.Where(field => field.GetCustomAttribute<AutoAttribute>() != null);
            var hasChanges = false;

            foreach (var field in autoAssignFields)
            {
                if (TryAssignField(behavior, field))
                {
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        /// <summary>
        /// Attempts to automatically assign a single field.
        /// Returns true if the field was modified.
        /// </summary>
        private static bool TryAssignField(MonoBehaviour behavior, FieldInfo field)
        {
            // Skip if field is already assigned
            var currentValue = field.GetValue(behavior);
            if (currentValue != null && !(currentValue is Object unityObj && unityObj == null))
            {
                return false;
            }

            var autoAttribute = field.GetCustomAttribute<AutoAttribute>();
            var fieldType = field.FieldType;
            var path = autoAttribute.Path;
            var isArray = fieldType.IsArray;

            // Extract element type if array
            if (isArray)
            {
                fieldType = fieldType.GetElementType();
            }

            // Handle ScriptableObject assignment
            if (fieldType != null && fieldType.IsSubclassOf(typeof(ScriptableObject)))
            {
                return AssignScriptableObject(behavior, field, fieldType, path, isArray);
            }

            // Handle Component/GameObject assignment
            return AssignComponentOrGameObject(behavior, field, fieldType, path, isArray);
        }

        /// <summary>
        /// Assigns a ScriptableObject to the field (finds instance(s) in the project).
        /// If path is specified, searches only in that folder (relative to Assets/ScriptableObjects/).
        /// If array, assigns all found instances; otherwise assigns first one.
        /// Returns true if assignment was made.
        /// </summary>
        private static bool AssignScriptableObject(MonoBehaviour behavior, FieldInfo field, Type fieldType, string path, bool isArray)
        {
            // Build search path - if path specified, search only in that folder
            string searchFilter = $"t:{fieldType}";
            string[] searchFolders = null;

            if (!string.IsNullOrEmpty(path))
            {
                // Normalize path to be relative to Assets/ScriptableObjects/
                var folderPath = path.StartsWith("Assets/ScriptableObjects/") ? path : $"Assets/ScriptableObjects/{path}";
                searchFolders = new[] { folderPath };
            }

            var guids = searchFolders != null
                ? AssetDatabase.FindAssets(searchFilter, searchFolders)
                : AssetDatabase.FindAssets(searchFilter);

            if (guids == null || guids.Length == 0)
            {
                var pathInfo = searchFolders != null ? $" in folder '{searchFolders[0]}'" : "";
                Debug.LogError($"[AutoAssigner] No instance of {fieldType} found{pathInfo}!", behavior);
                return false;
            }

            if (isArray)
            {
                // Load all found assets into an array
                var arrayType = fieldType.MakeArrayType();
                var assets = Array.CreateInstance(fieldType, guids.Length);

                for (int i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, fieldType);
                    assets.SetValue(asset, i);
                }

                field.SetValue(behavior, assets);
                EditorUtility.SetDirty(behavior);

                var pathInfo = searchFolders != null ? $" in folder '{searchFolders[0]}'" : " in project";
                Debug.Log($"[AutoAssigner] Assigned {guids.Length} {fieldType.Name}(s){pathInfo} to {behavior.name}.{field.Name}");
                return true;
            }
            else
            {
                // Single assignment - use first found
                if (guids.Length > 1)
                {
                    var pathInfo = searchFolders != null ? $" in folder '{searchFolders[0]}'" : "";
                    Debug.LogWarning($"[AutoAssigner] Multiple instances of {fieldType} found{pathInfo}. Using first one.", behavior);
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, fieldType);

                if (asset != null)
                {
                    field.SetValue(behavior, asset);
                    EditorUtility.SetDirty(behavior);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Assigns a Component or GameObject to the field (or array of them).
        /// Returns true if assignment was made.
        /// </summary>
        private static bool AssignComponentOrGameObject(MonoBehaviour behavior, FieldInfo field, Type fieldType, string path, bool isArray)
        {
            // Find the parent transform (either specified path or self)
            var parentTransform = string.IsNullOrEmpty(path)
                ? behavior.transform
                : behavior.transform.Find(path);

            if (parentTransform == null)
            {
                Debug.LogError($"[AutoAssigner] Could not find child path '{path}' in {behavior.name}", behavior);
                return false;
            }

            object value;

            if (isArray)
            {
                value = GetComponentsArray(parentTransform, fieldType);
            }
            else
            {
                value = GetSingleComponent(parentTransform, fieldType);
            }

            if (value == null)
            {
                Debug.LogError($"[AutoAssigner] Could not assign field '{field.Name}' of type {fieldType} in {behavior.name} (path: '{path}')", behavior);
                return false;
            }

            field.SetValue(behavior, value);
            EditorUtility.SetDirty(behavior);
            return true;
        }

        /// <summary>
        /// Gets a single component or GameObject from the transform.
        /// </summary>
        private static object GetSingleComponent(Transform parentTransform, Type fieldType)
        {
            if (fieldType == typeof(GameObject))
            {
                return parentTransform.gameObject;
            }

            return parentTransform.GetComponentInChildren(fieldType, includeInactive: true);
        }

        /// <summary>
        /// Gets an array of components or GameObjects from the transform and its children.
        /// </summary>
        private static object GetComponentsArray(Transform parentTransform, Type fieldType)
        {
            if (fieldType == typeof(GameObject))
            {
                // Special case: GameObject arrays need manual collection
                var gameObjects = new List<GameObject>();
                CollectGameObjects(parentTransform, gameObjects);
                return gameObjects.ToArray();
            }

            if (fieldType == typeof(Transform))
            {
                // Special case: Transform arrays need manual collection
                var transforms = new List<Transform>();
                CollectTransforms(parentTransform, transforms);
                return transforms.ToArray();
            }

            // Try using the generic GetComponentsInChildren<T> method first
            // This works for most component types including abstract ones like Collider
            try
            {
                var genericMethod = typeof(Component).GetMethod(
                    nameof(Component.GetComponentsInChildren),
                    new[] { typeof(bool) });

                if (genericMethod != null)
                {
                    var typedMethod = genericMethod.MakeGenericMethod(fieldType);
                    var result = typedMethod.Invoke(parentTransform, new object[] { true });

                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // Fall through to reflection-based approach
            }

            // Fallback: Use reflection to call GetComponentsInChildren<T> with List parameter
            var listType = typeof(List<>).MakeGenericType(fieldType);
            var listInstance = Activator.CreateInstance(listType);

            var getComponentsMethod = typeof(Component).GetMethod(
                nameof(Component.GetComponentsInChildren),
                new[] { typeof(bool), listType });



            if (getComponentsMethod == null)
            {
                Debug.LogError($"[AutoAssigner] Could not find GetComponentsInChildren method for type {fieldType}");
                return null;
            }

            getComponentsMethod.Invoke(parentTransform, new[] { true, listInstance });

            // Convert List<T> to T[]
            var toArrayMethod = listType.GetMethod(nameof(List<object>.ToArray));
            return toArrayMethod?.Invoke(listInstance, null);
        }

        // Gathers all child GameObjects of the Transform, including disabled ones.
        private static void CollectGameObjects(Transform parent, List<GameObject> gameObjects)
        {
            if (parent == null) return;

            // Add this GameObject
            gameObjects.Add(parent.gameObject);

            // Recursively add all child GameObjects
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                CollectGameObjects(child, gameObjects);
            }
        }

        // Gathers all child Transforms of the Transform, including disabled ones.
        private static void CollectTransforms(Transform parent, List<Transform> transforms)
        {
            if (parent == null) return;

            // Add this Transform
            transforms.Add(parent);

            // Recursively add all child Transforms
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                CollectTransforms(child, transforms);
            }
        }


        /// <summary>
        /// Validates all auto-assigned fields on play mode entry and pauses if any are null.
        /// </summary>
        private static void ValidateAndPauseIfNecessary()
        {
            var hasErrors = false;
            var monoBehaviors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var monoBehavior in monoBehaviors)
            {
                var fields = monoBehavior.GetType().GetFields(_fieldBindingFlags);

                foreach (var field in fields)
                {
                    var autoAttribute = field.GetCustomAttribute<AutoAttribute>();
                    if (autoAttribute == null) continue;

                    var value = field.GetValue(monoBehavior);
                    if (value == null || (value is Object unityObj && unityObj == null))
                    {
                        Debug.LogError($"[AutoAssigner] Field '{field.Name}' in {monoBehavior.GetType().Name} is null despite [Auto] attribute!", monoBehavior);
                        hasErrors = true;
                    }
                }
            }

            if (hasErrors)
            {
                Debug.LogError("[AutoAssigner] Pausing playmode due to unassigned auto-fields. Fix the errors and try again.");
                EditorApplication.isPaused = true;
            }
        }

        /// <summary>
        /// Forces a refresh of auto-assigned fields for a specific MonoBehaviour.
        /// Used by context menu to manually refresh fields.
        /// </summary>
        public static void ForceRefreshMonoBehaviour(MonoBehaviour behavior)
        {
            if (behavior == null) return;
            ProcessMonoBehaviour(behavior);
        }
    }
}
