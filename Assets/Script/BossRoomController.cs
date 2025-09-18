using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class BossRoomController : MonoBehaviour
{
    public Room bossRoom;                 // assign (or auto-find in Awake)
    public GameObject doorBlockerPrefab;  // your wall plug/door block
    public GameObject bossPrefab;         // Zeus or other
    public Transform bossSpawnPoint;      // center if null
    public Slider bossHealthBar;          // UI slider (inactive until fight)
    public bool unsealOnDeath = true;

    bool started = false;
    GameObject bossInstance;

    void Awake()
    {
        if (!bossRoom) bossRoom = GetComponentInParent<Room>();
        GetComponent<Collider>().isTrigger = true;
        if (bossHealthBar) bossHealthBar.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (started || !other.CompareTag("Player")) return;
        started = true;

        // Seal all doors of this room
        foreach (var d in bossRoom.Doorways)
            Instantiate(doorBlockerPrefab, d.transform.position, d.transform.rotation, bossRoom.transform);

        // Spawn boss
        Vector3 pos = bossSpawnPoint ? bossSpawnPoint.position : bossRoom.transform.position;
        bossInstance = Instantiate(bossPrefab, pos, Quaternion.identity, bossRoom.transform);

        // Hook health bar
        var bh = bossInstance.GetComponent<BossHealth>();
        if (bossHealthBar && bh)
        {
            bossHealthBar.gameObject.SetActive(true);
            bossHealthBar.maxValue = 1f;
            bossHealthBar.value = 1f;
            bh.bossBar = bossHealthBar;
            bh.OnBossDied += OnBossDied;
        }
    }

    void OnBossDied()
    {
        if (bossHealthBar) bossHealthBar.gameObject.SetActive(false);
        if (unsealOnDeath)
        {
            // Destroy all blockers we spawned (they are children of room)
            // (Optionally mark blockers by tag to delete only those)
        }
    }
}
