using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;
using UILib;
using UILib.Patches;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

using UECamera = UnityEngine.Camera;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * A dedicated camera for flying between flags, and for the
     * picker's overview shot.
     *
     * Camera setup referenced from Kaden5480's poy-freecam code.
     * </summary>
     */
    internal class FlyCamera {
        private static Logger logger = new Logger(typeof(FlyCamera));

        private static FlyCamera instance;

        private GameObject root;
        internal static UECamera camera => instance?.cameraComponent;
        private UECamera cameraComponent;
        private AudioListener listener;
        private PostProcessLayer processLayer;

        private Lock @lock;
        private IEnumerator flight;

        // Bounds for overview zoom.
        private static Vector3 boundsCenter;
        private static float boundsRadius = 1f;

        /**
         * <summary>
         * Initializes the fly camera. Stays disabled until needed.
         * </summary>
         */
        internal FlyCamera() {
            instance = this;

            root = new GameObject("MoreRoutingFlags Camera");
            root.tag = "MainCamera";
            GameObject.DontDestroyOnLoad(root);

            cameraComponent = root.AddComponent<UECamera>();
            listener = root.AddComponent<AudioListener>();
            processLayer = root.AddComponent<PostProcessLayer>();

            cameraComponent.enabled = false;
            cameraComponent.farClipPlane = 5000f;
            root.SetActive(false);
        }

        /**
         * <summary>
         * Whether the fly camera is currently the active camera.
         * </summary>
         */
        internal static bool isActive {
            get => instance != null && UECamera.main == instance.cameraComponent;
        }

        /**
         * <summary>
         * Checks whether the fly camera is allowed to take over
         * right now.
         * </summary>
         */
        internal static bool CanStart() {
            // Skip once already active: these checks would block every transition after the first
            if (isActive == false) {
                if (UECamera.main != Cache.playerCamera) {
                    return false;
                }

                if (InGameMenu.isCurrentlyNavigationMenu == true) {
                    return false;
                }
            }

            if (Bivouac.currentlyUsingBivouac == true) {
                return false;
            }

            if (RopeAnchor.currentlyUsingRope == true) {
                return false;
            }

            if (Plugin.currentSet == null) {
                return false;
            }

            return true;
        }

        /**
         * <summary>
         * Copies post processing settings from the player's camera.
         * </summary>
         */
        private void CopyPostProcessing() {
            if (Cache.playerCamera == null) {
                return;
            }

            cameraComponent.renderingPath = Cache.playerCamera.renderingPath;

            PostProcessLayer originalLayer = Cache.playerCamera.GetComponent<PostProcessLayer>();
            if (originalLayer == null) {
                return;
            }

            string[] fieldNames = new[] {
                "m_ActiveEffects", "m_Resources", "m_OldResources",
            };

            foreach (string name in fieldNames) {
                FieldInfo info = AccessTools.Field(typeof(PostProcessLayer), name);
                info.SetValue(processLayer, info.GetValue(originalLayer));
            }

            processLayer.antialiasingMode = originalLayer.antialiasingMode;
            processLayer.volumeLayer = originalLayer.volumeLayer;
        }

        /**
         * <summary>
         * Enables the fly camera, taking over from the player's camera.
         * </summary>
         */
        private void Enable() {
            if (@lock == null) {
                @lock = new Lock();
            }

            // Block vanilla navigation while active.
            @lock.SetMode(LockMode.Default | LockMode.Navigation);

            CopyPostProcessing();

            root.transform.position = Cache.playerCamera.transform.position;
            root.transform.rotation = Cache.playerCamera.transform.rotation;

            if (Cache.playerRb != null) {
                Cache.playerRb.isKinematic = true;
                Cache.playerRb.velocity = Vector3.zero;
            }

            Cache.playerCamera.enabled = false;
            root.SetActive(true);
            cameraComponent.enabled = true;
        }

        /**
         * <summary>
         * Gets the camera distance from the overview center.
         * </summary>
         */
        internal static float DistanceFromBoundsCenter() {
            if (isActive == false) {
                return 0f;
            }

            return Vector3.Distance(instance.root.transform.position, boundsCenter);
        }

        /**
         * <summary>
         * Moves the overview camera along a direction.
         * </summary>
         * <param name="direction">Direction to move along, need not be normalized</param>
         * <param name="amount">Distance to move, negative pulls back</param>
         */
        internal static void Zoom(Vector3 direction, float amount) {
            if (isActive == false || instance.flight != null) {
                return;
            }

            Vector3 dir = direction.normalized;

            if (amount > 0f) {
                amount = ClampZoomDistance(instance.root.transform.position, dir, amount);
            }

            Vector3 desired = instance.root.transform.position + dir * amount;

            // Keep zoom within the overview.
            Vector3 fromCenter = desired - boundsCenter;
            if (fromCenter.magnitude > boundsRadius) {
                desired = boundsCenter + fromCenter.normalized * boundsRadius;
            }

            instance.root.transform.position = desired;
        }

        /**
         * <summary>
         * Limits zoom before terrain.
         * </summary>
         */
        private static float ClampZoomDistance(Vector3 origin, Vector3 direction, float distance) {
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, distance, Cache.terrainMask) == true) {
                return Mathf.Max(0f, hit.distance - 1f);
            }

            return distance;
        }

        /**
         * <summary>
         * Releases the fly camera, restoring the vanilla camera
         * and rigidbody. Always safe to call, even if not active.
         * </summary>
         */
        internal static void Release() {
            if (instance == null) {
                return;
            }

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            if (instance.@lock != null) {
                instance.@lock.Close();
                instance.@lock = null;
            }

            instance.cameraComponent.enabled = false;
            instance.root.SetActive(false);

            if (Cache.playerCamera != null) {
                Cache.playerCamera.enabled = true;
            }

            if (Cache.playerRb != null) {
                Cache.playerRb.isKinematic = false;
            }
        }

        /**
         * <summary>
         * Gets the fly camera position.
         * </summary>
         */
        internal static Vector3 position => instance?.root.transform.position ?? Vector3.zero;

        /**
         * <summary>
         * Gets the fly camera rotation.
         * </summary>
         */
        internal static Quaternion rotation => instance?.root.transform.rotation ?? Quaternion.identity;

        /**
         * <summary>
         * Checks whether the camera is flying.
         * </summary>
         */
        internal static bool isFlying => instance != null && instance.flight != null;

        /**
         * <summary>
         * Stops flying and snaps to a pose.
         * </summary>
         * <param name="pos">The position to snap to</param>
         * <param name="rot">The rotation to snap to</param>
         */
        internal static void SnapTo(Vector3 pos, Quaternion rot) {
            if (isActive == false) {
                return;
            }

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            instance.root.transform.position = pos;
            instance.root.transform.rotation = rot;
        }

        /**
         * <summary>
         * Rotates the camera in place.
         * </summary>
         * <param name="deltaYaw">Degrees to rotate around world up</param>
         * <param name="deltaPitch">Degrees to rotate up/down</param>
         */
        internal static void FreeLook(float deltaYaw, float deltaPitch) {
            if (isActive == false) {
                return;
            }

            Vector3 euler = instance.root.transform.eulerAngles;
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;

            pitch = Mathf.Clamp(pitch - deltaPitch, -89f, 89f);
            float yaw = euler.y + deltaYaw;

            instance.root.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /**
         * <summary>
         * Flies to a pose.
         * </summary>
         * <param name="pos">The position to fly to</param>
         * <param name="rot">The rotation to fly to</param>
         * <param name="onArrive">Called once the flight finishes</param>
         */
        internal static void FlyBackTo(Vector3 pos, Quaternion rot, Action onArrive) {
            if (isActive == false) {
                onArrive?.Invoke();
                return;
            }

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            if (Config.flyDuration.Value <= 0f) {
                instance.root.transform.position = pos;
                instance.root.transform.rotation = rot;
                onArrive?.Invoke();
                return;
            }

            instance.flight = instance.FlyBackRoutine(pos, rot, onArrive);
            Plugin.instance.StartCoroutine(instance.flight);
        }

        /**
         * <summary>
         * Flies back and clears the active routine.
         * </summary>
         */
        private IEnumerator FlyBackRoutine(Vector3 pos, Quaternion rot, Action onArrive) {
            try {
                yield return Ease(
                    root.transform.position, pos,
                    root.transform.rotation, rot,
                    Config.flyDuration.Value / 2f
                );
            }
            finally {
                flight = null;
                onArrive?.Invoke();
            }
        }

        /**
         * <summary>
         * Flies to a flag's saved view.
         *
         * Skips the flight entirely and calls <paramref name="onArrive"/>
         * synchronously when <see cref="Config.flyDuration"/> is 0.
         * </summary>
         * <param name="flag">The flag to fly to</param>
         * <param name="onArrive">Called once the flight finishes</param>
         */
        internal static void FlyTo(Flag flag, Action onArrive) {
            if (CanStart() == false) {
                return;
            }

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            if (UECamera.main != instance.cameraComponent) {
                instance.Enable();
            }

            if (Config.flyDuration.Value <= 0f) {
                instance.Arrive(flag);
                onArrive?.Invoke();
                return;
            }

            instance.flight = instance.FlightRoutine(flag, onArrive);
            Plugin.instance.StartCoroutine(instance.flight);
        }

        /**
         * <summary>
         * Raycasts from origin toward direction and returns a point
         * short of anything hit, or the full-distance point if clear.
         * Shared by every camera placement so none of them can end up
         * embedded in terrain.
         * </summary>
         * <param name="origin">Where the ray starts</param>
         * <param name="direction">Normalized ray direction</param>
         * <param name="distance">Maximum distance to travel</param>
         */
        private static Vector3 ClampAlongRay(Vector3 origin, Vector3 direction, float distance) {
            Vector3 endPos = origin + direction * distance;

            RaycastHit hit;
            bool didHit = Physics.Raycast(origin, direction, out hit, distance, Cache.terrainMask);

            if (didHit == true) {
                endPos = hit.point - direction * 1f;
            }

            return endPos;
        }

        /**
         * <summary>
         * Computes an overview position for all flags.
         * </summary>
         * <param name="flags">The flags to frame</param>
         */
        private Vector3 ComputeOverview(List<Flag> flags, out Vector3 center) {
            Vector3 normalSum = Vector3.up;
            float radius = 1f;

            List<Vector3> points = new List<Vector3>();

            if (flags.Count == 0) {
                points.Add(Cache.playerTransform != null ? Cache.playerTransform.position : Vector3.zero);

                // Keep the empty overview above ground.
                if (Cache.playerCamera != null) {
                    Vector3 lookBack = -Cache.playerCamera.transform.forward;
                    lookBack.y = Mathf.Max(lookBack.y, 0.3f);
                    normalSum = lookBack;
                }
            }

            foreach (Flag flag in flags) {
                points.Add(FlagPlacer.Resolve(flag));
                normalSum += flag.normal;
            }

            center = Vector3.zero;
            foreach (Vector3 point in points) {
                center += point;
            }
            center /= points.Count;

            foreach (Vector3 point in points) {
                radius = Mathf.Max(radius, Vector3.Distance(center, point));
            }

            Vector3 normal = normalSum.normalized;
            // sin, not tan: fits a bounding SPHERE in the view cone, tan under-shoots
            float distance = radius / Mathf.Sin(cameraComponent.fieldOfView * Mathf.Deg2Rad / 2f) * 1.2f
                + Config.flyPullBack.Value;

            // Safety net for a stray far-off flag: picker buttons edge-clamp
            // anyway, so this just keeps the overview from becoming unusable
            distance = Mathf.Min(distance, 500f);

            // No terrain-avoidance raycast: an outward ray on a slope nearly always
            // hits nearby rock and collapses the pull-back, worth the occasional clip
            boundsCenter = center;
            boundsRadius = Mathf.Max(radius * 1.5f, distance);

            return center + normal * distance;
        }

        /**
         * <summary>
         * Snaps the camera straight to a flag's saved view.
         * </summary>
         * <param name="flag">The flag to snap to</param>
         */
        private void Arrive(Flag flag) {
            Vector3 pos = FlagPlacer.Resolve(flag);
            root.transform.position = ClampAlongRay(pos, -flag.normal, Config.flyPullBack.Value);
            root.transform.rotation = Quaternion.Euler(flag.camY, flag.camX, 0f);

            if (Cache.distanceActivator != null) {
                Cache.distanceActivator.ForceCheck();
            }
        }

        /**
         * <summary>
         * Eases the camera from its current pose to a flag's saved view.
         * </summary>
         * <param name="flag">The flag to fly to</param>
         * <param name="onArrive">Called once the flight finishes</param>
         */
        private IEnumerator FlightRoutine(Flag flag, Action onArrive) {
            try {
                Vector3 startPos = root.transform.position;
                Quaternion startRot = root.transform.rotation;

                Vector3 targetPos = FlagPlacer.Resolve(flag);
                Vector3 endPos = ClampAlongRay(targetPos, -flag.normal, Config.flyPullBack.Value);
                Quaternion endRot = Quaternion.Euler(flag.camY, flag.camX, 0f);

                // Match the switch animation's arrival speed.
                yield return Ease(startPos, endPos, startRot, endRot, Config.flyDuration.Value / 2f);
            }
            finally {
                flight = null;
                onArrive?.Invoke();
            }
        }

        /**
         * <summary>
         * Flies from the current pose out to an overview of every flag,
         * then in to a target flag's saved view -- the GTA-style
         * character-switch shot. Used for the next/previous shortcuts,
         * which start from the player's own first-person view rather
         * than an already-open picker overview.
         * </summary>
         * <param name="set">The flag set to frame on the way out</param>
         * <param name="target">The flag to arrive at</param>
         * <param name="onArrive">Called once the flight finishes</param>
         */
        private IEnumerator SwitchRoutine(FlagSet set, Flag target, Action onArrive) {
            try {
                Vector3 startPos = root.transform.position;
                Quaternion startRot = root.transform.rotation;

                Vector3 overviewPos = ComputeOverview(set.flags, out Vector3 overviewCenter);
                Quaternion overviewRot = Quaternion.LookRotation(overviewCenter - overviewPos);

                float leg = Config.flyDuration.Value / 2f;
                yield return Ease(startPos, overviewPos, startRot, overviewRot, leg);

                Vector3 targetPos = FlagPlacer.Resolve(target);
                Vector3 endPos = ClampAlongRay(targetPos, -target.normal, Config.flyPullBack.Value);
                Quaternion endRot = Quaternion.Euler(target.camY, target.camX, 0f);

                yield return Ease(overviewPos, endPos, overviewRot, endRot, leg);
            }
            finally {
                flight = null;
                onArrive?.Invoke();
            }
        }

        /**
         * <summary>
         * Eases the camera between two poses over a duration. Skips
         * straight to the end pose when duration is 0.
         * </summary>
         * <param name="startPos">Starting position</param>
         * <param name="endPos">Ending position</param>
         * <param name="startRot">Starting rotation</param>
         * <param name="endRot">Ending rotation</param>
         * <param name="duration">How long the ease takes, in seconds</param>
         */
        private IEnumerator Ease(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float duration) {
            float timer = 0f;

            while (timer < duration) {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float eased = t * t * (3f - 2f * t);

                root.transform.position = Vector3.Lerp(startPos, endPos, eased);
                root.transform.rotation = Quaternion.Slerp(startRot, endRot, eased);

                if (Cache.distanceActivator != null) {
                    Cache.distanceActivator.ForceCheck();
                }

                yield return null;
            }

            root.transform.position = endPos;
            root.transform.rotation = endRot;
        }

        /**
         * <summary>
         * Frames all flags in an overview shot.
         * </summary>
         * <param name="flags">The flags to frame</param>
         * <returns>Whether the camera actually took over</returns>
         */
        internal static bool FrameAll(List<Flag> flags) {
            if (CanStart() == false && isActive == false) {
                return false;
            }

            if (UECamera.main != instance.cameraComponent) {
                instance.Enable();
            }

            Vector3 desiredPos = instance.ComputeOverview(flags, out Vector3 center);

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            Quaternion endRot = Quaternion.LookRotation(center - desiredPos);
            instance.flight = instance.FrameAllRoutine(
                instance.root.transform.position, desiredPos,
                instance.root.transform.rotation, endRot,
                Config.flyDuration.Value
            );
            Plugin.instance.StartCoroutine(instance.flight);

            return true;
        }

        /**
         * <summary>
         * Frames the overview and clears the active routine.
         * </summary>
         */
        private IEnumerator FrameAllRoutine(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float duration) {
            try {
                yield return Ease(startPos, endPos, startRot, endRot, duration);
            }
            finally {
                flight = null;
            }
        }

        /**
         * <summary>
         * Flies through the overview to a target flag.
         * </summary>
         * <param name="set">The flag set to frame on the way out</param>
         * <param name="target">The flag to arrive at</param>
         * <param name="onArrive">Called once the flight finishes</param>
         */
        internal static void SwitchTo(FlagSet set, Flag target, Action onArrive) {
            if (CanStart() == false) {
                return;
            }

            if (instance.flight != null) {
                Plugin.instance.StopCoroutine(instance.flight);
                instance.flight = null;
            }

            if (UECamera.main != instance.cameraComponent) {
                instance.Enable();
            }

            if (Config.flyDuration.Value <= 0f) {
                instance.Arrive(target);
                onArrive?.Invoke();
                return;
            }

            instance.flight = instance.SwitchRoutine(set, target, onArrive);
            Plugin.instance.StartCoroutine(instance.flight);
        }
    }
}
