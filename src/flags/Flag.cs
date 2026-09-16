using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * A single saved routing flag.
     *
     * `offset` is stored relative to the `LeavePeakScene` anchor so it
     * stays valid across origin shifts and reloads.
     * </summary>
     */
    [System.Serializable]
    public class Flag {
        public Vector3 offset;
        public Vector3 normal;
        public float camX;
        public float camY;
        public string name;

        /**
         * <summary>
         * Constructs an empty flag.
         *
         * IMPORTANT: This should only be used when deserializing.
         * </summary>
         */
        public Flag() {}

        /**
         * <summary>
         * Constructs a flag from its saved values.
         * </summary>
         * <param name="offset">The position, relative to the peak anchor</param>
         * <param name="normal">The surface normal at placement</param>
         * <param name="camX">The saved camera yaw</param>
         * <param name="camY">The saved camera pitch</param>
         * <param name="name">The display name for this flag</param>
         */
        public Flag(Vector3 offset, Vector3 normal, float camX, float camY, string name) {
            this.offset = offset;
            this.normal = normal;
            this.camX = camX;
            this.camY = camY;
            this.name = name;
        }
    }
}
