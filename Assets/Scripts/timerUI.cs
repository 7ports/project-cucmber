using TMPro;
using UnityEngine;

public class timerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       timerText.text = "00:00:00"; 
    }

    // Update is called once per frame
    void Update()
    {
        float t = Time.timeSinceLevelLoad;

        int minutes    = Mathf.FloorToInt(t / 60f);        // whole minutes
        int seconds    = Mathf.FloorToInt(t) % 60;         // 0–59
        int hundredths = Mathf.FloorToInt(t * 100f) % 100; // centiseconds, 0–99

        timerText.text = $"{minutes:00}:{seconds:00}:{hundredths:00}";
    }
}
