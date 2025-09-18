using UnityEngine;

/// Rotate a single straight wall (thin box) around 'pivot' for 'duration'.
[RequireComponent(typeof(BoxCollider))]
public class LightningWall : MonoBehaviour
{
    public Transform pivot;
    public float angularSpeed = 80f;     // degrees/sec
    public float duration = 5f;
    public int damage = 15;

    float timer;

    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    void OnEnable()
    {
        timer = duration;
    }

    void Update()
    {
        if (!pivot) return;

        transform.position = pivot.position;
        transform.Rotate(Vector3.up, angularSpeed * Time.deltaTime, Space.World);

        timer -= Time.deltaTime;
        if (timer <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph) ph.TakeDamage(damage);
    }
}
