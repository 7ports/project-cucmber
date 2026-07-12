using UnityEngine;

// Reusable pooled one-shot particle burst. Mirrors bloodBurst: on enable it (re)plays the
// attached ParticleSystem, then returns itself to objectPool once the system is no longer
// alive. Placeholder VFX used by the explosion / trail systems; tuned later.
public class pooledParticleBurst : MonoBehaviour
{
    private ParticleSystem _ps;
    private float _fallbackLife;   // main.duration + startLifetime, used if IsAlive never clears
    private float _timer;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    void OnEnable()
    {
        _timer = 0f;
        if (_ps != null)
        {
            ParticleSystem.MainModule main = _ps.main;
            _fallbackLife = main.duration + main.startLifetime.constantMax;
            _ps.Clear(true);
            _ps.Play(true);
        }
        else
        {
            _fallbackLife = 1f;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        // Return once the system has finished emitting (with a tiny grace so we don't recycle
        // on the first frame before particles spawn), or after the fallback lifetime elapses.
        bool finished = _ps == null
            || (_timer > 0.05f && !_ps.IsAlive(true))
            || _timer >= _fallbackLife;
        if (finished && objectPool.instance != null)
            objectPool.instance.ret(gameObject);
    }
}
