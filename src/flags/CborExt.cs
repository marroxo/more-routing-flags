using PeterO.Cbor;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * CBOR conversion helpers shared by flag serialization.
     * </summary>
     */
    internal static class CborExt {
        /**
         * <summary>
         * Converts a Vector3 into a CBOR array of its 3 components.
         * </summary>
         * <param name="vec">The Vector3 to convert</param>
         */
        internal static CBORObject ToCBOR(this Vector3 vec) {
            return CBORObject.NewArray().Add(vec.x).Add(vec.y).Add(vec.z);
        }

        /**
         * <summary>
         * Reads a Vector3 from a CBOR array of 3 components.
         * </summary>
         * <param name="cbor">The CBOR array to read from</param>
         */
        internal static Vector3 AsVector3(this CBORObject cbor) {
            return new Vector3(
                cbor[0].AsSingle(),
                cbor[1].AsSingle(),
                cbor[2].AsSingle()
            );
        }
    }
}
