using System.Collections;
using UnityEngine;

// Shared visuals for Lab 5 states: the flicker used by the invincible states.
// Reference-counted so the actor FSM and the buff FSM can both request it without
// one turning the other's flicker off.
public class SpriteFlicker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;

    private float interval = 0.12f;
    private int requests;
    private Coroutine routine;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public bool IsFlickering => requests > 0;

    public void Request(float blinkInterval = 0.12f)
    {
        interval = Mathf.Max(0.02f, blinkInterval);
        requests++;
        if (routine == null) routine = StartCoroutine(Blink());
    }

    public void Release()
    {
        requests = Mathf.Max(0, requests - 1);
        if (requests == 0) StopFlickering();
    }

    public void StopFlickering()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        requests = 0;
        SetVisible(true);
    }

    IEnumerator Blink()
    {
        bool visible = true;
        while (requests > 0)
        {
            visible = !visible;
            SetVisible(visible);
            yield return new WaitForSeconds(interval);
        }
        SetVisible(true);
        routine = null;
    }

    void SetVisible(bool visible)
    {
        if (renderers == null) return;
        foreach (SpriteRenderer renderer in renderers)
            if (renderer != null) renderer.enabled = visible;
    }
}
