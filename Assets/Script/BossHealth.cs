using UnityEngine;
using UnityEngine.UI;

public class BossHealth : MonoBehaviour
{
    public int maxHealth = 300;
    public int currentHealth;
    public Slider bossBar; // assign at fight start

    public System.Action OnBossDied;

    void Awake() => currentHealth = maxHealth;

    public void TakeDamage(int dmg)
    {
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, dmg));
        if (bossBar) bossBar.value = (float)currentHealth / maxHealth;
        if (currentHealth <= 0)
        {
            OnBossDied?.Invoke();
            Destroy(gameObject);
        }
    }
}
