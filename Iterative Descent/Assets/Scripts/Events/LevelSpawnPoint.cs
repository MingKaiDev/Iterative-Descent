// LevelSpawnPoint.cs
// Marks where LevelTransitionManager should place the persisted Player after
// loading a new scene. Place an empty GameObject at the desired spawn position/
// facing (the object's own forward direction is the facing the player ends up
// looking), add this script, and leave 'spawnId' as "Default" unless this scene
// has more than one entry point (e.g. arriving back via a different door later).
//
// Setup per scene that can be a LevelTransitionManager.TransitionToScene() target:
//   1. Create an empty GameObject where the player should appear.
//   2. Add this script. Leave spawnId = "Default" for a scene's only entry point.
//   3. Pass that same id as the spawnPointId argument wherever this scene is
//      targeted by a LevelTransitionDoorProp (defaults to "Default" too, so for
//      a single-entry-point scene there is nothing to wire up beyond placing this).
using UnityEngine;

public class LevelSpawnPoint : MonoBehaviour
{
    [Tooltip("Matched against the spawnPointId passed to LevelTransitionManager.TransitionToScene(). " +
             "Leave as \"Default\" unless this scene has more than one entry point.")]
    public string spawnId = "Default";

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.8f, $"Spawn: {spawnId}");
#endif
    }
}
