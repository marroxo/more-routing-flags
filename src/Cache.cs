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
