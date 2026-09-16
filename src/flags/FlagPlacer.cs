using System.Reflection;

using HarmonyLib;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Resolves saved flags back onto the world, and drives the
     * vanilla routing flag from whichever one is active.
     * </summary>
     */
    internal static class FlagPlacer {
        private static Logger logger = new Logger(typeof(FlagPlacer));

        private static readonly FieldInfo maskField = AccessTools.Field(typeof(RoutingFlag), "mask");
        private static readonly FieldInfo wallOffsetField = AccessTools.Field(typeof(RoutingFlag), "wallOffset");
        private static readonly FieldInfo posXField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_X");
        private static readonly FieldInfo posYField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_Y");
        private static readonly FieldInfo posZField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_Z");
        private static readonly FieldInfo rotXField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_X");
        private static readonly FieldInfo rotYField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_Y");
        private static readonly FieldInfo rotZField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_Z");

        /**
         * <summary>
         * Checks every vanilla field this class reflects into.
         *
         * Run once from Plugin.Awake so a bad field name fails loud
         * at startup instead of a silent NullReferenceException deep
         * in a Harmony patch the first time someone places a flag.
         * </summary>
         */
        internal static bool ValidateFields() {
            bool ok = true;

            ok &= CheckField(maskField, "RoutingFlag.mask");
            ok &= CheckField(wallOffsetField, "RoutingFlag.wallOffset");
            ok &= CheckField(posXField, "RoutingFlag.flagPositionOnPeak_X");
            ok &= CheckField(posYField, "RoutingFlag.flagPositionOnPeak_Y");
            ok &= CheckField(posZField, "RoutingFlag.flagPositionOnPeak_Z");
            ok &= CheckField(rotXField, "RoutingFlag.flagRotationOnPeak_X");
            ok &= CheckField(rotYField, "RoutingFlag.flagRotationOnPeak_Y");
            ok &= CheckField(rotZField, "RoutingFlag.flagRotationOnPeak_Z");

            return ok;
        }

        /**
         * <summary>
         * Logs a clear error if a reflected field failed to resolve.
         * </summary>
         * <param name="field">The field to check</param>
         * <param name="name">The vanilla name to report if missing</param>
         */
        private static bool CheckField(FieldInfo field, string name) {
            if (field == null) {
                logger.LogError(
                    $"Could not find vanilla field '{name}', it may have"
                    + " been renamed. See docs/windows-build-todos.md."
                );
                return false;
            }

            return true;
        }

        /**
         * <summary>
         * Resolves a flag's saved offset back to a world position,
         * snapping it to the nearest surface.
         * </summary>
         * <param name="flag">The flag to resolve</param>
         */
        internal static Vector3 Resolve(Flag flag) {
            Vector3 pos = Cache.leavePeakScene.transform.TransformPoint(flag.offset);

            if (Cache.routingFlag == null) {
                return pos;
            }

            int mask = ((LayerMask) maskField.GetValue(Cache.routingFlag)).value;
            float wallOffset = (float) wallOffsetField.GetValue(Cache.routingFlag);

            RaycastHit hit;
            bool didHit = Physics.Raycast(
                pos + flag.normal * 0.3f, -flag.normal, out hit, 0.6f, mask
            );

            if (didHit == true) {
                pos = hit.point + flag.normal * wallOffset;
            }

            return pos;
        }

        /**
         * <summary>
         * Applies a flag to the vanilla routing flag, so the vanilla
         * "Move To Routing Flag" key and PlayerPrefs restore keep working.
         * </summary>
         * <param name="flag">The flag to apply</param>
         */
        internal static void Apply(Flag flag) {
            if (Cache.routingFlag == null || flag == null) {
                return;
            }

            Vector3 pos = Resolve(flag);
            Transform flagTransform = Cache.routingFlag.routingFlagTransform;

            flagTransform.position = pos;
            flagTransform.forward = -flag.normal;

            int peak = Cache.routingFlag.currentPeak;

            SetElement(posXField, peak, pos.x);
            SetElement(posYField, peak, pos.y);
            SetElement(posZField, peak, pos.z);

            Vector3 euler = flagTransform.eulerAngles;
            SetElement(rotXField, peak, euler.x);
            SetElement(rotYField, peak, euler.y);
            SetElement(rotZField, peak, euler.z);
        }

        /**
         * <summary>
         * Sets a single element of one of the vanilla per-peak
         * float array fields.
         * </summary>
         * <param name="field">The array field to write into</param>
         * <param name="index">The index to write</param>
         * <param name="value">The value to write</param>
         */
        private static void SetElement(FieldInfo field, int index, float value) {
            float[] array = (float[]) field.GetValue(Cache.routingFlag);

            if (array == null || index < 0 || index >= array.Length) {
                return;
            }

            array[index] = value;
        }

        /**
         * <summary>
         * Replicates the vanilla routing flag teleport.
         *
         * Used only by the switch/picker path, the vanilla key still
         * runs vanilla code.
         * </summary>
         */
        internal static void Teleport() {
            if (Cache.routingFlag == null || Cache.playerTransform == null) {
                return;
            }

            Transform flagTransform = Cache.routingFlag.routingFlagTransform;

            // Matches Kaden5480's FastReset.State.PlayerState.MoveTo
            if (Cache.climbing != null) {
                Cache.climbing.ReleaseResetBoth();
            }

            if (Cache.iceAxes != null) {
                Cache.iceAxes.ReleaseLeft(false);
                Cache.iceAxes.ReleaseRight(false);
            }

            if (Cache.fallingEvent != null) {
                Cache.fallingEvent.fellShortDistance = false;
                Cache.fallingEvent.fellLongDistance = false;
                Cache.fallingEvent.fellToDeath = false;
            }

            // Same offsets the vanilla teleport itself uses.
            float playerUp = Cache.routingFlag.playerUp;
            float playerOut = Cache.routingFlag.playerOut;

            Cache.playerTransform.position = flagTransform.position
                + flagTransform.up * playerUp
                + flagTransform.forward * playerOut;

            if (Cache.playerRb != null) {
                Cache.playerRb.velocity = Vector3.zero;
                Cache.playerRb.isKinematic = true;
            }

            if (Cache.playerCamX != null) {
                Cache.playerCamX.PlayerGrabbed();
            }

            Cache.routingFlag.usedFlagTeleport = true;

            if (Cache.distanceActivator != null) {
                Cache.distanceActivator.ForceCheck();
            }
        }
    }
}
