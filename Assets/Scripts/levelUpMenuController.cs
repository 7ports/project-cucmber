using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class levelUpMenuController : MonoBehaviour
{
    private const int OfferCount = 5;

    [SerializeField] private Button[] buttons; // 3
    [SerializeField] private Text[] labels;    // 3, one per button

    private readonly Upgrade[] rolled = new Upgrade[OfferCount];

    private void OnEnable()
    {
        if (buttons == null || buttons.Length < OfferCount) return;
        if (labels == null || labels.Length < OfferCount) return;

        List<Upgrade> pool = upgradePool.BuildPool();

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Upgrade tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        for (int i = 0; i < OfferCount; i++)
        {
            rolled[i] = pool[i];
            labels[i].text = upgradePool.LabelFor(rolled[i]);

            int idx = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => Choose(rolled[idx]));
        }
    }

    private void Choose(Upgrade u)
    {
        upgradePool.ApplyUpgrade(u);

        if (levelUpManager.instance != null)
        {
            levelUpManager.instance.ApplyChoiceAndAdvance();
        }
    }
}
