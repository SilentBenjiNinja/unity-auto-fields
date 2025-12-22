using System.Reflection;
using bnj.auto_fields.Runtime;
using UnityEditor;
using UnityEngine;

namespace bnj.auto_fields.Editor
{
    public static class AutoAssignerContextMenu
    {
        [MenuItem("CONTEXT/MonoBehaviour/Refresh Auto-Assigned Fields")]
        private static void RefreshAutoFields(MenuCommand command)
        {
            var behavior = command.context as MonoBehaviour;
            if (behavior == null) return;

            var fields = behavior.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var refreshedCount = 0;

            foreach (var field in fields)
            {
                var autoAttribute = field.GetCustomAttribute<AutoAttribute>();
                if (autoAttribute == null) continue;

                // Temporarily clear the field to force reassignment
                field.SetValue(behavior, null);
                refreshedCount++;
            }

            if (refreshedCount > 0)
            {
                // Trigger auto-assignment
                EditorUtility.SetDirty(behavior);
                AutoAssigner.ForceRefreshMonoBehaviour(behavior);
                Debug.Log($"[AutoAssigner] Refreshed {refreshedCount} auto-assigned field(s) on {behavior.name}");
            }
            else
            {
                Debug.Log($"[AutoAssigner] No auto-assigned fields found on {behavior.name}");
            }
        }

        [MenuItem("CONTEXT/MonoBehaviour/Refresh Auto-Assigned Fields", true)]
        private static bool ValidateRefreshAutoFields(MenuCommand command)
        {
            var behavior = command.context as MonoBehaviour;
            if (behavior == null) return false;

            // Only show menu if there are auto-assigned fields
            var fields = behavior.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<AutoAttribute>() != null)
                    return true;
            }

            return false;
        }
    }
}
