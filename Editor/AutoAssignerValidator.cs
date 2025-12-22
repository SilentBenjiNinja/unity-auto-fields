using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using bnj.auto_fields.Runtime;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// TODO:
// - make this work with scriptable objects if only one instance exists!
// - make this work with prefabs!
// - halt player if something in the scene is found unassigned after auto-assign!
// - make this work with children of children too!

namespace bnj.auto_fields.Editor
{
    // Credit to Tutorial by Freedom Coding:
    // https://youtu.be/cVxjKphoi5o
    [InitializeOnLoad]
    public static class AutoAssignerValidator
    {
        static AutoAssignerValidator()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                // to use ExitingEditMode, make sure console doesnt clear on play!
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                //AutoAssign();
                DebugWarnings();
            };
            EditorApplication.hierarchyChanged += () =>
            {
                //DebugUtils.Info($"Hierarchy changed", "autofield");
                AutoAssign();
            };
        }

        static void AutoAssign()
        {
            var monoBehaviors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            if (monoBehaviors == null) return;

            //DebugUtils.Log($"{monoBehaviors.Length} MBs found","autofield");
            monoBehaviors.ForEach(behavior =>
            {
                // TODO: make it work on prefabs too, unless they reference a parent!
                var fields = behavior.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var autoAssignFields = fields.Where(field => field.GetCustomAttribute<AutoAttribute>() != null);

                if (autoAssignFields.Count() == 0) return;
                //DebugUtils.Log($"{autoAssignFields.Count()}/{fields.Count()} auto-assign fields in {behavior.name}","autofield");

                autoAssignFields.ForEach(autoAssignField =>
                {
                    var autoAssignAttribute = autoAssignField.GetCustomAttribute<AutoAttribute>();

                    var fieldType = autoAssignField.FieldType;
                    var autoAssignPath = autoAssignAttribute.Path;
                    var isArray = fieldType.IsArray;
                    if (isArray) fieldType = fieldType.GetElementType();

                    if (fieldType.IsSubclassOf(typeof(ScriptableObject)))
                    {
                        var guids = AssetDatabase.FindAssets($"t:{fieldType}");
                        if (guids.IsNullOrEmpty())
                        {
                            Debug.LogError($"Found no instance of {fieldType}!");
                            return;
                        }

                        // TODO: make this work! (right now does not seem to set field info value)
                        var assetPath = AssetDatabase.GUIDToAssetPath(guids.First());
                        var asset = AssetDatabase.LoadAssetAtPath(assetPath, fieldType);
                        //LogUtils.Debug($"Found {guids.Length} at {assetPath}: {asset}");
                        autoAssignField.SetValue(behavior, asset);
                        return;
                    }

                    var parentTransform = !string.IsNullOrEmpty(autoAssignPath) ? behavior.transform.Find(autoAssignPath) : behavior.transform;

                    if (parentTransform == null)
                    {
                        Debug.LogError($"Could not find transform named {autoAssignPath} in {behavior.name}!");
                        return;
                    }

                    var newValue = (object)(fieldType == typeof(GameObject) ? (Object)parentTransform.gameObject :
                        parentTransform.GetComponentInChildren(fieldType, true));

                    if (isArray)
                    {
                        var listType = typeof(List<>).MakeGenericType(fieldType);
                        var listInstance = Activator.CreateInstance(listType);

                        // Use reflection to call GetComponentsInChildren<T>(bool, List<T>)
                        var getComponentsMethod = typeof(Component).GetMethod(
                            "GetComponentsInChildren",
                            new[] { typeof(bool), listType });

                        getComponentsMethod.Invoke(parentTransform, new[] { true, listInstance });

                        // Use reflection to call ToArray() on the list
                        var toArrayMethod = listType.GetMethod("ToArray");
                        var asArray = toArrayMethod.Invoke(listInstance, null);
                        newValue = asArray;

                        //DebugUtils.Log($"{asArray.Length}", "autofield array");
                    }

                    if (newValue == null)
                    {
                        Debug.LogError($"Could not assign: {behavior.name}/{autoAssignPath}/{fieldType}");
                        return;
                    }

                    autoAssignField.SetValue(behavior, newValue);
                    //DebugUtils.Log($"{behavior.name}/{autoAssignField.Name}: {autoAssignField.GetValue(behavior)}", "autofield after");
                });
            });
        }

        static void DebugWarnings()
        {
            var shouldPauseGame = false;
            var monoBehaviors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var monoBehavior in monoBehaviors)
            {
                var fields = monoBehavior.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    var autoAssignAttribute = field.GetCustomAttribute<AutoAttribute>();
                    if (autoAssignAttribute == null) continue;

                    if (field.GetValue(monoBehavior) == null)
                    {
                        Debug.LogError($"Could not set {field.Name} automatically for {monoBehavior}.");
                        shouldPauseGame = true;
                    }
                }
            }

            if (shouldPauseGame)
            {
                EditorApplication.isPaused = true;
            }
        }
    }
}
