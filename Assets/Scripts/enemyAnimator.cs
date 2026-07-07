using UnityEngine;

public class enemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private float animThreshold = 0.1f;
    private Vector3 lastPos;
    private Vector2 lastDir = Vector2.down;
    private float oldX, oldY;

    void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        lastPos = transform.position;
        oldX = oldY = float.NaN;
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        Vector2 dir = delta.sqrMagnitude > 0.0000001f ? ((Vector2)delta).normalized : lastDir;
        lastDir = dir;

        if (anim == null) return;

        if (float.IsNaN(oldX) || Mathf.Abs(dir.x - oldX) > animThreshold)
        {
            anim.SetFloat("x", dir.x);
            oldX = dir.x;
        }
        if (float.IsNaN(oldY) || Mathf.Abs(dir.y - oldY) > animThreshold)
        {
            anim.SetFloat("y", dir.y);
            oldY = dir.y;
        }
    }
}
