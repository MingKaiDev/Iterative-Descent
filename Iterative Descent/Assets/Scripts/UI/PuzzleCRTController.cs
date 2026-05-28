using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the root Puzzle Panel GameObject (the one that holds a CanvasGroup).
/// Produces subtle periodic screen flicker, similar to a dying CRT tube.
/// Uses WaitForSecondsRealtime so it works while Time.timeScale == 0.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PuzzleCRTController : MonoBehaviour
{
    [Header("Flicker Timing")]
    [Tooltip("Average seconds between flicker events")]
    public float flickerInterval = 5f;
    [Tooltip("Random offset applied to each interval (+/- this amount)")]
    public float flickerVariance = 2.5f;

    [Header("Flicker Intensity")]
    [Tooltip("Max alpha dip per flicker pulse (0 = no flicker, 0.4 = very harsh)")]
    [Range(0f, 0.4f)]
    public float flickerDepth = 0.10f;
    [Tooltip("Max number of rapid pulses per flicker event")]
    [Range(1, 5)]
    public int maxPulses = 3;

    private CanvasGroup _group;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        StartCoroutine(FlickerLoop());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_group != null) _group.alpha = 1f;
    }

    IEnumerator FlickerLoop()
    {
        while (true)
        {
            float waitTime = flickerInterval + Random.Range(-flickerVariance, flickerVariance);
            waitTime = Mathf.Max(0.5f, waitTime);
            yield return new WaitForSecondsRealtime(waitTime);
            yield return StartCoroutine(DoFlicker());
        }
    }

    IEnumerator DoFlicker()
    {
        int pulses = Random.Range(1, maxPulses + 1);

        for (int i = 0; i < pulses; i++)
        {
            // Dip
            float dip = Random.Range(flickerDepth * 0.4f, flickerDepth);
            _group.alpha = 1f - dip;
            yield return new WaitForSecondsRealtime(Random.Range(0.02f, 0.07f));

            // Restore
            _group.alpha = 1f;

            // Brief gap between pulses (skip gap after last pulse)
            if (i < pulses - 1)
                yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.10f));
        }

        _group.alpha = 1f;
    }
}
