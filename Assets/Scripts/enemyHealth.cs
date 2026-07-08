using UnityEngine;

public class enemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 30;
    private int currentHp;
    private int scaledMaxHp;   // ADDED: base maxHp * time multiplier, recomputed each (re)spawn
    [SerializeField] private GameObject xpPrefab;
    [SerializeField] private int xpDropCount = 1;
    [SerializeField] private int enemyDamage = 50;
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 dmgTextOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private GameObject deathDropPrefab;   // optional; only the boss sets this

    private damageFlash flash;
    private bool _isBoss;   // ADDED: true only for the boss (has bossBehaviour); gates blood-on-hit

    public int EnemyDamage => enemyDamage;
    public int MaxHp => scaledMaxHp;   // CHANGED: bars read the scaled max so fill proportions stay correct
    public int CurrentHp => currentHp;

    void Awake()
    {
        flash = GetComponent<damageFlash>();
        _isBoss = GetComponent<bossBehaviour>() != null;   // ADDED: boss-only marker, cached once
    }

    void OnEnable()
    {
        float mult = (worldState.instance != null) ? worldState.instance.EnemyHpTimeMultiplier() : 1f;
        scaledMaxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * mult));   // never mutate serialized maxHp
        currentHp = scaledMaxHp;
    }

    public void takeDamage(int amount)
    {
        currentHp -= amount;
        if (_isBoss && bloodPrefab != null && objectPool.instance != null)   // ADDED: boss-only blood on each hit
            objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);
        if (flash != null) flash.Flash();
        if (objectPool.instance != null && damageNumberPrefab != null)
        {
            GameObject dn = objectPool.instance.get(damageNumberPrefab, transform.position + dmgTextOffset, Quaternion.identity);
            damageNumber num = dn.GetComponent<damageNumber>();
            if (num != null) num.Set(amount);
        }
        if (currentHp <= 0)
            die();
    }

    void die()
    {
        for (int i = 0; i < xpDropCount; i++)
        {
            if (objectPool.instance != null && xpPrefab != null)
                objectPool.instance.get(xpPrefab, transform.position, Quaternion.identity);
        }

        if (bloodPrefab != null && objectPool.instance != null)
            objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);

        if (deathDropPrefab != null && objectPool.instance != null)
            objectPool.instance.get(deathDropPrefab, transform.position, Quaternion.identity);

        if (objectPool.instance != null)
            objectPool.instance.ret(gameObject);
    }
}
