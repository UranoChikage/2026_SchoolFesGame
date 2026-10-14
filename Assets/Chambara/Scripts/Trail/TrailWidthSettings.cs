using UnityEngine;

namespace Chambara
{
    [RequireComponent(typeof(TrailRenderer))]
    public class TrailWidthSettings : MonoBehaviour
    {
        [SerializeField] TrailRenderer trailRenderer;
        [SerializeField] float widthMultiplier = 1f;

        private void Awake()
        {
            NullGetComponent();
            if (trailRenderer != null)
            {
                trailRenderer.widthMultiplier = widthMultiplier;
            }
        }

        void NullGetComponent()
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }
        }


        private void OnValidate()
        {
            NullGetComponent();
            if (trailRenderer != null)
            {
                trailRenderer.widthMultiplier = widthMultiplier;
            }
        }
    }
}
