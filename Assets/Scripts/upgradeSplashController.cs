using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows a yellow floating splash of each selected upgrade's name over the player's
// head, reusing the pooled floatingText animation used by the "LEVEL UP!" text.
// When several upgrades are granted at once (e.g. the slot-machine menu applies
// multiple picks), the splashes play SEQUENTIALLY rather than stacking on top of
// one another. Splashes run after the menu closes / game unpauses, so normal
// scaled WaitForSeconds is correct here.
public class upgradeSplashController : MonoBehaviour
{
    public static upgradeSplashController instance;

    [SerializeField] private GameObject splashPrefab;
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float splashInterval = 0.45f;

    private readonly Queue<string> _pending = new Queue<string>();
    private bool _draining;
    private bool _held;

    private void Awake()
    {
        instance = this;
    }

    // HOLD/RELEASE: while held, splashes still Enqueue but do NOT start animating.
    // The slot-machine menu holds before its upgrades apply so the splashes queue
    // silently, then releases once the menu is fully closed. Non-slot callers never
    // hold, so their splashes fire immediately as before.
    public void Hold()
    {
        _held = true;
    }

    // Release the hold and flush anything queued while held, kicking off the drain
    // if it isn't already running.
    public void Release()
    {
        _held = false;
        if (!_draining && _pending.Count > 0)
            StartCoroutine(Drain());
    }

    // Queue an upgrade label to splash over the player. Kicks off the drain
    // coroutine only if one isn't already running, so multiple enqueues in the
    // same frame reveal one after another instead of all at once. While a hold is
    // engaged, the label queues but the drain waits for Release().
    public void Enqueue(string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        _pending.Enqueue(label);
        if (!_draining && !_held)
            StartCoroutine(Drain());
    }

    private IEnumerator Drain()
    {
        _draining = true;
        while (_pending.Count > 0)
        {
            string label = _pending.Dequeue();
            Spawn(label);
            yield return new WaitForSeconds(splashInterval);
        }
        _draining = false;
    }

    private void Spawn(string label)
    {
        if (splashPrefab == null || objectPool.instance == null ||
            worldState.instance == null || worldState.instance.player == null)
            return;

        GameObject go = objectPool.instance.get(
            splashPrefab,
            worldState.instance.player.position + textOffset,
            Quaternion.identity);
        if (go == null) return;

        Text text = go.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label;
            text.color = Color.yellow;
        }
    }
}
