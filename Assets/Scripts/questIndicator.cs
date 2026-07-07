using UnityEngine;
using UnityEngine.UI;

public class questIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform[] indicators;
    [SerializeField] private Image[] arrowImages;
    [SerializeField] private Image[] iconImages;
    [SerializeField] private Text[] distanceLabels;
    [SerializeField] private float edgeInset = 50f;

    private void LateUpdate()
    {
        if (questManager.instance == null || Camera.main == null ||
            worldState.instance == null || worldState.instance.player == null)
        {
            HideAll();
            return;
        }

        var items = questManager.instance.ActiveItems();

        for (int i = 0; i < indicators.Length; i++)
        {
            if (indicators[i] == null)
                continue;

            if (i >= items.Count || items[i] == null)
            {
                indicators[i].gameObject.SetActive(false);
                continue;
            }

            Vector3 vp = Camera.main.WorldToViewportPoint(items[i].transform.position);

            bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            if (onScreen)
            {
                indicators[i].gameObject.SetActive(false);
                continue;
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
            screenPos.x = Mathf.Clamp(screenPos.x, edgeInset, Screen.width - edgeInset);
            screenPos.y = Mathf.Clamp(screenPos.y, edgeInset, Screen.height - edgeInset);

            indicators[i].gameObject.SetActive(true);
            indicators[i].position = screenPos;

            float ang = Mathf.Atan2(c.y, c.x) * Mathf.Rad2Deg;
            if (arrowImages[i] != null)
                arrowImages[i].rectTransform.rotation = Quaternion.Euler(0f, 0f, ang - 90f);

            if (iconImages[i] != null)
                iconImages[i].sprite = items[i].icon;

            if (distanceLabels[i] != null)
                distanceLabels[i].text = Mathf.RoundToInt(
                    Vector2.Distance(worldState.instance.player.position, items[i].transform.position)).ToString();
        }
    }

    private void HideAll()
    {
        if (indicators == null)
            return;

        for (int i = 0; i < indicators.Length; i++)
        {
            if (indicators[i] != null)
                indicators[i].gameObject.SetActive(false);
        }
    }
}
