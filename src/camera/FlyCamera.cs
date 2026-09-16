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
        private static readonly FieldInfo maskField = AccessTools.Field(typeof(RoutingFlag), "mask");

        /**
         * <summary>
         * Checks the reflected field this class needs.
         * Run once from Plugin.Awake.
         * </summary>
         */
        internal static bool ValidateFields() {
            if (maskField == null) {
                logger.LogError("Could not find vanilla field 'RoutingFlag.mask'.");
                return false;
            }

            return true;
        }

        private GameObject root;
        internal static UECamera camera => instance?.cameraComponent;
        private UECamera cameraComponent;
        private AudioListener listener;
        private PostProcessLayer processLayer;

        private Lock @lock;
        private IEnumerator flight;

        // Last-computed overview bounds, keeps manual scroll-zoom from wandering off
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

            // Not currentlyUsingFlag: it only flips true on next scene load, gate on having a flag instead
            if (Cache.routingFlag == null
                || Plugin.currentSet == null
                || Plugin.currentSet.flags.Count == 0
            ) {
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

        private Dictionary<Terrain, float> hiddenTerrainTreeDistances;

        /**
         * <summary>
         * Hides (or restores) trees while the picker overview is open,
         * by zeroing each terrain's tree draw distance. Peaks of Yore
         * paints trees through Unity's Terrain tree system rather than
         * as ordinary scene GameObjects, confirmed by testing: toggling
         * Renderer.enabled and GameObject.SetActive on the matching
         * "treeline"/"conifer" renderers had zero visual effect, while
         * this does.
         * </summary>
         * <param name="hidden">Whether clutter should be hidden</param>
         */
        internal static void SetClutterHidden(bool hidden) {
            if (instance == null) {
                return;
            }

            if (hidden == false) {
                if (instance.hiddenTerrainTreeDistances != null) {
                    foreach (KeyValuePair<Terrain, float> pair in instance.hiddenTerrainTreeDistances) {
                        if (pair.Key != null) {
                            pair.Key.treeDistance = pair.Value;
                        }
                    }

                    instance.hiddenTerrainTreeDistances = null;
                }

                return;
            }

            instance.hiddenTerrainTreeDistances = new Dictionary<Terrain, float>();

            foreach (Terrain terrain in Terrain.activeTerrains) {
                instance.hiddenTerrainTreeDistances[terrain] = terrain.treeDistance;
                terrain.treeDistance = 0f;
            }

            logger.LogInfo($"SetClutterHidden(true): zeroed tree distance on {Terrain.activeTerrains.Length} terrain(s)");
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

            @lock.SetMode(LockMode.Default);

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
         * Nudges the fly camera along a given direction (e.g. toward
         * whatever's under the mouse cursor, not just straight
         * ahead), for a manual scroll-wheel zoom while the picker is
         * open. Does nothing to the player, only this camera. No-ops
         * when not active or mid-flight (a running coroutine would
         * just overwrite the nudge next frame). Clamped against
         * terrain when zooming in (positive amount) so it can't pass
         * through geometry.
         * </summary>
         * <param name="direction">Direction to move along, need not be normalized</param>
         * <param name="amount">Distance to move, negative pulls back</param>
         */
        /**
         * <summary>
         * Distance from the camera to the last-computed overview
         * center, for scaling zoom speed to the scene's actual scale
         * (a small peak and Solemn Tempest need very different
         * scroll speeds to feel usable).
         * </summary>
         */
        internal static float DistanceFromBoundsCenter() {
            if (isActive == false) {
                return 0f;
            }

            return Vector3.Distance(instance.root.transform.position, boundsCenter);
        }

        internal static void Zoom(Vector3 direction, float amount) {
            if (isActive == false || instance.flight != null) {
                return;
            }

            Vector3 dir = direction.normalized;

            if (amount > 0f) {
                amount = ClampZoomDistance(instance.root.transform.position, dir, amount);
            }

            Vector3 desired = instance.root.transform.position + dir * amount;

            // Keep the camera within the last-framed overview area
            Vector3 fromCenter = desired - boundsCenter;
            if (fromCenter.magnitude > boundsRadius) {
                desired = boundsCenter + fromCenter.normalized * boundsRadius;
            }

            instance.root.transform.position = desired;
        }

        /**
         * <summary>
         * Shortens a zoom-in distance so it stops just short of
         * whatever it would otherwise pass through.
         * </summary>
         */
        private static float ClampZoomDistance(Vector3 origin, Vector3 direction, float distance) {
            if (Cache.routingFlag == null) {
                return distance;
            }

            int mask = ((LayerMask) maskField.GetValue(Cache.routingFlag)).value;

            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, distance, mask) == true) {
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

            if (Cache.routingFlag == null) {
                return endPos;
            }

            int mask = ((LayerMask) maskField.GetValue(Cache.routingFlag)).value;

            RaycastHit hit;
            bool didHit = Physics.Raycast(origin, direction, out hit, distance, mask);

            if (didHit == true) {
                endPos = hit.point - direction * 1f;
            }

            return endPos;
        }

        /**
         * <summary>
         * Computes the pulled-back overview position and look-at point
         * that frames every flag plus the player.
         * </summary>
         * <param name="flags">The flags to frame</param>
         */
        private Vector3 ComputeOverview(List<Flag> flags, out Vector3 center) {
            Vector3 normalSum = Vector3.up;
            float radius = 1f;

            List<Vector3> points = new List<Vector3>();
            points.Add(Cache.playerTransform != null ? Cache.playerTransform.position : Vector3.zero);

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

                // Half duration: this is only ever the picker's "in" hop from an
                // already-open overview, the same pacing as SwitchRoutine's second leg
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
         * Frames every flag plus the player in an overview shot.
         * Used to open the picker.
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
         * Wraps Ease() so flight gets cleared back to null once the
         * framing finishes -- without this, Zoom()'s "not mid-flight"
         * guard stays permanently blocked after the first FrameAll.
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
         * Flies from the player's current view out to an overview of
         * every flag in <paramref name="set"/>, then in to
         * <paramref name="target"/>'s saved view -- the GTA-style
         * character-switch shot used by the next/previous shortcuts.
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
