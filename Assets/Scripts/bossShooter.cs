using UnityEngine;

public class bossShooter : MonoBehaviour
{
    public enum BulletPatternType { Ring, Spiral, AimedSpread, RandomScatter }

    [Header("Pattern")]
    [SerializeField] private BulletPatternType patternType = BulletPatternType.Ring;
    [SerializeField] private GameObject enemyProjectilePrefab;

    [Header("Cadence")]
    [SerializeField] private float fireInterval = 1.2f;

    [Header("Bullet")]
    [SerializeField] private float bulletSpeed = 3.5f;
    [SerializeField] private float bulletLifetime = 6f;

    [Header("Volley shape")]
    [SerializeField] private int bulletsPerVolley = 12;
    [SerializeField] private float spreadAngle = 45f;
    [SerializeField] private float spinStep = 13f;

    [Header("Randomize")]
    [SerializeField] private bool randomizeOnEnable = false;

    private float fireTimer;
    private float spinAngle;

    void OnEnable()
    {
        fireTimer = 0f;
        spinAngle = 0f;
        if (randomizeOnEnable) RandomizePattern();
    }

    void Update()
    {
        if (worldState.instance == null || worldState.instance.player == null) return;
        if (objectPool.instance == null || enemyProjectilePrefab == null) return;
        fireTimer += Time.deltaTime;
        float fireRateMult = (worldState.instance != null) ? worldState.instance.BossFireRateTimeMultiplier() : 1f;
        if (fireTimer >= fireInterval / fireRateMult) { fireTimer = 0f; Fire(); }
    }

    public void RandomizePattern()
    {
        int count = System.Enum.GetValues(typeof(BulletPatternType)).Length;
        patternType = (BulletPatternType)Random.Range(0, count);
        spinAngle = 0f;
    }

    void Fire()
    {
        switch (patternType)
        {
            case BulletPatternType.Ring:          EmitRing();         break;
            case BulletPatternType.Spiral:        EmitSpiral();       break;
            case BulletPatternType.AimedSpread:   EmitAimedSpread();  break;
            case BulletPatternType.RandomScatter: EmitRandomScatter();break;
        }
    }

    void EmitRing()
    {
        int n = Mathf.Max(1, EffectiveVolley());
        float step = 360f / n;
        for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(i * step));
    }

    void EmitSpiral()
    {
        int n = Mathf.Max(1, EffectiveVolley());
        float step = 360f / n;
        for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(spinAngle + i * step));
        spinAngle = Mathf.Repeat(spinAngle + spinStep, 360f);
    }

    void EmitAimedSpread()
    {
        int k = Mathf.Max(1, EffectiveVolley());
        float center = AimAngleDeg();
        if (k == 1) { EmitBullet(Ang2Dir(center)); return; }
        float half = spreadAngle * 0.5f;
        float inc = spreadAngle / (k - 1);
        for (int i = 0; i < k; i++) EmitBullet(Ang2Dir(center - half + i * inc));
    }

    void EmitRandomScatter()
    {
        int n = Mathf.Max(1, EffectiveVolley());
        for (int i = 0; i < n; i++) EmitBullet(Ang2Dir(Random.Range(0f, 360f)));
    }

    // Boss volley size grows over elapsed run time (worldState.BossVolleyBonus()).
    int EffectiveVolley()
    {
        int bonus = (worldState.instance != null) ? worldState.instance.BossVolleyBonus() : 0;
        return Mathf.Max(1, bulletsPerVolley + bonus);
    }

    void EmitBullet(Vector2 dir)
    {
        if (objectPool.instance == null || enemyProjectilePrefab == null) return;
        GameObject go = objectPool.instance.get(enemyProjectilePrefab, transform.position, Quaternion.identity);
        enemyProjectile p = go.GetComponent<enemyProjectile>();
        float speedMult = (worldState.instance != null) ? worldState.instance.BossBulletSpeedTimeMultiplier() : 1f;
        if (p != null) p.Launch(dir, bulletSpeed * speedMult, bulletLifetime);
    }

    float AimAngleDeg()
    {
        Transform pl = worldState.instance != null ? worldState.instance.player : null;
        Vector2 to = pl != null ? (Vector2)(pl.position - transform.position) : Vector2.right;
        if (to.sqrMagnitude < 0.0001f) to = Vector2.right;
        return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
    }

    static Vector2 Ang2Dir(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }
}
