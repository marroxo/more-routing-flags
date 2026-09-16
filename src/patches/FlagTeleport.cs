using System.Reflection;

using HarmonyLib;
using Rewired;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Restores the active flag's saved camera rotation right before
     * the vanilla "Move To Routing Flag" teleport runs.
     *
     * The guard list below is copied from Kaden5480's
     * poy-better-routing-flag, not reinvented. This mod supersedes
     * it, running both at the same time is harmless.
     * </summary>
     */
    [HarmonyPatch(typeof(RoutingFlag), "Update")]
    internal static class FlagTeleport {
        private static Logger logger = new Logger(typeof(FlagTeleport));

        private static readonly FieldInfo playerField = AccessTools.Field(typeof(RoutingFlag), "player");
        private static readonly FieldInfo ropeAnchorField = AccessTools.Field(typeof(RoutingFlag), "ropeanchor");

        /**
         * <summary>
         * Checks the two reflected fields this patch needs.
         * Run once from Plugin.Awake.
         * </summary>
         */
        internal static bool ValidateFields() {
            bool ok = true;

            if (playerField == null) {
                logger.LogError("Could not find vanilla field 'RoutingFlag.player'.");
                ok = false;
            }

            if (ropeAnchorField == null) {
                logger.LogError("Could not find vanilla field 'RoutingFlag.ropeanchor'.");
                ok = false;
            }

            return ok;
        }

        /**
         * <summary>
         * Restores the saved camera rotation for the active flag.
         * </summary>
         * <param name="__instance">The vanilla routing flag</param>
         */
        private static void Prefix(RoutingFlag __instance) {
            try {
                Run(__instance);
            }
            catch (System.Exception e) {
                logger.LogError($"Failed to restore camera on teleport: {e}");
            }
        }

        /**
         * <summary>
         * Does the actual restore. Split out so the whole body can be
         * wrapped in one try/catch, a Harmony prefix must never throw,
         * it would break vanilla's own Update.
         * </summary>
         * <param name="instance">The vanilla routing flag</param>
         */
        private static void Run(RoutingFlag instance) {
            if (Config.enabled.Value == false || Plugin.currentSet == null) {
                return;
            }

            Flag flag = Plugin.currentSet.Active();
            if (flag == null) {
                return;
            }

            if (CanTeleport(instance) == false) {
                return;
            }

            if (Cache.playerCamX != null) {
                Vector3 euler = Cache.playerCamX.transform.eulerAngles;
                euler.y = flag.camX;
                Cache.playerCamX.transform.eulerAngles = euler;
            }

            if (Cache.playerCamY != null) {
                Cache.playerCamY.rotationY = flag.camY;
            }
        }

        /**
         * <summary>
         * Checks every guard the vanilla teleport key itself checks,
         * so this only fires the same frame vanilla would teleport.
         * </summary>
         * <param name="instance">The vanilla routing flag</param>
         */
        private static bool CanTeleport(RoutingFlag instance) {
            if (instance.currentlyUsingFlag == false
                || InGameMenu.isCurrentlyNavigationMenu == true
                || EnterPeakScene.enteringPeakScene == true
                || ResetPosition.resettingPosition == true
            ) {
                return false;
            }

            Player player = (Player) playerField.GetValue(instance);
            RopeAnchor ropeAnchor = (RopeAnchor) ropeAnchorField.GetValue(instance);

            if (player.GetButtonDown("Move To Routing Flag") == false
                || ropeAnchor.attached == true
                || Crampons.cramponsActivated == true
                || Bivouac.currentlyUsingBivouac == true
                || instance.usedFlagTeleport == true
                || instance.flagPositionOnPeak_X[instance.currentPeak] == 0
                || instance.flagPositionOnPeak_Y[instance.currentPeak] == 0
                || instance.flagPositionOnPeak_Z[instance.currentPeak] == 0
            ) {
                return false;
            }

            return true;
        }
    }
}
