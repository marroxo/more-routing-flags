using System;

using HarmonyLib;
using UILib.Notifications;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Records a new flag whenever the vanilla routing flag
     * gets placed.
     * </summary>
     */
    [HarmonyPatch(typeof(RoutingFlag), "SetRoutingFlagPosition")]
    internal static class FlagPlaced {
        private static Logger logger = new Logger(typeof(FlagPlaced));

        /**
         * <summary>
         * Records the newly placed flag into the current set.
         * </summary>
         * <param name="__instance">The vanilla routing flag</param>
         */
        private static void Postfix(RoutingFlag __instance) {
            try {
                Run(__instance);
            }
            catch (Exception e) {
                logger.LogError($"Failed to record placed flag: {e}");
            }
        }

        /**
         * <summary>
         * Does the actual work of recording the flag.
         *
         * Split out from <see cref="Postfix"/> so the whole body
         * can be wrapped in a single try/catch, patches must
         * never throw.
         * </summary>
         * <param name="instance">The vanilla routing flag</param>
         */
        private static void Run(RoutingFlag instance) {
            if (Config.IsActive() == false
                || Cache.leavePeakScene == null
                || Plugin.currentSet == null
            ) {
                return;
            }

            if (instance.isCustomLevel == true
                && (instance.levelEditorManager == null
                    || instance.levelEditorManager.inPlayMode == false)
            ) {
                return;
            }

            Transform flagTransform = instance.routingFlagTransform;
            Vector3 offset = Cache.leavePeakScene.transform.InverseTransformPoint(
                flagTransform.position
            );
            Vector3 normal = -flagTransform.forward;

            float camX = Cache.playerCamX != null ? Cache.playerCamX.transform.eulerAngles.y : 0f;
            float camY = Cache.playerCamY != null ? Cache.playerCamY.rotationY : 0f;

            // Empty name lets Hud/Picker fall back to their "Flag N/M" index display
            Flag flag = new Flag(offset, normal, camX, camY, "");

            bool replacing = UnityEngine.Input.GetKey(Config.replaceKeybind.Value) == true;

            if (replacing == true
                || Plugin.currentSet.flags.Count >= Config.maxFlags.Value
            ) {
                if (Plugin.currentSet.flags.Count >= Config.maxFlags.Value
                    && replacing == false
                ) {
                    Notifier.Notify(
                        "More Routing Flags",
                        $"Flag limit reached ({Config.maxFlags.Value})",
                        NotificationType.Normal
                    );
                }

                Plugin.currentSet.Replace(flag);
            }
            else {
                Plugin.currentSet.Add(flag);
            }

            FlagStore.Save(Plugin.currentKey, Plugin.currentSet);
            FlagMarkers.Rebuild(Plugin.currentSet);
            Plugin.instance.hud.Show(Plugin.currentSet);
        }
    }
}
