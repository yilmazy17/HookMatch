using System.Collections.Generic;
using UnityEngine;

namespace Core.Gameplay.Hook
{
    /// <summary>
    /// Creates a physics rope between two cube positions using an explicitly
    /// supplied number of rope segments.
    /// </summary>
    public class Rope : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private GameObject ropeSegment;

        [Header("Rope Settings")]
        [Tooltip("Segments used per grid-cell span. GridManager multiplies this by the cube distance.")]
        [SerializeField, Min(2)] private int segmentCount = 4;

        public int SegmentCount => Mathf.Max(2, segmentCount);

        [Header("Inspector Test")]
        [SerializeField] private bool buildOnStart;
        [SerializeField] private Vector3 testFirstCubePosition;
        [SerializeField] private Vector3 testSecondCubePosition = Vector3.right;

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private bool _hasBeenBuilt;
        private Transform _firstCubeTarget;
        private Transform _secondCubeTarget;
        private Rigidbody2D _startAnchorBody;
        private Rigidbody2D _endAnchorBody;
        private float _segmentsPerWorldUnit;
        private int _currentSegmentCount;

        private void Start()
        {
            // Runtime callers such as GridManager may build the rope before
            // Start. In that case the Inspector test must not overwrite it.
            if (buildOnStart && !_hasBeenBuilt)
                BuildFromInspector();
        }

        [ContextMenu("Build Test Rope")]
        public void BuildFromInspector()
        {
            Build(testFirstCubePosition, testSecondCubePosition);
        }

        [ContextMenu("Clear Test Rope")]
        private void ClearFromInspector()
        {
            Clear();
        }

        /// <summary>
        /// Builds a rope between two cubes.
        /// Uses the segment count configured on this Rope component.
        /// </summary>
        public void Build(Transform firstCube, Transform secondCube)
        {
            Build(firstCube, secondCube, segmentCount);
        }

        public void Build(Vector3 firstCubePosition, Vector3 secondCubePosition)
        {
            Build(firstCubePosition, secondCubePosition, segmentCount);
        }

        /// <summary>
        /// Optional runtime override when a particular rope needs a different
        /// number of segments than the value configured in the Inspector.
        /// </summary>
        public void Build(Transform firstCube, Transform secondCube, int segmentCountOverride)
        {
            if (firstCube == null || secondCube == null)
            {
                Debug.LogError("[Rope] First and second cube transforms are required.", this);
                return;
            }

            _firstCubeTarget = firstCube;
            _secondCubeTarget = secondCube;
            BuildInternal(firstCube.position, secondCube.position, segmentCountOverride, true);
        }

        public void Build(Vector3 firstCubePosition, Vector3 secondCubePosition, int segmentCountOverride)
        {
            _firstCubeTarget = null;
            _secondCubeTarget = null;
            BuildInternal(firstCubePosition, secondCubePosition, segmentCountOverride, true);
        }

        public void RetargetFirstCube(Transform newFirstCube)
        {
            if (newFirstCube == null || _secondCubeTarget == null) return;

            _firstCubeTarget = newFirstCube;
            BuildInternal(
                _firstCubeTarget.position,
                _secondCubeTarget.position,
                _currentSegmentCount,
                false);
        }

        public void RetargetSecondCube(Transform newSecondCube)
        {
            if (_firstCubeTarget == null || newSecondCube == null) return;

            _secondCubeTarget = newSecondCube;
            BuildInternal(
                _firstCubeTarget.position,
                _secondCubeTarget.position,
                _currentSegmentCount,
                false);
        }

        private void BuildInternal(
            Vector3 firstCubePosition,
            Vector3 secondCubePosition,
            int segmentCountOverride,
            bool resetRetractionMetric)
        {
            _hasBeenBuilt = true;
            Clear();

            if (anchorPrefab == null || ropeSegment == null)
            {
                Debug.LogError("[Rope] Anchor and rope segment prefabs must be assigned.", this);
                return;
            }

            Vector2 direction = secondCubePosition - firstCubePosition;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                Debug.LogWarning("[Rope] Start and end positions are the same.", this);
                return;
            }

            int resolvedSegmentCount = Mathf.Max(2, segmentCountOverride);
            float endpointDistance = direction.magnitude;

            if (resetRetractionMetric)
                _segmentsPerWorldUnit = resolvedSegmentCount / endpointDistance;

            _currentSegmentCount = resolvedSegmentCount;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            _startAnchorBody = CreateAnchor(firstCubePosition, "RopeAnchor_Start");
            _endAnchorBody = CreateAnchor(secondCubePosition, "RopeAnchor_End");
            if (_startAnchorBody == null || _endAnchorBody == null)
            {
                Clear();
                return;
            }

            var segmentBodies = new Rigidbody2D[resolvedSegmentCount];
            var leftJoints = new HingeJoint2D[resolvedSegmentCount];
            var rightJoints = new HingeJoint2D[resolvedSegmentCount];

            for (int i = 0; i < resolvedSegmentCount; i++)
            {
                // The endpoint segments share the exact positions of their
                // anchors. All remaining segments are distributed uniformly
                // between them, so the prefab pieces line up edge-to-edge.
                float t = i / (float)(resolvedSegmentCount - 1);
                Vector3 position = Vector3.Lerp(firstCubePosition, secondCubePosition, t);
                GameObject segment = Instantiate(ropeSegment, position, rotation, transform);
                segment.name = $"RopeSegment_{i + 1:00}";
                _spawnedObjects.Add(segment);

                Rigidbody2D body = segment.GetComponent<Rigidbody2D>();
                HingeJoint2D[] joints = segment.GetComponents<HingeJoint2D>();
                if (body == null || joints.Length != 2)
                {
                    Debug.LogError(
                        $"[Rope] '{ropeSegment.name}' needs one Rigidbody2D and exactly two HingeJoint2D components.",
                        segment);
                    Clear();
                    return;
                }

                segmentBodies[i] = body;
                SortJointsByLocalX(joints, out leftJoints[i], out rightJoints[i]);
            }

            for (int i = 0; i < resolvedSegmentCount; i++)
            {
                // Left joint: start anchor for the first segment, otherwise
                // the previous rope segment.
                Rigidbody2D leftBody = i == 0 ? _startAnchorBody : segmentBodies[i - 1];
                ConfigureJoint(leftJoints[i], leftBody, i != 0);

                // Right joint: end anchor for the last segment, otherwise the
                // next rope segment. Internal right joints use auto configure.
                Rigidbody2D rightBody = i == resolvedSegmentCount - 1 ? _endAnchorBody : segmentBodies[i + 1];
                ConfigureJoint(rightJoints[i], rightBody, i != resolvedSegmentCount - 1);
            }
        }

        private void FixedUpdate()
        {
            if (_startAnchorBody != null && _firstCubeTarget != null)
                _startAnchorBody.MovePosition(_firstCubeTarget.position);

            if (_endAnchorBody != null && _secondCubeTarget != null)
                _endAnchorBody.MovePosition(_secondCubeTarget.position);

            RetractSegmentsIfNeeded();
        }

        private void RetractSegmentsIfNeeded()
        {
            if (_firstCubeTarget == null || _secondCubeTarget == null) return;
            if (_segmentsPerWorldUnit <= Mathf.Epsilon || _currentSegmentCount <= 2) return;

            float currentDistance = Vector3.Distance(
                _firstCubeTarget.position,
                _secondCubeTarget.position);
            int requiredSegmentCount = Mathf.Max(
                2,
                Mathf.CeilToInt(currentDistance * _segmentsPerWorldUnit));

            // Only retract. Small animation/physics fluctuations must never add
            // segments back after a piece has already been collected.
            if (requiredSegmentCount >= _currentSegmentCount) return;

            BuildInternal(
                _firstCubeTarget.position,
                _secondCubeTarget.position,
                requiredSegmentCount,
                false);
        }

        public void Clear()
        {
            foreach (GameObject spawnedObject in _spawnedObjects)
            {
                if (spawnedObject != null)
                    Destroy(spawnedObject);
            }

            _spawnedObjects.Clear();
            _startAnchorBody = null;
            _endAnchorBody = null;
        }

        private Rigidbody2D CreateAnchor(Vector3 position, string anchorName)
        {
            GameObject anchor = Instantiate(anchorPrefab, position, Quaternion.identity, transform);
            anchor.name = anchorName;
            _spawnedObjects.Add(anchor);

            Rigidbody2D body = anchor.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                Debug.LogError($"[Rope] '{anchorPrefab.name}' needs a Rigidbody2D.", anchor);
                return null;
            }

            body.bodyType = RigidbodyType2D.Kinematic;

            // Anchors are endpoints only. Their own hinge joints must not be
            // connected to the world or to another rigidbody.
            foreach (HingeJoint2D joint in anchor.GetComponents<HingeJoint2D>())
            {
                joint.connectedBody = null;
                joint.enabled = false;
            }

            return body;
        }

        private static void SortJointsByLocalX(
            HingeJoint2D[] joints,
            out HingeJoint2D leftJoint,
            out HingeJoint2D rightJoint)
        {
            if (joints[0].anchor.x <= joints[1].anchor.x)
            {
                leftJoint = joints[0];
                rightJoint = joints[1];
            }
            else
            {
                leftJoint = joints[1];
                rightJoint = joints[0];
            }
        }

        private static void ConfigureJoint(
            HingeJoint2D joint,
            Rigidbody2D connectedBody,
            bool autoConfigureConnectedAnchor)
        {
            joint.enabled = false;
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = autoConfigureConnectedAnchor;

            if (!autoConfigureConnectedAnchor)
            {
                // Preserve the joint's current world position when connecting
                // it to either the start or end anchor.
                Vector2 worldAnchor = joint.transform.TransformPoint(joint.anchor);
                joint.connectedAnchor = connectedBody.transform.InverseTransformPoint(worldAnchor);
            }

            joint.enabled = true;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
