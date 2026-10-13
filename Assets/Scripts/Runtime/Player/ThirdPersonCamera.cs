using UnityEngine;

namespace EggRescue
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 0.1f, -2.5f);
        [SerializeField] float mouseSensitivity = 2.2f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 55f;
        [SerializeField] float maxDropBelowPivot = 0.1f;
        [SerializeField] float collisionRadius = 0.3f;
        [SerializeField] float collisionSkin = 0.08f;
        [Tooltip("Maximum speed when restoring camera distance after an obstruction.")]
        [SerializeField] float avoidanceMoveSpeed = 2f;
        [SerializeField] float collisionRecoveryTime = 0.3f;
        [SerializeField] float collisionRecoveryDelay = 0.15f;
        [SerializeField] float collisionDistanceDeadZone = 0.04f;

        float _yaw;
        float _pitch = 8f;
        bool _snap;
        float _armDistance;
        float _armVelocity;
        float _recoveryWait;
        static RaycastHit[] Hits = new RaycastHit[24];
        static Collider[] Overlaps = new Collider[24];
        Renderer[] _playerRenderers;
        Material[][] _originalMaterials;
        Material[][] _fadeMaterials;
        Transform _rendererRoot;
        bool _playerFaded;

        /// <summary>???????? PlayerController ????????????????</summary>
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }

        public void SetView(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            SnapToTarget();
        }

        public void SetTarget(Transform t)
        {
            if (t != null && PlayerController.Instance != null && t == PlayerController.Instance.transform
                && PlayerController.Instance.CameraPivot != null)
                t = PlayerController.Instance.CameraPivot;
            target = t;
            if (t != null)
                _yaw = t.eulerAngles.y;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var position = AvoidGeometry(target.position, target.position + rot * offset);
            transform.SetPositionAndRotation(position, rot);
            UpdatePlayerVisibility();
            ResetArm(target.position, position);
            _snap = true;
        }

        void Awake()
        {
            Instance = this;
        }

        public void SetMouseSensitivity(float value)
        {
            mouseSensitivity = Mathf.Max(0.2f, value);
        }

        void Start()
        {
            mouseSensitivity = GameSettings.MouseSensitivity;
            if (target == null && PlayerController.Instance != null)
                SetTarget(PlayerController.Instance.CameraPivot != null
                    ? PlayerController.Instance.CameraPivot
                    : PlayerController.Instance.transform);
            else
                SnapToTarget();
            if (!GameEvents.InputLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            // ? Update ??????????????????????????? GetAxis ??????????
            if (GameEvents.InputLocked) return;
            _yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            if (_yaw > 360f) _yaw -= 360f;
            else if (_yaw < -360f) _yaw += 360f;
        }

        void LateUpdate()
        {
            if (target == null) return;

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = target.position;
            if (_snap)
            {
                var desired = AvoidGeometry(pivot, pivot + rot * offset);
                transform.SetPositionAndRotation(desired, rot);
                ResetArm(pivot, desired);
                _snap = false;
                UpdatePlayerVisibility();
                return;
            }

            var position = ResolvePosition(pivot, pivot + rot * offset, Time.deltaTime);
            // Collision changes distance only. The mouse owns orientation even when
            // the arm collapses; looking back at a nearby pivot would amplify tiny
            // positional corrections into large involuntary camera rotations.
            transform.SetPositionAndRotation(position, rot);
            UpdatePlayerVisibility();
        }

        Vector3 ResolvePosition(Vector3 pivot, Vector3 wanted, float deltaTime)
        {
            wanted.y = Mathf.Max(wanted.y, pivot.y - maxDropBelowPivot);
            var arm = wanted - pivot;
            var length = arm.magnitude;
            if (length < 0.001f)
            {
                ResetArm(pivot, pivot);
                return pivot;
            }
            var desired = AvoidGeometry(pivot, wanted);
            var safeDistance = Mathf.Min(length, Vector3.Distance(pivot, desired));
            deltaTime = Mathf.Max(0f, deltaTime);
            if (safeDistance < _armDistance)
            {
                // Retraction is a safety constraint. Smoothing through a new wall
                // would clip it; only the outward recovery may lag behind.
                _armDistance = safeDistance;
                _armVelocity = 0f;
                _recoveryWait = Mathf.Max(0f, collisionRecoveryDelay);
            }
            else if (safeDistance < length - 0.001f
                && safeDistance - _armDistance <= Mathf.Max(0f, collisionDistanceDeadZone))
            {
                // Hold a conservative distance at seams and moving collider edges.
                _recoveryWait = Mathf.Max(0f, collisionRecoveryDelay);
                _armVelocity = 0f;
            }
            else
            {
                var recoveryDelta = Mathf.Max(0f, deltaTime - _recoveryWait);
                _recoveryWait = Mathf.Max(0f, _recoveryWait - deltaTime);
                if (recoveryDelta > 0f)
                    _armDistance = Mathf.SmoothDamp(_armDistance, safeDistance, ref _armVelocity,
                        Mathf.Max(0.01f, collisionRecoveryTime), Mathf.Max(0.1f, avoidanceMoveSpeed), recoveryDelta);
            }
            _armDistance = Mathf.Clamp(_armDistance, 0f, safeDistance);
            // Resolve from today's player position rather than sweeping from the old
            // world position, which can strand the camera above a roof during a fall.
            var direction = arm / length;
            var position = pivot + direction * _armDistance;
            var radius = CollisionRadius();
            if (WorldOverlap(position, radius))
            {
                // The pivot sphere may graze a wall while the arm points away from it.
                // A cast can allow the far endpoint even though our shorter, recovering
                // arm is still overlapped. Escape only as far as necessary on this ray.
                var step = Mathf.Max(0.02f, radius * 0.25f);
                while (_armDistance < safeDistance && WorldOverlap(position, radius))
                {
                    _armDistance = Mathf.Min(safeDistance, _armDistance + step);
                    position = pivot + direction * _armDistance;
                }
                _armVelocity = 0f;
                _recoveryWait = Mathf.Max(0f, collisionRecoveryDelay);
            }
            return position;
        }

        Vector3 AvoidGeometry(Vector3 pivot, Vector3 desired)
        {
            // Never search for an overhead or sideways view. In tight spaces the
            // existing proximity fade keeps the player visible at a short distance.
            return SolveArm(pivot, desired);
        }

        void ResetArm(Vector3 pivot, Vector3 position)
        {
            _armDistance = Vector3.Distance(pivot, position);
            _armVelocity = 0f;
            _recoveryWait = 0f;
        }

        float CollisionRadius()
        {
            var cam = GetComponent<Camera>();
            return cam != null ? Mathf.Max(collisionRadius, cam.nearClipPlane) : collisionRadius;
        }

        Vector3 SolveArm(Vector3 pivot, Vector3 desired, bool clampHeight = true)
        {
            var floorY = pivot.y - maxDropBelowPivot;
            if (clampHeight && desired.y < floorY)
                desired.y = floorY;

            var delta = desired - pivot;
            var dist = delta.magnitude;
            if (dist < 0.001f) return pivot;
            var dir = delta / dist;

            var radius = CollisionRadius();

            var allowed = dist;
            var count = Physics.SphereCastNonAlloc(
                pivot, radius, dir, Hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == Hits.Length)
            {
                Hits = Physics.SphereCastAll(pivot, radius, dir, dist,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = Hits.Length;
                System.Array.Resize(ref Hits, count + 1);
            }
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (IsPlayer(hit.collider)) continue;
                // A cast starting overlapped reports distance zero even when moving away
                // from the wall. Only ignore it when the pivot is outside and moving away.
                if (hit.distance <= 0f)
                {
                    var away = pivot - hit.collider.ClosestPoint(pivot);
                    if (away.sqrMagnitude > 0.000001f && Vector3.Dot(away, dir) > 0f)
                        continue;
                }
                if (hit.distance < allowed)
                    allowed = hit.distance;
            }

            if (allowed < dist)
                allowed = Mathf.Max(0f, allowed - collisionSkin);
            allowed = Mathf.Min(allowed, dist);

            var pos = pivot + dir * allowed;
            if (WorldOverlap(pos, radius * 0.9f))
                pos = RetractUntilClear(pivot, dir, allowed, radius * 0.9f);
            return pos;
        }

        Vector3 RetractUntilClear(Vector3 pivot, Vector3 dir, float allowed, float radius)
        {
            // The pivot can overlap a ledge during a jump. Do not assume the near
            // endpoint is clear (the old binary search returned it without checking).
            var steps = Mathf.Max(1, Mathf.CeilToInt(allowed / Mathf.Max(0.02f, radius * 0.5f)));
            for (var i = 0; i <= steps; i++)
            {
                var point = pivot + dir * Mathf.Lerp(allowed, 0.05f, (float)i / steps);
                if (!WorldOverlap(point, radius)) return point;
            }
            // No room along the arm: stay close rather than crossing the wall.
            // UpdatePlayerVisibility makes the character translucent in this last resort.
            return pivot;
        }

        static bool WorldOverlap(Vector3 point, float radius)
        {
            var count = Physics.OverlapSphereNonAlloc(point, radius, Overlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == Overlaps.Length)
            {
                Overlaps = Physics.OverlapSphere(point, radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = Overlaps.Length;
                System.Array.Resize(ref Overlaps, count + 1);
            }
            for (var i = 0; i < count; i++)
                if (Overlaps[i] != null && !IsPlayer(Overlaps[i])) return true;
            return false;
        }

        void UpdatePlayerVisibility()
        {
            var player = PlayerController.Instance;
            var root = player != null ? player.transform : null;
            if (_rendererRoot != root)
            {
                RestorePlayerVisibility();
                _rendererRoot = root;
                _playerRenderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
            }
            if (_playerRenderers == null) return;

            var cam = GetComponent<Camera>();
            var clearance = cam != null ? Mathf.Max(collisionRadius, cam.nearClipPlane) : collisionRadius;
            // Hysteresis prevents the model flashing at the visibility boundary.
            if (_playerFaded) clearance += 0.15f;
            var hide = false;
            foreach (var renderer in _playerRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (renderer.bounds.SqrDistance(transform.position) < clearance * clearance)
                {
                    hide = true;
                    break;
                }
            }
            if (hide == _playerFaded) return;
            if (!hide)
            {
                RestorePlayerVisibility();
                return;
            }
            var shader = Resources.Load<Shader>("CameraProximityFade");
            if (shader == null) return;
            _originalMaterials = new Material[_playerRenderers.Length][];
            _fadeMaterials = new Material[_playerRenderers.Length][];
            for (var i = 0; i < _playerRenderers.Length; i++)
            {
                var renderer = _playerRenderers[i];
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                var originals = renderer.sharedMaterials;
                _originalMaterials[i] = originals;
                var faded = new Material[originals.Length];
                for (var j = 0; j < originals.Length; j++)
                {
                    var original = originals[j];
                    var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    if (original != null)
                    {
                        material.mainTexture = original.mainTexture;
                        material.mainTextureScale = original.mainTextureScale;
                        material.mainTextureOffset = original.mainTextureOffset;
                        if (original.HasProperty("_BaseColor")) material.color = original.GetColor("_BaseColor");
                        else if (original.HasProperty("_Color")) material.color = original.GetColor("_Color");
                    }
                    faded[j] = material;
                }
                _fadeMaterials[i] = faded;
                renderer.sharedMaterials = faded;
            }
            _playerFaded = true;
        }

        void RestorePlayerVisibility()
        {
            if (!_playerFaded) return;
            for (var i = 0; i < _playerRenderers.Length; i++)
            {
                if (_originalMaterials[i] == null) continue;
                if (_playerRenderers[i] != null) _playerRenderers[i].sharedMaterials = _originalMaterials[i];
                foreach (var material in _fadeMaterials[i])
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
            }
            _originalMaterials = null;
            _fadeMaterials = null;
            _playerFaded = false;
        }

        void OnDisable()
        {
            RestorePlayerVisibility();
        }

        static bool IsPlayer(Collider col)
        {
            var player = PlayerController.Instance;
            if (player == null || col == null) return false;
            var root = player.transform;
            return col.transform == root || col.transform.IsChildOf(root);
        }

    }
}
