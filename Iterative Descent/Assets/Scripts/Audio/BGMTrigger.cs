using UnityEngine;

/// <summary>
/// Attach to a trigger collider to change the scene's background music when the
/// player walks in -- e.g. a distinct track for a specific room or story beat.
/// Mirrors EncounterTrigger.cs's trigger-once pattern exactly, but hard-swaps
/// GameAudioManager's single BGM AudioSource instead of activating enemies.
///
/// Fires once per scene load -- re-entry does not re-trigger and does not
/// restart the clip.
///
/// ── Scene setup ──────────────────────────────────────────────────────────
///   1. Create a child GameObject at the point you want the music to change
///      (a room entrance, a corridor, a story beat) with a BoxCollider set to
///      Is Trigger = true.
///   2. Attach this script.
///   3. Assign 'musicClip'. Tick 'loop' ON for a continuous track (most BGM),
///      leave it OFF for a one-shot stinger/cue that plays once and stops.
///   4. Leave playerTag alone unless your player collider uses a different tag.
///
/// NOTE: this hard-swaps the SAME BGM AudioSource that ambient noise and boss
/// music both use (see GameAudioManager.cs) -- whatever was playing before
/// stops the instant this fires. There is no auto-revert back to the previous
/// track; if you need the ambient loop back afterward, trigger that explicitly
/// (GameAudioManager.Instance.StopBossMusic() resets it to the scene's ambient
/// clip, despite the "boss" name -- it just reverts the one shared BGM source)
/// from wherever that should happen.
/// </summary>
public class BGMTrigger : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────

    [Header("Music")]
    [Tooltip("Track to play when the player enters this trigger.")]
    [SerializeField] private AudioClip musicClip;

    [Tooltip("ON: loops continuously (most background music). OFF: plays once and stops.")]
    [SerializeField] private bool loop = true;

    [Range(0f, 1f)]
    [Tooltip("Playback volume -- same convention as GameAudioManager.ambientVolume.")]
    [SerializeField] private float volume = 0.4f;

    [Header("Trigger")]
    [Tooltip("Tag used to identify the player collider entering this trigger.")]
    [SerializeField] private string playerTag = "Player";

    // ── Private ────────────────────────────────────────────────────────────

    private bool _triggered;

    // ── Trigger ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;
        PlayMusic();
    }

    // ── Music activation ───────────────────────────────────────────────────

    private void PlayMusic()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("[BGMTrigger] No musicClip assigned. Trigger will not play anything.");
            return;
        }

        if (GameAudioManager.Instance == null)
        {
            Debug.LogWarning("[BGMTrigger] GameAudioManager.Instance is null -- music will not play.");
            return;
        }

        GameAudioManager.Instance.PlayMusic(musicClip, volume, loop);

        Debug.Log($"[BGMTrigger] '{gameObject.name}' fired -- playing '{musicClip.name}' (loop={loop}).");
    }

    // ── Scene gizmo ────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"BGMTrigger ({(musicClip != null ? musicClip.name : "no clip")}, loop={loop})");
#endif
    }
}
