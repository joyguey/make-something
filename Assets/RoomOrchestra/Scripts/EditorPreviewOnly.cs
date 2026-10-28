using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Keeps an editor-preview camera out of the way on device without the EditorOnly tag.
    /// EditorOnly objects are stripped from player builds but still exported by the PICO SpatialAdapter
    /// exporter, which shifts every later object by one and draws them with their neighbour's mesh,
    /// material and pose. So the object stays in the build, and only its Camera is switched off.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EditorPreviewOnly : MonoBehaviour
    {
#if !UNITY_EDITOR
        void Awake() => GetComponent<Camera>().enabled = false;
#endif
    }
}
