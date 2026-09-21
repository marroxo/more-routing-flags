using System.Reflection;

using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

using UECamera = UnityEngine.Camera;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Contains scene objects needed by this mod.
     * </summary>
     */
    internal static class Cache {
        // Defaults for maps without a vanilla routing flag.
        private const float fallbackPlayerUp = 0.2f;
        private const float fallbackPlayerOut = 0.12f;
        private const float fallbackWallOffset = 0.05f;

        private static readonly FieldInfo maskField = AccessTools.Field(typeof(RoutingFlag), "mask");
        private static readonly FieldInfo wallOffsetField = AccessTools.Field(typeof(RoutingFlag), "wallOffset");

        internal static RoutingFlag routingFlag           { get; private set; }
        internal static LeavePeakScene leavePeakScene      { get; private set; }
        internal static UECamera playerCamera              { get; private set; }
        internal static CameraLook playerCamX               { get; private set; }
        internal static CameraLook playerCamY               { get; private set; }
        internal static Rigidbody playerRb                 { get; private set; }
        internal static Transform playerTransform           { get; private set; }
        internal static Climbing climbing                  { get; private set; }
        internal static IceAxe iceAxes                     { get; private set; }
        internal static FallingEvent fallingEvent           { get; private set; }
        internal static DistanceActivator distanceActivator { get; private set; }
        internal static Scene scene                        { get; private set; }

        private static Logger logger = new Logger(typeof(Cache));

        /**
         * <summary>
         * Validates reflected vanilla fields at startup.
         * </summary>
         */
        internal static bool ValidateFields() {
            bool ok = true;

            if (maskField == null) {
                logger.LogError("Could not find vanilla field 'RoutingFlag.mask'.");
                ok = false;
            }

            if (wallOffsetField == null) {
                logger.LogError("Could not find vanilla field 'RoutingFlag.wallOffset'.");
                ok = false;
            }

            return ok;
        }

        /**
         * <summary>
         * Gets the vanilla terrain mask or a safe fallback.
         * </summary>
         */
        internal static int terrainMask {
            get {
                if (routingFlag == null || maskField == null) {
                    return Physics.DefaultRaycastLayers;
                }

                return ((LayerMask) maskField.GetValue(routingFlag)).value;
            }
        }

        /**
         * <summary>
         * Gets the vertical teleport offset.
         * </summary>
         */
        internal static float playerUp => routingFlag != null ? routingFlag.playerUp : fallbackPlayerUp;

        /**
         * <summary>
         * Gets the forward teleport offset.
         * </summary>
         */
        internal static float playerOut => routingFlag != null ? routingFlag.playerOut : fallbackPlayerOut;

        /**
         * <summary>
         * Gets the surface offset for a flag.
         * </summary>
         */
        internal static float wallOffset {
            get {
                if (routingFlag == null || wallOffsetField == null) {
                    return fallbackWallOffset;
                }

                return (float) wallOffsetField.GetValue(routingFlag);
            }
        }

        /**
         * <summary>
         * Finds objects in the scene.
         * </summary>
         */
        internal static void FindObjects() {
            scene = SceneManager.GetActiveScene();

            climbing = GameObject.FindObjectOfType<Climbing>();
            iceAxes = GameObject.FindObjectOfType<IceAxe>();
            fallingEvent = GameObject.FindObjectOfType<FallingEvent>();
            routingFlag = GameObject.FindObjectOfType<RoutingFlag>();

            // Only present on Solemn Tempest
            distanceActivator = GameObject.FindObjectOfType<DistanceActivator>();

            LeavePeakScene[] bags = Resources.FindObjectsOfTypeAll<LeavePeakScene>();
            if (bags.Length > 0) {
                leavePeakScene = bags[0];
            }

            if (climbing != null) {
                playerRb = climbing.playerBody;
            }

            if (playerRb != null) {
                playerTransform = playerRb.transform;
            }

            // Access the player's camera
            GameObject cameraHolderObj = GameObject.Find("PlayerCameraHolder");
            if (cameraHolderObj == null) {
                Plugin.LogDebug("No camera holder found");
                return;
            }

            // The camera has two components, X and Y
            foreach (CameraLook cameraLook in cameraHolderObj.GetComponentsInChildren<CameraLook>()) {
                if ("PlayerCameraHolder".Equals(cameraLook.name) == true) {
                    playerCamX = cameraLook;
                }
                else {
                    playerCamY = cameraLook;
                }
            }

            // Find the main camera through an audio listener
            foreach (AudioListener listener in GameObject.FindObjectsOfType<AudioListener>()) {
                if ("CamY".Equals(listener.name) == false
                    && "MainCamera".Equals(listener.name) == false
                ) {
                    continue;
                }

                playerCamera = listener.GetComponent<UECamera>();
                break;
            }
        }

        /**
         * <summary>
         * Clears the cache.
         * </summary>
         */
        internal static void Clear() {
            routingFlag = null;
            leavePeakScene = null;
            playerCamera = null;
            playerCamX = null;
            playerCamY = null;
            playerRb = null;
            playerTransform = null;
            climbing = null;
            iceAxes = null;
            fallingEvent = null;
            distanceActivator = null;
        }
    }
}
