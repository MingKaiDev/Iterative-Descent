using UnityEngine;

/// <summary>
/// Temporary test harness -- opens the drain puzzle on Play without needing the prop.
/// DELETE this script before the final build.
/// </summary>
public class DrainTestBootstrap : MonoBehaviour
{
    [SerializeField] private DrainPuzzleUI drainPuzzleUI;

    private void Start()
    {
        drainPuzzleUI.gameObject.SetActive(true);
        drainPuzzleUI.InitPuzzle(onClose: () => drainPuzzleUI.gameObject.SetActive(false));
    }
}
