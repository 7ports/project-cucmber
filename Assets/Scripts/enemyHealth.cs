using UnityEngine;

public class enemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 30;
    private int currentHp;
    private int scaledMaxHp;   // ADDED: base maxHp * time multiplier, recomputed each (re)spawn
    private int scaledEnemyDamage;   // ADDED: enemyDamage * boss time multiplier, recomputed each (re)spawn
    [SerializeField] private GameObject[] xpPrefabsByValue;   // [0]=XP1(v1) .. [3]=XP4(v4); assign in Inspector
    [SerializeField] private int baseDropValue = 1;           // per-drop worth before the XpGain bonus
    [SerializeField] private int xpDropCount = 1;
    [SerializeField] private int enemyDamage = 50;
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Vector3 dmgTextOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private GameObject deathDropPrefab;   // optional; only the boss sets this

    [SerializeField] private ParticleSystem _burnFx;    // placeholder burn VFX child; on while _burnStacks>0
    [SerializeField] private ParticleSystem _frostFx;   // placeholder freeze VFX child; on while IsFrozen

    private damageFlash flash;
    private bool _isBoss;   // ADDED: true only for the boss (has bossBehaviour); gates blood-on-hit

    // --- Status effect state (Fire / Freeze). Reset every OnEnable (pool respawn). ---
    private int   _burnStacks;          // 0..fireStackCap
    private float _burnTimeRemaining;   // seconds left before burn fully expires
    private float _burnTickAccum;       // accumulates toward one FireTickInterval
    private float _freezeTimeRemaining; // seconds left immobilized

    public bool IsFrozen => _freezeTimeRemaining > 0f;   // read by the three movers

    public int EnemyDamage => scaledEnemyDamage;   // CHANGED: bosses scale damage over time
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
        // Bosses additionally scale HP AND damage with elapsed run time so they get stronger, not just tankier.
        float bossMult = (_isBoss && worldState.instance != null) ? worldState.instance.BossStatTimeMultiplier() : 1f;
        scaledMaxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * mult * bossMult));   // never mutate serialized maxHp
        currentHp = scaledMaxHp;
        scaledEnemyDamage = Mathf.Max(1, Mathf.RoundToInt(enemyDamage * bossMult));   // never mutate serialized enemyDamage

        // ADDED: clear all status so a recycled enemy never starts burning/frozen.
        _burnStacks = 0;
        _burnTimeRemaining = 0f;
        _burnTickAccum = 0f;
        _freezeTimeRemaining = 0f;

        // ADDED: reset placeholder status VFX so a recycled enemy shows none.
        SetStatusFx(_burnFx, false);
        SetStatusFx(_frostFx, false);
    }

    void Update()
    {
        // Read config once; fall back to constants if worldState not ready.
        float tickInterval = (worldState.instance != null) ? worldState.instance.fireTickInterval : 1f;
        int   dpsPerStack  = (worldState.instance != null) ? Mathf.RoundToInt(worldState.instance.fireDpsPerStack()) : 10;

        // --- Burning DoT ---
        if (_burnStacks > 0 && _burnTimeRemaining > 0f)
        {
            _burnTimeRemaining -= Time.deltaTime;
            _burnTickAccum     += Time.deltaTime;
            while (_burnTickAccum >= tickInterval)
            {
                _burnTickAccum -= tickInterval;
                takeDamage(dpsPerStack * _burnStacks);   // 10 dmg/sec PER STACK
                if (currentHp <= 0) return;              // die() already recycled us
            }
            if (_burnTimeRemaining <= 0f)                // burn expired
            {
                _burnStacks = 0;
                _burnTickAccum = 0f;
                SetStatusFx(_burnFx, false);
            }
        }

        // --- Freeze countdown ---
        if (_freezeTimeRemaining > 0f)
        {
            _freezeTimeRemaining -= Time.deltaTime;
            if (_freezeTimeRemaining <= 0f)
                SetStatusFx(_frostFx, false);
        }
    }

    /// <summary>
    /// CONTRACT (called by the projectile-hit site when the player owns ItemId.Fire).
    /// Adds one burning stack (capped) and refreshes burn duration.
    /// </summary>
    public void ApplyFire()
    {
        int cap = (worldState.instance != null) ? worldState.instance.fireStackCap : 3;
        _burnStacks = Mathf.Min(_burnStacks + 1, cap);
        _burnTimeRemaining = (worldState.instance != null) ? worldState.instance.fireBurnDuration : 3f;
        SetStatusFx(_burnFx, true);
    }

    /// <summary>
    /// CONTRACT (called by the projectile-hit site AFTER its freeze-chance roll succeeds
    /// and the player owns ItemId.Freeze). Immobilizes the enemy for `seconds`.
    /// Passing seconds &lt;= 0 uses the configured default duration.
    /// </summary>
    public void ApplyFreeze(float seconds)
    {
        if (seconds <= 0f)
            seconds = (worldState.instance != null) ? worldState.instance.freezeDefaultDuration : 2f;
        // Refresh-to-longest: a new freeze never shortens an active one.
        _freezeTimeRemaining = Mathf.Max(_freezeTimeRemaining, seconds);
        SetStatusFx(_frostFx, true);
    }

    // Toggle a placeholder status particle system on/off, guarding for an unwired reference.
    private void SetStatusFx(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        if (on)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else if (ps.isPlaying)
        {
            ps.Stop();
            ps.Clear();
        }
    }

    public void takeDamage(int amount, bool isCrit = false)
    {
        currentHp -= amount;
        runStats.TotalDamage += amount;   // run-scoped telemetry: all damage flows through here
        if (_isBoss && bloodPrefab != null && objectPool.instance != null)   // ADDED: boss-only blood on each hit
            objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);
        if (flash != null) flash.Flash();
        if (objectPool.instance != null && damageNumberPrefab != null)
        {
            GameObject dn = objectPool.instance.get(damageNumberPrefab, transform.position + dmgTextOffset, Quaternion.identity);
            damageNumber num = dn.GetComponent<damageNumber>();
            if (num != null) num.Set(amount, isCrit);
        }
        if (currentHp <= 0)
            die();
    }

    // Selects the XP prefab whose baked value matches the effective per-drop worth
    // (baseDropValue + XpGain bonus). Higher upgrade level -> bigger prefab (XP4),
    // so the drop visually reflects its worth. Clamped so a raised cap can't overflow.
    GameObject PickXpPrefab()
    {
        if (xpPrefabsByValue == null || xpPrefabsByValue.Length == 0) return null;
        int bonus = (worldState.instance != null) ? worldState.instance.XpBonus() : 0;
        int value = baseDropValue + bonus;                       // 1..4 under current cap
        int idx   = Mathf.Clamp(value - 1, 0, xpPrefabsByValue.Length - 1);
        return xpPrefabsByValue[idx];
    }

    void die()
    {
        runStats.EnemiesKilled++;   // run-scoped telemetry: single death path
        GameObject xp = PickXpPrefab();
        for (int i = 0; i < xpDropCount; i++)
        {
            if (objectPool.instance != null && xp != null)
                objectPool.instance.get(xp, transform.position, Quaternion.identity);
        }

        if (bloodPrefab != null && objectPool.instance != null)
            objectPool.instance.get(bloodPrefab, transform.position, Quaternion.identity);

        if (deathDropPrefab != null && objectPool.instance != null)
            objectPool.instance.get(deathDropPrefab, transform.position, Quaternion.identity);

        if (objectPool.instance != null)
            objectPool.instance.ret(gameObject);
    }
}
