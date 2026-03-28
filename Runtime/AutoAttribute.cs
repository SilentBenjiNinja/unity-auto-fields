using System;
using UnityEngine;

namespace bnj.auto_fields.Runtime
{
    /// <summary>
    /// Marks a serialized field for automatic assignment in the Unity Editor.
    /// <para>
    /// On <see cref="Component"/> and <see cref="GameObject"/> fields the assigner calls
    /// <c>GetComponentInChildren</c> on the owning <see cref="UnityEngine.MonoBehaviour"/>.
    /// On <see cref="ScriptableObject"/> fields it searches the project via
    /// <c>AssetDatabase.FindAssets</c>, optionally scoped to a subfolder under
    /// <c>Assets/ScriptableObjects/</c>.
    /// Arrays of either type are supported — all matching instances are collected.
    /// </para>
    /// <para>
    /// Assignment runs automatically on hierarchy changes, scene/prefab stage transitions,
    /// and assembly reloads. Fields are validated on Play Mode entry; any unassigned
    /// field pauses the Editor and logs an error.
    /// </para>
    /// </summary>
    /// <example>
    /// Component on self or children:
    /// <code>
    /// [Auto] Rigidbody _rb;
    /// </code>
    /// Component on a specific child by transform path:
    /// <code>
    /// [Auto("Visuals/Mesh")] MeshRenderer _mesh;
    /// </code>
    /// All components in children:
    /// <code>
    /// [Auto] Collider[] _colliders;
    /// </code>
    /// Single ScriptableObject anywhere in the project:
    /// <code>
    /// [Auto] PlayerConfig _config;
    /// </code>
    /// All ScriptableObjects under Assets/ScriptableObjects/Enemies/:
    /// <code>
    /// [Auto("Enemies")] EnemyData[] _enemies;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoAttribute : PropertyAttribute
    {
        /// <summary>
        /// Optional path hint.
        /// For <see cref="Component"/> fields: a child transform path relative to the owning MonoBehaviour
        /// (e.g. <c>"Visuals/Mesh"</c>).
        /// For <see cref="ScriptableObject"/> fields: a subfolder relative to
        /// <c>Assets/ScriptableObjects/</c> (e.g. <c>"Enemies"</c>).
        /// Leave empty to search on self / children or anywhere in the project.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Marks this field for automatic assignment.
        /// </summary>
        /// <param name="path">
        /// Optional path hint. For Components: child transform path. For ScriptableObjects: subfolder
        /// under <c>Assets/ScriptableObjects/</c>. Leave empty to search on self/children or project-wide.
        /// </param>
        public AutoAttribute(string path = "")
        {
            Path = path;
        }
    }
}
