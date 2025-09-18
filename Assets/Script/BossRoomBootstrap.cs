using System.Collections;
using UnityEngine;

/// Put this on DungeonSystems. After generation, it finds the room that
/// has a BossRoomMarker, then adds/sets up BossRoomController + BossRoomBoundsWatcher.
public class BossRoomBootstrap : MonoBehaviour
{
    [Header("Boss")]
    public GameObject zeusPrefab;

    [Header("Start Condition (bounds-based)")]
    public BossRoomBoundsWatcher.StartMode startMode = BossRoomBoundsWatcher.StartMode.ImmediateOnEnter;
    public float seconds = 0.75f;
    public bool cancelDelayIfExit = true;
    public float innerMargin = 0.35f;
    public bool checkY = false;

    [Header("Timing")]
    public float findTimeout = 5f;    // wait up to this long for the marker

    void Start() => StartCoroutine(Setup());

    IEnumerator Setup()
    {
        // 1) Wait for a BossRoomMarker (works even if it's inactive)
        Transform markerT = null;
        float t = 0f;
        while (!markerT)
        {
            var marker = FindFirstObjectByType<BossRoomMarker>(FindObjectsInactive.Include);
            if (marker) markerT = marker.transform;
            else
            {
                yield return null;
                t += Time.deltaTime;
                if (t > findTimeout)
                {
                    Debug.LogWarning("[BossRoomBootstrap] Timed out waiting for BossRoomMarker.");
                    yield break;
                }
            }
        }

        // 2) Find the room root (object that has your Room component) or use marker parent
        Transform roomRoot = markerT;
        var roomComp = markerT.GetComponentInParent<Room>();
        if (roomComp) roomRoot = roomComp.transform;

        // One frame settle so children like Bounds/SealManager appear
        yield return null;

        // 3) Ensure BossRoomController on the room
        var ctrl = roomRoot.GetComponent<BossRoomController>();
        if (!ctrl) ctrl = roomRoot.gameObject.AddComponent<BossRoomController>();
        ctrl.roomRoot = roomRoot;
        if (!ctrl.zeusPrefab && zeusPrefab) ctrl.zeusPrefab = zeusPrefab;

        // 4) Ensure Bounds watcher on the room
        var watcher = roomRoot.GetComponent<BossRoomBoundsWatcher>();
        if (!watcher) watcher = roomRoot.gameObject.AddComponent<BossRoomBoundsWatcher>();
        watcher.controller = ctrl;
        watcher.boundsChild = BossRoomController.FindBoundsChild(roomRoot);
        watcher.mode = startMode;
        watcher.seconds = seconds;
        watcher.cancelDelayIfExit = cancelDelayIfExit;
        watcher.innerMargin = innerMargin;
        watcher.checkY = checkY;

        Debug.Log($"[BossRoomBootstrap] Wired boss room '{roomRoot.name}'.");
    }
}
