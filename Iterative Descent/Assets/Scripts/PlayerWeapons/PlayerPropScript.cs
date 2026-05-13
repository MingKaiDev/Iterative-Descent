using UnityEngine;
using System.Linq;

public class PlayerPropScript : MonoBehaviour
{
    void Start()
    {
        Transform rightHand = GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.name == "mixamorig:RightHand");

        if (rightHand != null)
        {
            GameObject gun = GameObject.Find("Glock17");
            if (gun != null)
            {
                gun.transform.SetParent(rightHand);
                gun.transform.localPosition = new Vector3(-0.15f, 0.37f, 0.09f);
                gun.transform.localRotation = Quaternion.Euler(13.9f, -113.3f, 68.32f);
            }
            else
            {
                Debug.LogWarning("Glock17 not found in scene.");
            }
        }
        else
        {
            Debug.LogWarning("mixamorig:RightHand bone not found.");
        }
    }
}