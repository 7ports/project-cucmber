using System.Collections.Generic;
using UnityEngine;

public class playerHealth : MonoBehaviour
{
    [SerializeField] private float damageInterval = 2f;
    private float tickTimer;
    private readonly HashSet<Collider2D> touchingEnemies = new HashSet<Collider2D>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy")) touchingEnemies.Add(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        touchingEnemies.Remove(other);
    }

    void Update()
    {
        touchingEnemies.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
        if (touchingEnemies.Count == 0) { tickTimer = 0f; return; }
        tickTimer += Time.deltaTime;
        if (tickTimer >= damageInterval) { tickTimer -= damageInterval; ApplyContactDamage(); }
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
        worldState.instance.currentHP -= dmg;
        if (worldState.instance.currentHP <= 0)
        {
            worldState.instance.currentHP = 0;
            if (gameOverManager.instance != null) gameOverManager.instance.Show();
        }
    }
}
