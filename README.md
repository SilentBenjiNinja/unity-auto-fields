# Auto Fields for Unity

Eliminates manual Inspector wiring. Mark a serialized field with `[Auto]` and the Editor automatically resolves and assigns it — no drag-and-drop required.

## Features

- **Component & GameObject fields** — resolved via `GetComponentInChildren` on the owning MonoBehaviour
- **ScriptableObject fields** — resolved via project-wide asset search
- **Array support** — collects all matching instances into an array
- **Path hints** — scope resolution to a child transform path or a project subfolder
- **Always up to date** — re-runs on hierarchy changes, scene/prefab transitions, and assembly reloads
- **Play Mode validation** — pauses the Editor and logs errors if any `[Auto]` field is still null
- **Inspector feedback** — unassigned fields show an error icon immediately, without entering Play Mode
- **Context menu** — right-click any MonoBehaviour → *Refresh Auto-Assigned Fields* to force re-evaluation

## Usage

### Component on self or children

Finds the first matching component on the MonoBehaviour's GameObject or any of its children:

```csharp
public class Player : MonoBehaviour
{
    [Auto] Rigidbody _rb;
    [Auto] Animator _animator;
}
```

### Component on a specific child

Provide a child transform path relative to the owning MonoBehaviour:

```csharp
public class Weapon : MonoBehaviour
{
    [Auto("Visuals/Mesh")] MeshRenderer _mesh;
    [Auto("FX/Muzzle")] ParticleSystem _muzzleFlash;
}
```

### Array of components

Collects **all** matching components from the MonoBehaviour and its children:

```csharp
public class Ragdoll : MonoBehaviour
{
    [Auto] Collider[] _colliders;
    [Auto] Rigidbody[] _bodies;
}
```

### Single ScriptableObject

Searches the entire project for a ScriptableObject of that type. Logs a warning if multiple are found and uses the first:

```csharp
public class GameManager : MonoBehaviour
{
    [Auto] GameConfig _config;
}
```

### ScriptableObject scoped to a subfolder

Scope the search to a folder under `Assets/ScriptableObjects/`:

```csharp
public class EnemySpawner : MonoBehaviour
{
    [Auto("Enemies")] EnemyData _enemyData; // searches Assets/ScriptableObjects/Enemies/
}
```

### Array of ScriptableObjects

Collects **all** assets of that type found in the project (or subfolder):

```csharp
public class LevelManager : MonoBehaviour
{
    [Auto] LevelData[] _allLevels;
    [Auto("Enemies")] EnemyData[] _enemies; // all assets under Assets/ScriptableObjects/Enemies/
}
```

## Play Mode validation

On entering Play Mode, the Editor checks every `[Auto]` field across all MonoBehaviours in the scene. If any field is still `null`, it:

1. Logs an error message identifying the component and field name
2. Pauses Play Mode immediately

Fix the reported fields (usually a missing asset or a wrong path hint) and press Play again.

## Notes

- `[Auto]` works on both `public` and `private` / `[SerializeField]` fields
- Already-assigned fields are **not overwritten** — clear manually via the context menu to force reassignment
- The assigner runs in the **Editor only** and has no runtime overhead
