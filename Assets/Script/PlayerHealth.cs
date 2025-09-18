using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    [Tooltip("Shown for debugging; initialized at Start.")]
    public int currentHealth;

    [Header("Hit Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking any hit.")]
    public float hitIFrameDuration = 0.35f;
    [Tooltip("Optional flash during i-frames.")]
    public Image screenFlash;
    public float flashAlpha = 0.22f;
    public float flashFadeTime = 0.25f;

    [Header("UI (optional)")]
    public Slider healthBar;
    public Image healthFill;
    public Gradient healthGradient;

    [Header("Death")]
    public GameObject deathScreen;
    public bool pauseOnDeath = true;

    // state
    bool isDead = false;
    float invulnerableUntil = -999f;

    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth > 0 ? currentHealth : maxHealth, 0, maxHealth);
        UpdateUI();
        if (screenFlash) screenFlash.color = new Color(screenFlash.color.r, screenFlash.color.g, screenFlash.color.b, 0f);
        if (deathScreen) deathScreen.SetActive(false);
    }

    public bool IsInvulnerable() => Time.time < invulnerableUntil;

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        UpdateUI();
    }

    public void Kill()
    {
        if (isDead) return;
        currentHealth = 0;
        Die();
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        // Block damage during i-frames
        if (IsInvulnerable()) return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
            return;
        }

        // Start post-hit i-frames
        invulnerableUntil = Time.time + Mathf.Max(0f, hitIFrameDuration);

        // Optional feedback
        if (screenFlash) StartCoroutine(Flash(screenFlash, flashAlpha, flashFadeTime));

        UpdateUI();
    }

    void Die()
    {
        isDead = true;
        UpdateUI();

        if (deathScreen) deathScreen.SetActive(true);
        if (pauseOnDeath) Time.timeScale = 0f;

        // optional: reload after delay if no death screen provided
        if (!deathScreen)
            StartCoroutine(ReloadAfter(1.25f));
    }

    IEnumerator ReloadAfter(float s)
    {
        yield return new WaitForSecondsRealtime(s);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void UpdateUI()
    {
        if (healthBar)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
            if (healthFill && healthGradient != null)
            {
                float t = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
                healthFill.color = healthGradient.Evaluate(t);
            }
        }
    }

    IEnumerator Flash(Image img, float alpha, float fadeTime)
    {
        if (!img) yield break;

        // up
        var c = img.color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / (fadeTime * 0.5f);
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, alpha, t));
            yield return null;
        }

        // down
        float startA = img.color.a;
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / (fadeTime * 0.5f);
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startA, 0f, t));
            yield return null;
        }

        img.color = new Color(c.r, c.g, c.b, 0f);
    }
}
