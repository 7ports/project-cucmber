using UnityEngine;

/// <summary>
/// Passive damage aura item weapon. Lives on the player. While the player owns
/// ItemId.Aura, deals constant radial DPS to every enemy inside worldState.AuraRadius()
/// on a fixed tick (worldState.auraTickInterval). Effective DPS scales with attack speed
/// (FireRate relative to fireRateBase). Fractional damage is accumulated between ticks and
/// applied as whole hits via Mathf.FloorToInt (per-enemy full share). Damage routes through
/// worldState.RollDamage so crits apply uniformly, mirroring explosionUtil. A placeholder ring
/// SpriteRenderer child is scaled to the aura diameter and shown while active.
/// </summary>
public class auraWeapon : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _auraRing;   // placeholder ring child, wired in Editor

    private bool _active;
    private float _tickTimer;
    private float _accum;   // fractional damage carried across ticks

    private void Start()
    {
        if (playerInventory.instance != null)
        {
            playerInventory.instance.OnItemAdded += HandleItemAdded;
            if (playerInventory.instance.Has(ItemId.Aura))
                Activate();
        }
        ApplyRingVisual();
    }

    private void OnDestroy()
    {
        if (playerInventory.instance != null)
            playerInventory.instance.OnItemAdded -= HandleItemAdded;
    }

    private void HandleItemAdded(string itemId)
    {
        if (itemId == ItemId.Aura)
            Activate();
    }

    private void Activate()
    {
        if (_active) return;
        _active = true;
        _tickTimer = 0f;
        _accum = 0f;
        ApplyRingVisual();
    }

    private void Update()
    {
        if (!_active || worldState.instance == null) return;

        float tickInterval = Mathf.Max(0.01f, worldState.instance.auraTickInterval);
        _tickTimer += Time.deltaTime;
        while (_tickTimer >= tickInterval)
        {
            _tickTimer -= tickInterval;
            Tick(tickInterval);
        }

        ApplyRingVisual();
    }

    private void Tick(float tickInterval)
    {
        worldState ws = worldState.instance;

        // Effective DPS scaled by attack speed relative to the base fire rate.
        float fireScale = ws.fireRateBase > 0f ? ws.FireRate() / ws.fireRateBase : 1f;
        float effectiveDps = ws.AuraDps() * fireScale;

        _accum += effectiveDps * tickInterval;
        int perTickDamage = Mathf.FloorToInt(_accum);
        if (perTickDamage < 1) return;
        _accum -= perTickDamage;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ws.AuraRadius());
        foreach (Collider2D c in hits)
        {
            if (c == null || !c.CompareTag("Enemy")) continue;
            enemyHealth eh = c.GetComponent<enemyHealth>();
            if (eh == null) continue;
            int dmg = ws.RollDamage(perTickDamage, out bool crit);
            eh.takeDamage(dmg, crit);
        }
    }

    private void ApplyRingVisual()
    {
        if (_auraRing == null) return;
        _auraRing.enabled = _active;
        if (_active && worldState.instance != null)
        {
            // World-space diameter the ring must visually span.
            float diameter = 2f * worldState.instance.AuraRadius();

            // Account for the ring sprite's own base size: a sprite whose bounds
            // are not exactly 1 world-unit across at scale 1 would otherwise draw
            // the wrong radius. Divide the target diameter by the sprite's
            // unscaled size so the drawn ring's WORLD radius always equals the
            // gameplay AuraRadius() used for the damage OverlapCircle above.
            float spriteBase = 1f;
            if (_auraRing.sprite != null)
            {
                Vector2 baseSize = _auraRing.sprite.bounds.size;
                spriteBase = Mathf.Max(baseSize.x, baseSize.y);
            }
            if (spriteBase <= 0f) spriteBase = 1f;

            float scale = diameter / spriteBase;
            _auraRing.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
