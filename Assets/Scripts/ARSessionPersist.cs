using UnityEngine;

/// Attach to the AR Session GameObject.
/// Keeps AR session alive across scene loads so ARCameraBackground
/// doesn't need to reinitialize in the Tutorial scene.
public class ARSessionPersist : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
