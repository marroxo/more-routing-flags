using PeterO.Cbor;
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

        /**
         * <summary>
         * Converts this flag to CBOR.
         * </summary>
         */
        internal CBORObject ToCBOR() {
            return CBORObject.NewMap()
                .Add("offset", offset.ToCBOR())
                .Add("normal", normal.ToCBOR())
                .Add("camX", camX)
                .Add("camY", camY)
                .Add("name", name);
        }

        /**
         * <summary>
         * Creates a flag from CBOR.
         * </summary>
         * <param name="cbor">The CBOR map to read from</param>
         */
        internal static Flag FromCBOR(CBORObject cbor) {
            return new Flag(
                cbor["offset"].AsVector3(),
                cbor["normal"].AsVector3(),
                cbor["camX"].AsSingle(),
                cbor["camY"].AsSingle(),
                cbor["name"].AsString()
            );
        }
    }
}
