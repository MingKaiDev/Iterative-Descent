// PageFlipAnimator.cs
// Animates a notepad-style top-edge page flip using ScaleY.
// Pivot of the animated RectTransform MUST be set to (0.5, 1) -- top-centre.
// Content is swapped at the midpoint (scale = 0) via a callback.
using System;
using System.Collections;
using UnityEngine;

public class PageFlipAnimator : MonoBehaviour
{
    [Header("Flip Settings")]
    [Tooltip("Total duration of one full flip (half-out + half-in).")]
    [SerializeField] private float flipDuration = 0.45f;

    [Tooltip("Ease curve applied to each half of the flip. X=normalised time, Y=scale 0..1.")]
    [SerializeField] private AnimationCurve flipCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.7f);

    [Tooltip("RectTransform of the paper sheet that flips. Pivot must be (0.5, 1).")]
    [SerializeField] private RectTransform pageRect;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Plays the full flip animation.
    /// <paramref name="onSwap"/> is called at the midpoint (while the page is edge-on)
    /// so the caller can swap question content invisibly.
    /// </summary>
    public void Flip(Action onSwap)
    {
        if (_flipping) return;
        StartCoroutine(DoFlip(onSwap));
    }

    public bool IsFlipping => _flipping;

    // ── Private ──────────────────────────────────────────────────────────────

    private bool _flipping;

    private IEnumerator DoFlip(Action onSwap)
    {
        _flipping = true;

        float half = flipDuration * 0.5f;

        // --- Phase 1: scale Y from 1 down to 0 (page lifts away) ------------
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / half);           // 0..1
            float scale = 1f - flipCurve.Evaluate(n);    // 1..0
            SetScaleY(scale);
            yield return null;
        }
        SetScaleY(0f);

        // --- Midpoint: swap content while the page is invisible edge-on ------
        onSwap?.Invoke();

        // --- Phase 2: scale Y from 0 up to 1 (new page lands) ---------------
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / half);
            float scale = flipCurve.Evaluate(n);          // 0..1
            SetScaleY(scale);
            yield return null;
        }
        SetScaleY(0.7f);

        _flipping = false;
    }

    private void SetScaleY(float y)
    {
        if (pageRect == null) return;
        Vector3 s = pageRect.localScale;
        s.y = y;
        pageRect.localScale = s;
    }
}
