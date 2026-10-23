using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable pitch selector: five steps rising left to right like a staircase (low to high note,
    /// or the five udu sounds). The chosen step is raised and wider, so it reads without color.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PetalButton : MonoBehaviour
    {
        [Tooltip("Five steps, lowest first.")]
        [SerializeField] Transform[] steps;
        [SerializeField] Vector3 stepScale = new Vector3(0.025f, 0.02f, 0.03f);
        [Tooltip("Extra height and width of the selected step.")]
        [SerializeField] Vector3 selectedBoost = new Vector3(0.015f, 0.03f, 0.01f);

        /// <summary>Unselected scale of step <paramref name="i"/>: each step is a little taller (higher note).</summary>
        public Vector3 BaseScale(int i) => stepScale + new Vector3(0f, stepScale.y * 0.5f * i, 0f);

        public void Show(int petal)
        {
            if (steps == null) return;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] == null) continue;
                Vector3 target = BaseScale(i) + (i == petal ? selectedBoost : Vector3.zero);
                if (steps[i].localScale != target) steps[i].localScale = target;
            }
        }
    }
}
