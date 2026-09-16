using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Untags DistanceRenderCam on Solemn Tempest, otherwise it
     * fights with the fly camera. Ported from poy-freecam.
     * </summary>
     */
    internal static class DistanceCamera {
        /**
         * <summary>
         * Applies the fix if the object exists in this scene.
         * </summary>
         */
        internal static void SceneLoad() {
            GameObject obj = GameObject.Find("DistanceRenderCam");
            if (obj == null) {
                return;
            }

            obj.tag = "Untagged";
        }
    }
}
