using System.Collections.Generic;

using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Marks a marker's root with the flag it represents, so a
     * raycast hit against it (or any of its children) can be
     * traced back to a specific flag.
     * </summary>
     */
    internal class FlagMarkerRef : MonoBehaviour {
        internal Flag flag;
    }

    /**
     * <summary>
     * Shows clones of the vanilla flag object for every inactive
     * flag in the current set.
     * </summary>
     */
    internal static class FlagMarkers {
        private static List<GameObject> markers = new List<GameObject>();
        private static bool pickable;

        /**
         * <summary>
         * Destroys the current markers and rebuilds one per
         * inactive flag in the given set.
         * </summary>
         * <param name="set">The flag set to build markers for</param>
         */
        internal static void Rebuild(FlagSet set) {
            Clear();

            if (Cache.routingFlag == null) {
                return;
            }

            for (int i = 0; i < set.flags.Count; i++) {
                if (i == set.active) {
                    continue;
                }

                markers.Add(BuildMarker(set.flags[i]));
            }
        }

        /**
         * <summary>
         * Enables or disables click detection on every current and
         * future marker, without affecting whether they're visible.
         * Colliders stay off outside the picker so markers don't
         * interfere with normal player movement/collision.
         * </summary>
         * <param name="value">Whether markers should be clickable</param>
         */
        internal static void SetPickable(bool value) {
            pickable = value;

            foreach (GameObject marker in markers) {
                if (marker == null) {
                    continue;
                }

                foreach (Collider collider in marker.GetComponentsInChildren<Collider>(true)) {
                    collider.enabled = value;
                }
            }
        }

        /**
         * <summary>
         * Builds a single marker for a flag.
         * </summary>
         * <param name="flag">The flag to build a marker for</param>
         */
        private static GameObject BuildMarker(Flag flag) {
            // routingFlagTransform is the real flag vanilla moves, routingFlagObject is just its anchor
            GameObject marker = GameObject.Instantiate(Cache.routingFlag.routingFlagTransform.gameObject);
            marker.name = "MoreRoutingFlags Marker";
            marker.tag = "Untagged";
            marker.SetActive(true);
            marker.AddComponent<FlagMarkerRef>().flag = flag;

            marker.transform.position = FlagPlacer.Resolve(flag);
            marker.transform.forward = -flag.normal;

            foreach (Collider collider in marker.GetComponentsInChildren<Collider>()) {
                collider.enabled = pickable;
            }

            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true)) {
                renderer.enabled = true;

                // Vanilla assigns this at runtime via a script this static clone never runs
                if (Cache.routingFlag.routingFlagMat != null) {
                    renderer.sharedMaterial = Cache.routingFlag.routingFlagMat;
                }
            }

            return marker;
        }

        /**
         * <summary>
         * Destroys every current marker.
         * </summary>
         */
        internal static void Clear() {
            foreach (GameObject marker in markers) {
                if (marker != null) {
                    GameObject.Destroy(marker);
                }
            }

            markers.Clear();
        }
    }
}
