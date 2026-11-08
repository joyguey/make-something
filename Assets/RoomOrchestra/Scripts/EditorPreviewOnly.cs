using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Keeps the editor-preview camera out of the way on the device: its Camera and its ears are switched off there
    /// (the headset rig's eye camera, <see cref="XrRig"/>, sees and hears instead).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EditorPreviewOnly : MonoBehaviour
    {
#if !UNITY_EDITOR
        void Awake()
        {
            GetComponent<Camera>().enabled = false;
            if (TryGetComponent<AudioListener>(out var ears)) ears.enabled = false;
        }
#endif
    }
}
