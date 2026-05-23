using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Attach to each Button GameObject in the Main Menu.
// Gives a smooth scale pop on hover and plays sounds via MainMenuAudioManager.
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Scale Settings")]
    [Tooltip("How much the button scales up on hover. 1.08 = 8% bigger.")]
    [SerializeField] float hoverScale = 1.08f;
    [Tooltip("How long the scale transition takes in seconds.")]
    [SerializeField] float animDuration = 0.1f;

    [Header("Sound Settings")]
    [Tooltip("Uncheck for buttons that have no functionality yet (optional).")]
    [SerializeField] bool playClickSound = true;

    Vector3 originalScale;
    Coroutine scaleRoutine;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    // Called when cursor enters the button
    public void OnPointerEnter(PointerEventData eventData)
    {
        MainMenuAudioManager.Instance?.PlayHover();
        ScaleTo(originalScale * hoverScale);
    }

    // Called when cursor leaves the button
    public void OnPointerExit(PointerEventData eventData)
    {
        ScaleTo(originalScale);
    }

    // Called on mouse button down on the button
    public void OnPointerClick(PointerEventData eventData)
    {
        if (playClickSound)
            MainMenuAudioManager.Instance?.PlayClick();
    }

    void ScaleTo(Vector3 target)
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(target));
    }

    IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            // SmoothStep gives a nice ease-in-out feel
            transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.localScale = target;
        scaleRoutine = null;
    }

    // Safety: reset scale if something disables this component mid-animation
    void OnDisable()
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        transform.localScale = originalScale;
    }
}
