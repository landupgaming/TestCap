using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 50;
    public int currentHealth;

    public System.Action OnDeath;

    void Awake() => currentHealth = maxHealth;

    public void TakeDamage(int dmg)
    {
        if (currentHealth <= 0) return;
        currentHealth -= Mathf.Max(0, dmg);
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
}
