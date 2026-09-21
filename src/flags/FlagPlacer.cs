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

        private static readonly FieldInfo posXField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_X");
        private static readonly FieldInfo posYField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_Y");
        private static readonly FieldInfo posZField = AccessTools.Field(typeof(RoutingFlag), "flagPositionOnPeak_Z");
        private static readonly FieldInfo rotXField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_X");
        private static readonly FieldInfo rotYField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_Y");
        private static readonly FieldInfo rotZField = AccessTools.Field(typeof(RoutingFlag), "flagRotationOnPeak_Z");

        /**
         * <summary>
         * Validates reflected vanilla fields at startup.
         * </summary>
         */
        internal static bool ValidateFields() {
            bool ok = true;

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
         * Reports a missing reflected field.
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
         * Resolves a saved flag position.
         * </summary>
         * <param name="flag">The flag to resolve</param>
         */
        internal static Vector3 Resolve(Flag flag) {
            Vector3 pos = Cache.leavePeakScene.transform.TransformPoint(flag.offset);

            RaycastHit hit;
            bool didHit = Physics.Raycast(
                pos + flag.normal * 0.3f, -flag.normal, out hit, 0.6f, Cache.terrainMask
            );

            if (didHit == true) {
                pos = hit.point + flag.normal * Cache.wallOffset;
            }

            return pos;
        }

        /**
         * <summary>
         * Applies a saved flag to the vanilla routing flag.
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
         * Sets a vanilla per-peak float value.
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
         * Teleports the player to a saved flag.
         * </summary>
         */
        internal static void Teleport(Flag flag) {
            if (flag == null || Cache.playerTransform == null) {
                return;
            }

            // Match Fast Reset's player-state cleanup.
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

            Vector3 pos = Resolve(flag);
            Quaternion rot = Quaternion.LookRotation(-flag.normal);

            Cache.playerTransform.position = pos
                + (rot * Vector3.up) * Cache.playerUp
                + (rot * Vector3.forward) * Cache.playerOut;

            if (Cache.playerRb != null) {
                Cache.playerRb.velocity = Vector3.zero;

                // Vanilla releases this on the next update.
                if (Cache.routingFlag != null) {
                    Cache.playerRb.isKinematic = true;
                    Cache.routingFlag.usedFlagTeleport = true;
                }
            }

            if (Cache.playerCamX != null) {
                Cache.playerCamX.PlayerGrabbed();
            }

            if (Cache.distanceActivator != null) {
                Cache.distanceActivator.ForceCheck();
            }
        }
    }
}
