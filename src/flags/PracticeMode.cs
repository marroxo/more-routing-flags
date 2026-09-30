using HarmonyLib;
using UnityEngine;

namespace MoreRoutingFlags {
    internal static class PracticeMode {
        private static RoutingFlag owner;
        private static PlayerMove movement;
        private static readonly System.Reflection.FieldInfo watchField = AccessTools.Field(typeof(RoutingFlag), "pocketWatch");
        private static readonly System.Reflection.FieldInfo summitField = AccessTools.Field(typeof(RoutingFlag), "summitbox");

        internal static bool Available => Config.IsActive() && Cache.routingFlag != null
            && Cache.routingFlag.isActiveAndEnabled && Cache.routingFlag.routingFlagObject != null
            && Cache.routingFlag.routingFlagObject.activeInHierarchy;

        internal static void Enable() {
            if (!Available) return;
            var flag = Cache.routingFlag;
            if (flag == null) return;
            owner = flag;
            movement = Object.FindObjectOfType<PlayerMove>();
            Set(flag, true);
            // Disable scoring immediately, even if vanilla Update already ran this frame.
            var watch = watchField.GetValue(flag) as TimeAttack;
            if (watch != null) watch.disableTimeAttack = true;
            var summit = summitField.GetValue(flag) as StamperPeakSummit;
            if (summit != null) summit.gameObject.SetActive(false);
        }

        internal static void BeforeUpdate(RoutingFlag flag) {
            if (!Config.IsActive()) return;
            if (owner != flag || owner == null) return;
            // A manual toggle is respected until the next picker/teleport action.
            if (!flag.currentlyUsingFlag) { owner = null; return; }
            if (Plugin.currentSet == null || Plugin.currentSet.flags.Count != 0
                || FlyCamera.isActive || Plugin.instance.picker?.isOpen == true
                || movement == null || !movement.IsGrounded()) return;
            Set(flag, false);
            owner = null;
        }

        private static void Set(RoutingFlag flag, bool enabled) {
            flag.currentlyUsingFlag = enabled;
            flag.customLevel_UsedRoutingFlag = enabled;
            var values = flag.usedRoutingFlag;
            if (values == null || flag.currentPeak < 0 || flag.currentPeak >= values.Length) return;
            values[flag.currentPeak] = enabled ? 1 : 0;
            if (flag.isCustomLevel) return;
            var summit = summitField.GetValue(flag) as StamperPeakSummit;
            bool alps = summit != null && (summit.isAlps1 || summit.isAlps2 || summit.isAlps3);
            PlayerPrefsX.SetIntArray(alps ? "UsedRoutingFlag_Alps" : "UsedRoutingFlag", values);
        }
    }
}
