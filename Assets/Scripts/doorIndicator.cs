using UnityEngine;
using UnityEngine.UI;

public class doorIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform _indicator;
    [SerializeField] private Image _arrow;
    [SerializeField] private Text _distanceLabel;
    [SerializeField] private Transform _doorTarget;
    [SerializeField] private float _edgeInset = 50f;

    private bool _active;

    private void OnEnable()
    {
        questManager.OnAllQuestItemsCollected += Activate;
    }

    private void OnDisable()
    {
        questManager.OnAllQuestItemsCollected -= Activate;
    }

    private void Activate()
    {
        _active = true;
    }

    private void LateUpdate()
    {
        if (!_active || _indicator == null || _doorTarget == null || Camera.main == null ||
            worldState.instance == null || worldState.instance.player == null)
        {
            if (_indicator != null)
                _indicator.gameObject.SetActive(false);
            return;
        }

        Vector3 vp = Camera.main.WorldToViewportPoint(_doorTarget.position);

        bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        if (onScreen)
        {
            _indicator.gameObject.SetActive(false);
            return;
        }

        if (vp.z < 0f)
        {
            vp.x = 1f - vp.x;
            vp.y = 1f - vp.y;
        }

        Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        float m = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y));
        if (m > 0.0001f)
            c *= (0.5f / m);

        Vector2 screenPos = new Vector2((c.x + 0.5f) * Screen.width, (c.y + 0.5f) * Screen.height);
        screenPos.x = Mathf.Clamp(screenPos.x, _edgeInset, Screen.width - _edgeInset);
        screenPos.y = Mathf.Clamp(screenPos.y, _edgeInset, Screen.height - _edgeInset);

        _indicator.gameObject.SetActive(true);
        _indicator.position = screenPos;

        float ang = Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg;
        if (_arrow != null)
            _arrow.rectTransform.rotation = Quaternion.Euler(0f, 0f, ang - 90f);

        if (_distanceLabel != null)
            _distanceLabel.text = Mathf.RoundToInt(
                Vector2.Distance(worldState.instance.player.position, _doorTarget.position)).ToString();
    }
}
