using UnityEngine;

public class enemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    private int currentHp;
    [SerializeField] private GameObject xpPrefab;
    [SerializeField] private int xpDropCount = 1;
    [SerializeField] private int enemyDamage = 5;
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 dmgTextOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private GameObject deathDropPrefab;   // optional; only the boss sets this

    private damageFlash flash;

    public int EnemyDamage => enemyDamage;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    void Awake()
    {
        flash = GetComponent<damageFlash>();
    }

    void OnEnable()
    {
        currentHp = maxHp;
    }

    public void takeDamage(int amount)
    {
        currentHp -= amount;
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
