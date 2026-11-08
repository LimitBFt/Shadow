using System.Collections.Generic;
using ShadowPath.Input;
using ShadowPath.Interaction;
using UnityEngine;

namespace ShadowPath.Camera
{
    public enum CameraRotationMode
    {
        Fixed,
        LimitedFollow,
        FullFollow
    }

    public class CameraFollowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, InspectorName("Player")] private Transform target;
        [SerializeField] private Transform projectionPlane;
        [SerializeField] private PlayerInteractionController playerInteractionController;
        [Tooltip("Perspective camera on this Rig or one of its children. Resolved locally if left empty.")]
        [SerializeField] private UnityEngine.Camera framingCamera;

        [Header("Composition")]
        [SerializeField, InspectorName("Plane Horizontal Offset")] private float planeRightOffset;
        [SerializeField, InspectorName("Plane Vertical Offset")] private float planeUpOffset = 3f;
        [SerializeField, InspectorName("Normal Offset")] private float planeNormalOffset = 8f;
        [SerializeField] private float aimRightOffset;
        [SerializeField] private float aimUpOffset = 1f;

        [Header("Manual Zoom")]
        [SerializeField] private bool manualZoomEnabled = true;
        [SerializeField] private float minPlaneNormalOffset = -20f;
        [SerializeField] private float maxPlaneNormalOffset = -10f;
        [SerializeField, Min(0f)] private float zoomSpeed = 8f;
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.2f;
        [SerializeField, Range(0f, 0.95f)] private float zoomTriggerDeadZone = 0.1f;

        [Header("Manual Zoom Debug (Inspector Only)")]
        [SerializeField] private bool manualZoomDebug;
        [SerializeField, InspectorName("Current Manual Zoom Offset")] private float currentPlaneNormalOffset;
        [SerializeField, InspectorName("Target Zoom Offset")] private float targetPlaneNormalOffset;
        [SerializeField, InspectorName("LT Input")] private float ltInput;
        [SerializeField, InspectorName("RT Input")] private float rtInput;
        [SerializeField] private bool zoomAtLimit;

        [Header("Interaction Camera Debug (Inspector Only)")]
        [SerializeField] private bool interactionCameraDebug;
        [SerializeField] private bool interactionCameraActive;
        [SerializeField] private InteractionStation activeInteractionStation;
        [SerializeField] private Vector3 currentCameraFocus;
        [SerializeField] private Vector3 targetCameraFocus;
        [SerializeField, InspectorName("Current Zoom Offset")] private float currentCameraZoomOffset;
        [SerializeField] private float interactionZoomOffset;
        [SerializeField] private float savedManualZoomOffset;

        [Header("Position Follow")]
        [SerializeField, Min(0f)] private float horizontalSmoothTime = 0.45f;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.75f;
        [SerializeField, Min(0f)] private float maxFollowSpeed = 8f;

        [Header("Dead Zone (Plane Units, Half Extents)")]
        [SerializeField, Min(0f)] private float horizontalDeadZone = 0.6f;
        [SerializeField, Min(0f)] private float verticalDeadZone = 0.35f;

        [Header("Look Ahead")]
        [SerializeField] private bool enableLookAhead = true;
        [SerializeField, Min(0f)] private float lookAheadDistance = 1f;
        [SerializeField, Min(0f)] private float lookAheadSmoothTime = 0.65f;
        [SerializeField, Min(0f)] private float lookAheadMaxSpeed = 3f;
        [SerializeField, Min(0f)] private float lookAheadVelocityThreshold = 0.05f;

        [Header("Rotation")]
        [SerializeField] private CameraRotationMode rotationMode = CameraRotationMode.LimitedFollow;
        [SerializeField, Range(0f, 85f)] private float maxHorizontalAimAngle = 2.5f;
        [SerializeField, Range(0f, 85f)] private float maxVerticalAimAngle = 2f;
        [SerializeField, Min(0f)] private float rotationSmoothTime = 0.35f;

        [Header("Catch Up")]
        [SerializeField, Min(0f)] private float catchUpDistance = 4f;
        [SerializeField, Min(1f)] private float catchUpMultiplier = 1.8f;

        [Header("Debug")]
        [SerializeField] private Quaternion initialCameraRotation;
        [SerializeField] private float currentHorizontalAimAngle;
        [SerializeField] private float currentVerticalAimAngle;
        [SerializeField] private float targetHorizontalAimAngle;
        [SerializeField] private float targetVerticalAimAngle;
        [SerializeField] private Vector2 currentTargetPlanePosition;
        [SerializeField] private Vector2 currentCameraPlanePosition;
        [SerializeField] private float currentLookAhead;
        [SerializeField] private float currentFollowError;

        private float rightVelocity;
        private float upVelocity;
        private float normalVelocity;
        private float lookAheadVelocity;
        private Vector3 previousTargetPosition;
        private Vector2 deadZoneAnchor;
        private Vector3 filteredTargetPosition;
        private bool horizontalFollowActive;
        private float currentCatchUpGain = 1f;
        private bool initialized;
        private bool rotationInitialized;
        private Vector3 initialCameraEuler;
        private float horizontalAimVelocity;
        private float verticalAimVelocity;
        private CameraRotationMode previousRotationMode;
        private bool zoomInitialized;
        private bool warnedInvalidZoomRange;
        private float zoomVelocity;
        private float zoomBaseOffset;
        private Vector3 appliedZoomDisplacement;
        private Vector3 appliedInteractionDisplacement;
        private Vector3 interactionFocusOffset;
        private Vector3 interactionFocusVelocity;
        private float interactionZoomDelta;
        private float interactionZoomVelocity;
        private float interactionTransitionSmoothTime = 0.6f;
        private bool interactionTransitionActive;
        private InteractionStation warnedInteractionStation;
        private readonly List<Vector3> autoFramePoints = new List<Vector3>(128);
        private readonly List<Vector3> acceptedFramePoints = new List<Vector3>(128);
        private CameraAutoFrameMath.View acceptedFrameView;
        private CameraAutoFrameMath.Frame acceptedFrame;
        private Vector2 acceptedFrameRange;
        private float acceptedFramePadding;
        private bool hasAutoFrame;
        private string lastAutoFrameWarning;

        private float ManualNormalOffset => zoomInitialized ? currentPlaneNormalOffset : planeNormalOffset;
        private float EffectiveNormalOffset => ManualNormalOffset + interactionZoomDelta;

        private void Awake()
        {
            interactionCameraActive = false;
            activeInteractionStation = null;
            GameplayInput.TryInitialize(this);
            InitializeManualZoom();
            ResolveInteractionController();
        }

        private void InitializeManualZoom()
        {
            if (zoomInitialized && zoomBaseOffset == planeNormalOffset) return;
            // Keep the authored composition, including older positive offsets.
            currentPlaneNormalOffset = targetPlaneNormalOffset = planeNormalOffset;
            if (!interactionCameraActive && !interactionTransitionActive) savedManualZoomOffset = planeNormalOffset;
            zoomBaseOffset = planeNormalOffset;
            appliedZoomDisplacement = Vector3.zero;
            zoomVelocity = 0f;
            zoomInitialized = true;
        }

        private void OnEnable()
        {
            // Resume a disabled camera from its actual pose, including an unfinished return.
            if (!rotationInitialized) initialized = false;
            zoomVelocity = 0f;
        }

        private void OnDisable() => SetInteractionStation(null);

        private void Start()
        {
            TrySnapToTarget();
        }

        private void LateUpdate()
        {
            if (!HasValidReferences())
            {
                SetInteractionStation(null);
                initialized = false;
                return;
            }

            if (!initialized)
            {
                TrySnapToTarget();
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;

            InitializeManualZoom();
            UpdateInteractionStation();
            Vector3 followPosition = transform.position - appliedZoomDisplacement - appliedInteractionDisplacement;
            UpdateManualZoom(Time.deltaTime);
            appliedZoomDisplacement = planeNormal * (ManualNormalOffset - planeNormalOffset);
            UpdateDeadZone(planeRight, planeUp, planeNormal);
            UpdateLookAhead(planeRight);

            Vector3 desiredPosition = CalculateDesiredPosition(
                planeRight,
                planeUp,
                planeNormal,
                currentLookAhead);
            Vector3 desiredFollowPosition = desiredPosition - appliedZoomDisplacement;
            currentFollowError = Vector3.Distance(followPosition, desiredFollowPosition);
            currentCatchUpGain = CameraFollowMath.CatchUpGain(currentFollowError, catchUpDistance, catchUpMultiplier);
            Vector3 normalPosition = SmoothPosition(
                followPosition,
                desiredFollowPosition,
                planeRight,
                planeUp,
                planeNormal) + appliedZoomDisplacement;
            Vector3 normalFocus = normalPosition - planeRight * (planeRightOffset + currentLookAhead)
                - planeUp * planeUpOffset - planeNormal * ManualNormalOffset;
            UpdateInteractionComposition(normalFocus, planeNormal, Time.deltaTime);
            transform.position = normalPosition + appliedInteractionDisplacement;

            UpdateRotation(planeRight, planeUp, planeNormal);

            previousTargetPosition = target.position;
            RefreshDebug(desiredPosition + appliedInteractionDisplacement, planeRight, planeUp);
        }

        private void TrySnapToTarget()
        {
            if (!HasValidReferences())
            {
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;

            InitializeManualZoom();
            appliedZoomDisplacement = planeNormal * (ManualNormalOffset - planeNormalOffset);
            currentLookAhead = 0f;
            lookAheadVelocity = 0f;
            rightVelocity = 0f;
            upVelocity = 0f;
            normalVelocity = 0f;
            Vector3 relativeTarget = target.position - projectionPlane.position;
            deadZoneAnchor = new Vector2(Vector3.Dot(relativeTarget, planeRight), Vector3.Dot(relativeTarget, planeUp));
            filteredTargetPosition = target.position;
            horizontalFollowActive = false;

            transform.position = CalculateDesiredPosition(planeRight, planeUp, planeNormal, 0f)
                + appliedInteractionDisplacement;
            if (!rotationInitialized)
            {
                // Preserve the existing startup composition, then keep this base for the Play session.
                Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, 0f);
                initialCameraRotation = CalculateDesiredRotation(transform.position, aimPoint, planeUp);
                initialCameraEuler = initialCameraRotation.eulerAngles;
                rotationInitialized = true;
                previousRotationMode = rotationMode;
                transform.rotation = initialCameraRotation;
            }

            previousTargetPosition = target.position;
            initialized = true;
            currentFollowError = 0f;
            RefreshDebug(transform.position, planeRight, planeUp);
        }

        private void UpdateManualZoom(float deltaTime)
        {
            float zoomOut = Mathf.Clamp01(GameplayInput.CameraZoomOut?.ReadValue<float>() ?? 0f);
            float zoomIn = Mathf.Clamp01(GameplayInput.CameraZoomIn?.ReadValue<float>() ?? 0f);
            ltInput = manualZoomDebug ? zoomOut : 0f;
            rtInput = manualZoomDebug ? zoomIn : 0f;
            if (!manualZoomDebug) zoomAtLimit = false;
            if (interactionCameraActive)
            {
                zoomVelocity = 0f;
                return;
            }
            if (interactionTransitionActive)
            {
                // Ignore live trigger input while restoring the saved target, including modal frames.
                if (deltaTime > 0f)
                    currentPlaneNormalOffset = Mathf.SmoothDamp(currentPlaneNormalOffset, savedManualZoomOffset,
                        ref zoomVelocity, interactionTransitionSmoothTime, Mathf.Infinity, deltaTime);
                return;
            }
            if (!manualZoomEnabled || deltaTime <= 0f || GameplayInput.GameplayCommandsBlocked)
            {
                zoomVelocity = 0f;
                return;
            }

            float minimum = Mathf.Min(minPlaneNormalOffset, maxPlaneNormalOffset);
            float maximum = Mathf.Max(minPlaneNormalOffset, maxPlaneNormalOffset);
            // Crossing the ProjectionPlane would invert the view direction. Preserve legacy
            // starting offsets until the Inspector range includes them on their existing side.
            bool sameSide = minimum > 0f || maximum < 0f;
            if (!sameSide || planeNormalOffset < minimum || planeNormalOffset > maximum)
            {
                if (!warnedInvalidZoomRange)
                    Debug.LogWarning("Manual Camera Zoom requires a Min/Max range on one side of the ProjectionPlane "
                        + "that includes the authored Normal Offset. The starting composition is preserved.", this);
                warnedInvalidZoomRange = true;
                zoomVelocity = 0f;
                return;
            }
            warnedInvalidZoomRange = false;
            float deadZone = Mathf.Clamp(zoomTriggerDeadZone, 0f, 0.95f);
            zoomOut = Mathf.Max(0f, zoomOut - deadZone) / (1f - deadZone);
            zoomIn = Mathf.Max(0f, zoomIn - deadZone) / (1f - deadZone);
            float direction = Mathf.Sign(planeNormalOffset) * (zoomOut - zoomIn);
            targetPlaneNormalOffset = Mathf.Clamp(targetPlaneNormalOffset
                + direction * Mathf.Max(0f, zoomSpeed) * deltaTime, minimum, maximum);
            if (zoomSmoothTime <= 0f)
            {
                currentPlaneNormalOffset = targetPlaneNormalOffset;
                zoomVelocity = 0f;
            }
            else
            {
                float smoothed = Mathf.SmoothDamp(currentPlaneNormalOffset, targetPlaneNormalOffset,
                    ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);
                currentPlaneNormalOffset = Mathf.Clamp(smoothed, minimum, maximum);
                if (currentPlaneNormalOffset != smoothed) zoomVelocity = 0f;
            }
            if (manualZoomDebug)
                zoomAtLimit = targetPlaneNormalOffset <= minimum + 0.0001f
                    || targetPlaneNormalOffset >= maximum - 0.0001f;
        }

        private void ResolveInteractionController()
        {
            if (playerInteractionController == null && target != null)
                playerInteractionController = target.GetComponentInParent<PlayerInteractionController>();
        }

        private void UpdateInteractionStation()
        {
            ResolveInteractionController();
            InteractionStation station = playerInteractionController != null
                && playerInteractionController.isActiveAndEnabled ? playerInteractionController.CurrentStation : null;
            if (station != null && (!station.isActiveAndEnabled || !station.UseInteractionCamera)) station = null;
            if (station != null && (float.IsNaN(station.InteractionCameraNormalOffset)
                || float.IsInfinity(station.InteractionCameraNormalOffset)
                || station.InteractionCameraNormalOffset * planeNormalOffset <= 0f))
            {
                if (warnedInteractionStation != station)
                    Debug.LogWarning("Interaction Camera Offset must remain on the same side of the ProjectionPlane "
                        + "as the authored Normal Offset.", station);
                warnedInteractionStation = station;
                station = null;
            }
            if (station == null && activeInteractionStation == null && !interactionCameraActive) return;
            SetInteractionStation(station);
        }

        private void SetInteractionStation(InteractionStation station)
        {
            if (station == activeInteractionStation && interactionCameraActive == (station != null)) return;
            if (station != null)
            {
                if (!interactionCameraActive && !interactionTransitionActive)
                    savedManualZoomOffset = targetPlaneNormalOffset;
                interactionTransitionSmoothTime = station.CameraTransitionSmoothTime;
                interactionCameraActive = true;
            }
            else
            {
                if (!interactionCameraActive) return;
                targetPlaneNormalOffset = savedManualZoomOffset;
                interactionCameraActive = false;
            }
            activeInteractionStation = station;
            hasAutoFrame = false;
            lastAutoFrameWarning = null;
            interactionTransitionActive = true;
            zoomVelocity = 0f;
            // Keep the current offsets and velocities so rapid state changes remain continuous.
        }

        private void UpdateInteractionComposition(Vector3 normalFocus, Vector3 planeNormal, float deltaTime)
        {
            Vector3 desiredFocus = normalFocus;
            Vector3 desiredFocusOffset = Vector3.zero;
            float desiredZoomDelta = 0f;
            if (interactionCameraActive && activeInteractionStation != null)
            {
                if (activeInteractionStation.AutoFrameControlledObjects
                    && TryGetAutoFrame(planeNormal, out CameraAutoFrameMath.Frame frame))
                {
                    desiredFocus = frame.Focus;
                    interactionZoomOffset = frame.NormalOffset;
                    interactionTransitionSmoothTime = activeInteractionStation.AutoFrameSmoothTime;
                }
                else
                {
                    desiredFocus = activeInteractionStation.GetCameraFocusPosition();
                    interactionZoomOffset = activeInteractionStation.InteractionCameraNormalOffset;
                    interactionTransitionSmoothTime = activeInteractionStation.CameraTransitionSmoothTime;
                    if (!activeInteractionStation.AutoFrameControlledObjects)
                    {
                        hasAutoFrame = false;
                        lastAutoFrameWarning = null;
                    }
                }
                desiredFocusOffset = desiredFocus - normalFocus;
                desiredZoomDelta = interactionZoomOffset - ManualNormalOffset;
            }
            if (interactionTransitionActive && deltaTime > 0f)
            {
                interactionFocusOffset = Vector3.SmoothDamp(interactionFocusOffset, desiredFocusOffset,
                    ref interactionFocusVelocity, interactionTransitionSmoothTime, Mathf.Infinity, deltaTime);
                interactionZoomDelta = Mathf.SmoothDamp(interactionZoomDelta, desiredZoomDelta,
                    ref interactionZoomVelocity, interactionTransitionSmoothTime, Mathf.Infinity, deltaTime);
                if (!interactionCameraActive && interactionFocusOffset.sqrMagnitude < 0.000001f
                    && Mathf.Abs(interactionZoomDelta) < 0.001f
                    && Mathf.Abs(currentPlaneNormalOffset - savedManualZoomOffset) < 0.001f)
                {
                    interactionFocusOffset = interactionFocusVelocity = Vector3.zero;
                    interactionZoomDelta = interactionZoomVelocity = 0f;
                    currentPlaneNormalOffset = savedManualZoomOffset;
                    interactionTransitionActive = false;
                }
            }
            appliedInteractionDisplacement = interactionFocusOffset + planeNormal * interactionZoomDelta;
            if (interactionCameraDebug)
            {
                currentCameraFocus = normalFocus + interactionFocusOffset;
                targetCameraFocus = desiredFocus;
                currentCameraZoomOffset = EffectiveNormalOffset;
            }
        }

        private bool TryGetAutoFrame(Vector3 planeNormal, out CameraAutoFrameMath.Frame frame)
        {
            frame = default;
            if (framingCamera == null) framingCamera = GetComponentInChildren<UnityEngine.Camera>();
            if (framingCamera == null || framingCamera.orthographic
                || (!framingCamera.transform.IsChildOf(transform) && framingCamera.transform != transform))
            {
                WarnAutoFrame("Auto Frame requires a perspective Camera owned by this Camera Rig.");
                return false;
            }
            activeInteractionStation.CollectCameraFramePoints(autoFramePoints);
            if (autoFramePoints.Count == 0)
            {
                hasAutoFrame = false;
                WarnAutoFrame("Auto Frame has no valid Renderer/Collider bounds; using fixed Interaction Camera settings.");
                return false;
            }
            Matrix4x4 projection = framingCamera.nonJitteredProjectionMatrix;
            if (Mathf.Abs(projection.m32 + 1f) > 0.00001f)
                projection = framingCamera.projectionMatrix;
            var view = new CameraAutoFrameMath.View
            {
                Rotation = framingCamera.transform.rotation,
                // This contains the actual FOV/aspect/lens shift without render-time TAA jitter.
                Projection = projection,
                NearClip = framingCamera.nearClipPlane,
                FarClip = framingCamera.farClipPlane,
                PlaneNormal = planeNormal,
                LensOffset = framingCamera.transform.position - transform.position,
                CompositionOffset = projectionPlane.right * (planeRightOffset + currentLookAhead)
                    + projectionPlane.up * planeUpOffset
            };
            Vector2 range = activeInteractionStation.InteractionNormalOffsetRange;
            float padding = activeInteractionStation.FramePadding;
            if (AutoFrameNeedsUpdate(view, range, padding))
            {
                if (range.x * planeNormalOffset <= 0f || range.y * planeNormalOffset <= 0f
                    || !CameraAutoFrameMath.TryFrame(autoFramePoints, view, range, padding, out frame))
                {
                    hasAutoFrame = false;
                    WarnAutoFrame("Auto Frame calculation or Interaction distance range is invalid; using fixed settings.");
                    return false;
                }
                acceptedFrame = frame;
                acceptedFrameView = view;
                acceptedFrameRange = range;
                acceptedFramePadding = padding;
                acceptedFramePoints.Clear();
                acceptedFramePoints.AddRange(autoFramePoints);
                hasAutoFrame = true;
                if (frame.FitsAll) lastAutoFrameWarning = null;
            }
            frame = acceptedFrame;
            if (!frame.FitsAll)
                WarnAutoFrame("Controlled Objects do not fit within the Interaction distance range and camera clip planes. "
                    + "Using the farthest allowed offset.");
            return true;
        }

        private bool AutoFrameNeedsUpdate(CameraAutoFrameMath.View view, Vector2 range, float padding)
        {
            if (!hasAutoFrame || acceptedFramePoints.Count != autoFramePoints.Count
                || acceptedFrameRange != range || acceptedFramePadding != padding
                || Quaternion.Angle(acceptedFrameView.Rotation, view.Rotation) > 0.25f
                || (acceptedFrameView.CompositionOffset - view.CompositionOffset).sqrMagnitude > 0.0025f
                || (acceptedFrameView.LensOffset - view.LensOffset).sqrMagnitude > 0.000001f
                || (acceptedFrameView.PlaneNormal - view.PlaneNormal).sqrMagnitude > 0.000001f
                || acceptedFrameView.NearClip != view.NearClip || acceptedFrameView.FarClip != view.FarClip) return true;
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(acceptedFrameView.Projection[i] - view.Projection[i]) > 0.0001f) return true;
            for (int i = 0; i < autoFramePoints.Count; i++)
                if ((acceptedFramePoints[i] - autoFramePoints[i]).sqrMagnitude > 0.0025f) return true;
            return false;
        }

        private void WarnAutoFrame(string message)
        {
            if (!interactionCameraDebug || lastAutoFrameWarning == message) return;
            Debug.LogWarning(message, activeInteractionStation);
            lastAutoFrameWarning = message;
        }

        private void UpdateDeadZone(Vector3 planeRight, Vector3 planeUp, Vector3 planeNormal)
        {
            Vector3 relativeTarget = target.position - projectionPlane.position;
            float previousAnchorX = deadZoneAnchor.x;
            deadZoneAnchor.x = CameraFollowMath.AdvanceDeadZone(deadZoneAnchor.x,
                Vector3.Dot(relativeTarget, planeRight), horizontalDeadZone);
            deadZoneAnchor.y = CameraFollowMath.AdvanceDeadZone(deadZoneAnchor.y,
                Vector3.Dot(relativeTarget, planeUp), verticalDeadZone);
            horizontalFollowActive = Mathf.Abs(deadZoneAnchor.x - previousAnchorX) > 0.000001f;
            filteredTargetPosition = projectionPlane.position + planeRight * deadZoneAnchor.x
                + planeUp * deadZoneAnchor.y + planeNormal * Vector3.Dot(relativeTarget, planeNormal);
        }

        private void RefreshDebug(Vector3 desiredPosition, Vector3 planeRight, Vector3 planeUp)
        {
            Vector3 desired = desiredPosition - projectionPlane.position;
            Vector3 current = transform.position - projectionPlane.position;
            currentTargetPlanePosition = new Vector2(Vector3.Dot(desired, planeRight), Vector3.Dot(desired, planeUp));
            currentCameraPlanePosition = new Vector2(Vector3.Dot(current, planeRight), Vector3.Dot(current, planeUp));
            currentFollowError = Vector3.Distance(desiredPosition, transform.position);
        }

        private void UpdateLookAhead(Vector3 planeRight)
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 targetDelta = target.position - previousTargetPosition;
            float horizontalSpeed = Vector3.Dot(targetDelta, planeRight) / deltaTime;
            float desiredLookAhead = CameraFollowMath.LookAheadTarget(horizontalSpeed,
                enableLookAhead && (horizontalFollowActive || horizontalDeadZone <= 0f),
                lookAheadDistance, lookAheadVelocityThreshold);

            currentLookAhead = Mathf.SmoothDamp(
                currentLookAhead,
                desiredLookAhead,
                ref lookAheadVelocity,
                Mathf.Max(0.0001f, lookAheadSmoothTime),
                Mathf.Max(0f, lookAheadMaxSpeed),
                deltaTime);
        }

        private Vector3 SmoothPosition(
            Vector3 currentPosition,
            Vector3 desiredPosition,
            Vector3 planeRight,
            Vector3 planeUp,
            Vector3 planeNormal)
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return currentPosition;
            float speed = Mathf.Max(0f, maxFollowSpeed) * currentCatchUpGain;
            float currentRight = Vector3.Dot(currentPosition, planeRight);
            float currentUp = Vector3.Dot(currentPosition, planeUp);
            float currentNormal = Vector3.Dot(currentPosition, planeNormal);
            float desiredRight = Vector3.Dot(desiredPosition, planeRight);
            float desiredUp = Vector3.Dot(desiredPosition, planeUp);
            float desiredNormal = Vector3.Dot(desiredPosition, planeNormal);

            float smoothedRight = Mathf.SmoothDamp(
                currentRight,
                desiredRight,
                ref rightVelocity,
                Mathf.Max(0.0001f, horizontalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);
            float smoothedUp = Mathf.SmoothDamp(
                currentUp,
                desiredUp,
                ref upVelocity,
                Mathf.Max(0.0001f, verticalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);
            float smoothedNormal = Mathf.SmoothDamp(
                currentNormal,
                desiredNormal,
                ref normalVelocity,
                Mathf.Max(0.0001f, horizontalSmoothTime / currentCatchUpGain),
                speed,
                deltaTime);

            Vector3 smoothed = planeRight * smoothedRight
                + planeUp * smoothedUp
                + planeNormal * smoothedNormal;
            Vector3 change = smoothed - currentPosition;
            float maximumStep = speed * deltaTime;
            if (change.magnitude > maximumStep && change.sqrMagnitude > 0f)
            {
                float scale = maximumStep / change.magnitude;
                rightVelocity *= scale;
                upVelocity *= scale;
                normalVelocity *= scale;
                return currentPosition + change * scale;
            }
            return smoothed;
        }

        private Quaternion SmoothRotation(Quaternion currentRotation, Quaternion desiredRotation)
        {
            if (rotationSmoothTime <= 0f)
            {
                return desiredRotation;
            }

            float interpolation = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothTime);
            return Quaternion.Slerp(currentRotation, desiredRotation, interpolation);
        }

        private void UpdateRotation(Vector3 planeRight, Vector3 planeUp, Vector3 planeNormal)
        {
            if (previousRotationMode != rotationMode)
            {
                previousRotationMode = rotationMode;
                horizontalAimVelocity = verticalAimVelocity = 0f;
                Vector3 currentEuler = transform.rotation.eulerAngles;
                currentHorizontalAimAngle = Mathf.DeltaAngle(initialCameraEuler.y, currentEuler.y);
                currentVerticalAimAngle = Mathf.DeltaAngle(initialCameraEuler.x, currentEuler.x);
            }

            targetHorizontalAimAngle = targetVerticalAimAngle = 0f;
            if (rotationMode == CameraRotationMode.Fixed)
            {
                currentHorizontalAimAngle = currentVerticalAimAngle = 0f;
                horizontalAimVelocity = verticalAimVelocity = 0f;
                transform.rotation = initialCameraRotation;
                return;
            }

            if (rotationMode == CameraRotationMode.FullFollow)
            {
                Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, currentLookAhead);
                Quaternion desired = CalculateDesiredRotation(transform.position, aimPoint, planeUp);
                Vector3 desiredEuler = desired.eulerAngles;
                targetHorizontalAimAngle = Mathf.DeltaAngle(initialCameraEuler.y, desiredEuler.y);
                targetVerticalAimAngle = Mathf.DeltaAngle(initialCameraEuler.x, desiredEuler.x);
                Vector3 smoothedEuler = SmoothRotation(transform.rotation, desired).eulerAngles;
                transform.rotation = Quaternion.Euler(smoothedEuler.x, smoothedEuler.y, initialCameraEuler.z);
                currentHorizontalAimAngle = Mathf.DeltaAngle(initialCameraEuler.y, smoothedEuler.y);
                currentVerticalAimAngle = Mathf.DeltaAngle(initialCameraEuler.x, smoothedEuler.x);
                return;
            }

            // Recover the framing target from the actual smoothed position, without changing position follow.
            Vector3 framingTarget = transform.position
                - planeRight * (planeRightOffset + currentLookAhead)
                - planeUp * planeUpOffset - planeNormal * EffectiveNormalOffset;
            Vector3 delta = target.position + interactionFocusOffset - framingTarget;
            float horizontalExcess = CameraRotationMath.DeadZoneExcess(Vector3.Dot(delta, planeRight), horizontalDeadZone);
            float verticalExcess = CameraRotationMath.DeadZoneExcess(Vector3.Dot(delta, planeUp), verticalDeadZone);
            Vector3 localAimOffset = Quaternion.Inverse(initialCameraRotation)
                * (planeRight * horizontalExcess + planeUp * verticalExcess);
            Vector3 baseAimPoint = framingTarget + planeRight * (aimRightOffset + currentLookAhead)
                + planeUp * aimUpOffset;
            float depth = Mathf.Abs(Vector3.Dot(baseAimPoint - transform.position,
                initialCameraRotation * Vector3.forward));
            targetHorizontalAimAngle = CameraRotationMath.ClampedAimAngle(localAimOffset.x, depth, maxHorizontalAimAngle);
            targetVerticalAimAngle = CameraRotationMath.ClampedAimAngle(-localAimOffset.y, depth, maxVerticalAimAngle);
            float dt = Time.deltaTime;
            if (rotationSmoothTime <= 0f)
            {
                currentHorizontalAimAngle = targetHorizontalAimAngle;
                currentVerticalAimAngle = targetVerticalAimAngle;
                horizontalAimVelocity = verticalAimVelocity = 0f;
            }
            else if (dt > 0f)
            {
                currentHorizontalAimAngle = Mathf.SmoothDamp(currentHorizontalAimAngle, targetHorizontalAimAngle,
                    ref horizontalAimVelocity, rotationSmoothTime, Mathf.Infinity, dt);
                currentVerticalAimAngle = Mathf.SmoothDamp(currentVerticalAimAngle, targetVerticalAimAngle,
                    ref verticalAimVelocity, rotationSmoothTime, Mathf.Infinity, dt);
            }
            // Clamp current angles too, including after switching from FullFollow or reducing limits at runtime.
            currentHorizontalAimAngle = ClampCurrentAim(currentHorizontalAimAngle, maxHorizontalAimAngle, ref horizontalAimVelocity);
            currentVerticalAimAngle = ClampCurrentAim(currentVerticalAimAngle, maxVerticalAimAngle, ref verticalAimVelocity);
            // Compose Euler yaw/pitch explicitly: quaternion interpolation can introduce unwanted roll.
            transform.rotation = Quaternion.Euler(initialCameraEuler.x + currentVerticalAimAngle,
                initialCameraEuler.y + currentHorizontalAimAngle, initialCameraEuler.z);
        }

        private static float ClampCurrentAim(float angle, float maximum, ref float velocity)
        {
            float limit = Mathf.Clamp(maximum, 0f, 85f);
            float clamped = Mathf.Clamp(angle, -limit, limit);
            if (clamped != angle) velocity = 0f;
            return clamped;
        }

        private Vector3 CalculateDesiredPosition(
            Vector3 planeRight,
            Vector3 planeUp,
            Vector3 planeNormal,
            float lookAhead)
        {
            Vector3 followTarget = initialized ? filteredTargetPosition : target.position;
            return followTarget
                + planeRight * (planeRightOffset + lookAhead)
                + planeUp * planeUpOffset
                + planeNormal * ManualNormalOffset;
        }

        private Vector3 CalculateAimPoint(Vector3 planeRight, Vector3 planeUp, float lookAhead)
        {
            Vector3 followTarget = initialized ? filteredTargetPosition : target.position;
            return followTarget
                + interactionFocusOffset
                + planeRight * (aimRightOffset + lookAhead)
                + planeUp * aimUpOffset;
        }

        private Quaternion CalculateDesiredRotation(
            Vector3 cameraPosition,
            Vector3 aimPoint,
            Vector3 planeUp)
        {
            Vector3 lookDirection = aimPoint - cameraPosition;
            if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return transform.rotation;
            }

            Vector3 cameraUp = planeUp;
            if (Vector3.Cross(lookDirection.normalized, cameraUp).sqrMagnitude <= Mathf.Epsilon)
            {
                cameraUp = projectionPlane.forward;
            }

            return Quaternion.LookRotation(lookDirection, cameraUp);
        }

        private bool HasValidReferences()
        {
            return target != null && projectionPlane != null;
        }

        private void OnValidate()
        {
            horizontalSmoothTime = Mathf.Max(0f, horizontalSmoothTime);
            verticalSmoothTime = Mathf.Max(0f, verticalSmoothTime);
            maxFollowSpeed = Mathf.Max(0f, maxFollowSpeed);
            horizontalDeadZone = Mathf.Max(0f, horizontalDeadZone);
            verticalDeadZone = Mathf.Max(0f, verticalDeadZone);
            lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
            lookAheadSmoothTime = Mathf.Max(0f, lookAheadSmoothTime);
            lookAheadMaxSpeed = Mathf.Max(0f, lookAheadMaxSpeed);
            lookAheadVelocityThreshold = Mathf.Max(0f, lookAheadVelocityThreshold);
            rotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);
            maxHorizontalAimAngle = Mathf.Clamp(maxHorizontalAimAngle, 0f, 85f);
            maxVerticalAimAngle = Mathf.Clamp(maxVerticalAimAngle, 0f, 85f);
            catchUpDistance = Mathf.Max(0f, catchUpDistance);
            catchUpMultiplier = Mathf.Max(1f, catchUpMultiplier);
            zoomSpeed = Mathf.Max(0f, zoomSpeed);
            zoomSmoothTime = Mathf.Max(0f, zoomSmoothTime);
            zoomTriggerDeadZone = Mathf.Clamp(zoomTriggerDeadZone, 0f, 0.95f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasValidReferences())
            {
                return;
            }

            Vector3 planeRight = projectionPlane.right;
            Vector3 planeUp = projectionPlane.up;
            Vector3 planeNormal = projectionPlane.forward;
            float previewLookAhead = Application.isPlaying ? currentLookAhead : 0f;
            Vector3 aimPoint = CalculateAimPoint(planeRight, planeUp, previewLookAhead);
            Vector3 desiredPosition = CalculateDesiredPosition(
                planeRight,
                planeUp,
                planeNormal,
                previewLookAhead);
            if (Application.isPlaying) desiredPosition += appliedInteractionDisplacement;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimPoint, 0.15f);
            Gizmos.DrawLine(target.position, aimPoint);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(desiredPosition, 0.2f);
            Gizmos.DrawLine(desiredPosition, aimPoint);
        }
    }
}
