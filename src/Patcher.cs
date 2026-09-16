using HarmonyLib;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * A small wrapper around Harmony for applying patches
     * with a single shared instance.
     * </summary>
     */
    internal static class Patcher {
        private const string harmonyId = "com.github.marroxo.more-routing-flags";
        private static Harmony harmony = new Harmony(harmonyId);
        private static Logger logger = new Logger(typeof(Patcher));

        /**
         * <summary>
         * Applies every patch declared on the given type.
         *
         * Harmony throws if the target method can't be found, rather
         * than skipping it quietly, so this needs its own try/catch.
         * Left unpatched, a bad target name would otherwise surface
         * as a mysterious missing feature instead of a log line.
         * </summary>
         * <param name="type">The patch class to apply</param>
         */
        internal static void Patch(System.Type type) {
            try {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e) {
                logger.LogError($"Failed to apply patch '{type.Name}': {e}");
            }
        }

        /**
         * <summary>
         * Removes every patch applied by this mod.
         * </summary>
         */
        internal static void UnpatchAll() {
            harmony.UnpatchSelf();
        }
    }
}
