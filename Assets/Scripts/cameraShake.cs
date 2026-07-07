using UnityEngine;

public class cameraShake : MonoBehaviour
{
    public static cameraShake instance;
    [SerializeField] private float defaultMagnitude = 0.25f;
    [SerializeField] private float defaultDuration = 0.2f;
    private float magnitude, duration, elapsed;
    private Vector3 prevOffset;

    void Awake() { instance = this; }

    public void Shake() { Shake(defaultMagnitude, defaultDuration); }
    public void Shake(float mag, float dur) { magnitude = mag; duration = dur; elapsed = 0f; }

    void LateUpdate()
    {
        // remove last frame's offset first (follow-cam re-sets position, but be safe)
        transform.position -= prevOffset;
        prevOffset = Vector3.zero;
        if (elapsed < duration && duration > 0f)
        {
            elapsed += Time.deltaTime;
            float curMag = magnitude * (1f - elapsed / duration);
            Vector2 o = Random.insideUnitCircle * curMag;
            prevOffset = new Vector3(o.x, o.y, 0f);
            transform.position += prevOffset;
        }
    }
}
