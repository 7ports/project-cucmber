using UnityEngine;

public class enemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    private int currentHp;
    [SerializeField] private GameObject xpPrefab;
    [SerializeField] private int xpDropCount = 1;

    void OnEnable()
    {
        currentHp = maxHp;
    }

    public void takeDamage(int amount)
    {
        currentHp -= amount;
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

        if (objectPool.instance != null)
            objectPool.instance.ret(gameObject);
    }
}
