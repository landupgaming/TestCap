// NavMeshBakerAtRuntime.cs
using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;

public class NavMeshBakerAtRuntime : MonoBehaviour
{
    [ContextMenu("Bake All Surfaces Now")]
    public void BakeNow()
    {
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in surfaces) s.BuildNavMesh();
        Debug.Log($"[NavMeshBaker] Built {surfaces.Length} surfaces.");
    }

    IEnumerator Start()
    {
        // Wait a couple frames so your DungeonGenerator/room spawners finish.
        yield return null;
        yield return null;
        BakeNow();
    }
}
