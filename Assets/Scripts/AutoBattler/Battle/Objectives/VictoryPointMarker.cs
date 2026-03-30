using UnityEngine;

namespace AutoBattler
{
    public sealed class VictoryPointMarker : MonoBehaviour
    {
        private const string VisualName = "_VictoryVisual";

        [SerializeField] private string pointId = "VictoryPoint";
        [SerializeField] private string displayName = "Victory Point";
        [SerializeField] private float captureRadius = 4f;
        [SerializeField] private float captureTime = 4f;
        [SerializeField] private bool requiredForVictory = true;
        [SerializeField] private ObjectiveOwner initialOwner = ObjectiveOwner.Red;
        [SerializeField] private bool visibleInGame = true;

        private ObjectiveOwner currentOwner;
        private ObjectiveOwner pendingOwner;
        private float captureProgressNormalized;
        private Renderer visualRenderer;

        public string PointId => string.IsNullOrWhiteSpace(pointId) ? name : pointId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? PointId : displayName;
        public float CaptureRadius => Mathf.Max(0.5f, captureRadius);
        public float CaptureTime => Mathf.Max(0.1f, captureTime);
        public bool RequiredForVictory => requiredForVictory;
        public ObjectiveOwner InitialOwner => initialOwner;
        public ObjectiveOwner CurrentOwner => currentOwner;
        public ObjectiveOwner PendingOwner => pendingOwner;
        public float CaptureProgressNormalized => captureProgressNormalized;
        public Vector3 Position => transform.position;

        public Vector3 GetApproachPosition(Vector3 requesterPosition)
        {
            var center = transform.position;
            center.y = requesterPosition.y;
            return center;
        }

        public void ResetRuntimeState()
        {
            SetRuntimeState(initialOwner, ObjectiveOwner.Neutral, 0f);
        }

        public void SetRuntimeState(ObjectiveOwner owner, ObjectiveOwner captureInProgress, float progressNormalized)
        {
            currentOwner = owner;
            pendingOwner = captureInProgress;
            captureProgressNormalized = Mathf.Clamp01(progressNormalized);
            UpdateVisualState();
        }

        private void Awake()
        {
            currentOwner = initialOwner;
        }

        private void OnEnable()
        {
            EnsureRuntimeVisual();
            UpdateVisualState();
        }

        private void OnDisable()
        {
            if (visualRenderer == null)
            {
                return;
            }

            var visualTransform = visualRenderer.transform;
            visualRenderer = null;
            if (visualTransform != null)
            {
                Destroy(visualTransform.gameObject);
            }
        }

        private void EnsureRuntimeVisual()
        {
            if (!Application.isPlaying || !visibleInGame)
            {
                return;
            }

            var visual = transform.Find(VisualName);
            if (visual == null)
            {
                var visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visualObject.name = VisualName;
                visualObject.transform.SetParent(transform, false);
                visualObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                visualObject.transform.localRotation = Quaternion.identity;
                var collider = visualObject.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    Destroy(collider);
                }

                visual = visualObject.transform;
            }

            visual.localScale = new Vector3(CaptureRadius * 2f, 0.025f, CaptureRadius * 2f);
            visualRenderer = visual.GetComponent<Renderer>();
        }

        private void UpdateVisualState()
        {
            EnsureRuntimeVisual();
            if (visualRenderer == null)
            {
                return;
            }

            var color = GetOwnerColor(currentOwner);
            if (pendingOwner != ObjectiveOwner.Neutral && currentOwner != pendingOwner)
            {
                color = Color.Lerp(color, GetOwnerColor(pendingOwner), Mathf.Clamp01(captureProgressNormalized));
            }

            visualRenderer.material.color = color;
        }

        private static Color GetOwnerColor(ObjectiveOwner owner)
        {
            switch (owner)
            {
                case ObjectiveOwner.Blue:
                    return new Color(0.2f, 0.45f, 1f, 0.9f);
                case ObjectiveOwner.Red:
                    return new Color(1f, 0.25f, 0.25f, 0.9f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f, 0.9f);
            }
        }

        private void Reset()
        {
            pointId = name;
            displayName = name;
            captureRadius = 4f;
            captureTime = 4f;
            requiredForVictory = true;
            initialOwner = ObjectiveOwner.Red;
            visibleInGame = true;
        }

        private void OnDrawGizmos()
        {
            var baseColor = GetOwnerColor(Application.isPlaying ? currentOwner : initialOwner);
            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f);
            Gizmos.DrawWireSphere(transform.position, CaptureRadius);

            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
            Gizmos.DrawSphere(transform.position + (Vector3.up * 0.05f), 0.2f);
        }
    }
}
