using System.Collections.Generic;
using UnityEngine;

public class playerHealth : MonoBehaviour
{
    [SerializeField] private float damageInterval = 2f;
    private float tickTimer;
    private float regenAccumulator;
    private readonly HashSet<Collider2D> touchingEnemies = new HashSet<Collider2D>();
    private damageFlash flash;

    void Awake()
    {
        flash = GetComponent<damageFlash>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            bool wasEmpty = touchingEnemies.Count == 0;
            touchingEnemies.Add(other);
            if (wasEmpty)
            {
                enemyHealth eh = other.GetComponent<enemyHealth>();
                if (eh != null) ApplyDamage(eh.EnemyDamage);
                tickTimer = 0f;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        touchingEnemies.Remove(other);
    }

    void Update()
    {
        touchingEnemies.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

        if (touchingEnemies.Count == 0)
        {
            tickTimer = 0f;
        }
        else
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= damageInterval) { tickTimer -= damageInterval; ApplyContactDamage(); }
        }

        if (worldState.instance != null && worldState.instance.currentHP > 0)
        {
            regenAccumulator += worldState.instance.Regen() * Time.deltaTime;
            if (regenAccumulator >= 1f)
            {
                int whole = Mathf.FloorToInt(regenAccumulator);
                regenAccumulator -= whole;
                worldState.instance.currentHP = Mathf.Min(worldState.instance.MaxHP(), worldState.instance.currentHP + whole);
            }
        }
    }

    int Reduce(int raw) => Mathf.Max(1, raw - Mathf.RoundToInt(worldState.instance.Defense()));

    void ApplyDamage(int raw)
    {
        if (worldState.instance == null) return;
        int applied = Reduce(raw);
        worldState.instance.currentHP -= applied;
        if (flash != null) flash.Flash();
        if (cameraShake.instance != null) cameraShake.instance.Shake();
        if (screenFlash.instance != null) screenFlash.instance.Flash();
        if (worldState.instance.currentHP <= 0)
        {
            worldState.instance.currentHP = 0;
            if (gameOverManager.instance != null) gameOverManager.instance.Show();
        }
    }

    void ApplyContactDamage()
    {
        if (worldState.instance == null) return;
        int dmg = 0;
        foreach (Collider2D c in touchingEnemies)
        {
            enemyHealth eh = c.GetComponent<enemyHealth>();
            if (eh != null) dmg += eh.EnemyDamage;
        }
        ApplyDamage(dmg);
    }
}
