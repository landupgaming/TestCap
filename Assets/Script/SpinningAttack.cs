using UnityEngine;

public class SpinningAttack : MonoBehaviour
{
    public float rotationSpeed = 90f;
    public float duration = 3f;
    public int damageAmount = 10;

    float t;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        t += Time.deltaTime;
        if (t >= duration) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            other.GetComponent<PlayerHealth>()?.TakeDamage(damageAmount);
    }
}
