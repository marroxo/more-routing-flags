using System;
using System.Globalization;
using System.IO;

using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Loads and saves a <see cref="FlagSet"/>, one file per scene.
     *
     * Not a single JsonUtility.ToJson(FlagFile) call: JsonUtility
     * silently drops a List&lt;T&gt; field on the wrapper object no
     * matter where that wrapper class is declared (confirmed live --
     * logging the exact string passed to File.WriteAllText showed
     * only {"active": N}, flags missing, even from a top-level class).
     * Each flag is JsonUtility-serialized on its own instead (a single
     * plain object, the case JsonUtility actually handles reliably),
     * one per line, with the active index as the first line.
     * </summary>
     */
    internal static class FlagStore {
        private const string modGuid = "com.github.marroxo.more-routing-flags";
        private static Logger logger = new Logger(typeof(FlagStore));

        /**
         * <summary>
         * Builds the storage key for a scene.
         * </summary>
         * <param name="scene">The active scene</param>
         * <param name="isCustomLevel">Whether the active scene is a custom level</param>
         * <param name="peakName">The custom level's name, if applicable</param>
         */
        internal static string KeyFor(Scene scene, bool isCustomLevel, string peakName) {
            if (isCustomLevel == true) {
                return $"custom/{peakName}";
            }

            return scene.name;
        }

        /**
         * <summary>
         * Builds the file path for a storage key.
         * </summary>
         * <param name="key">The storage key</param>
         */
        private static string PathFor(string key) {
            return Path.Combine(Paths.ConfigPath, modGuid, $"{key}.json");
        }

        /**
         * <summary>
         * Loads the flag set for a storage key.
         *
         * Returns an empty set if no file exists yet, or if the
         * file failed to load.
         * </summary>
         * <param name="key">The storage key to load</param>
         */
        internal static FlagSet Load(string key) {
            FlagSet set = new FlagSet();
            string path = PathFor(key);

            if (File.Exists(path) == false) {
                return set;
            }

            string[] lines;

            try {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) {
                logger.LogError($"Failed to load flags for '{key}': {e}");
                return set;
            }

            if (lines.Length == 0) {
                return set;
            }

            int active;
            int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out active);

            for (int i = 1; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i]) == true) {
                    continue;
                }

                try {
                    Flag flag = JsonUtility.FromJson<Flag>(lines[i]);
                    if (flag != null) {
                        set.Add(flag);
                    }
                }
                catch (Exception e) {
                    logger.LogError($"Failed to parse a flag line for '{key}': {e}");
                }
            }

            set.Select(active);
            logger.LogInfo($"Load('{key}'): {set.flags.Count} flags, active={set.active}");

            return set;
        }

        /**
         * <summary>
         * Saves a flag set for a storage key.
         * </summary>
         * <param name="key">The storage key to save under</param>
         * <param name="set">The flag set to save</param>
         */
        internal static void Save(string key, FlagSet set) {
            string path = PathFor(key);

            string[] lines = new string[set.flags.Count + 1];
            lines[0] = set.active.ToString(CultureInfo.InvariantCulture);

            for (int i = 0; i < set.flags.Count; i++) {
                lines[i + 1] = JsonUtility.ToJson(set.flags[i]);
            }

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
                logger.LogInfo($"Save('{key}'): {set.flags.Count} flags, active={set.active}");
            }
            catch (Exception e) {
                logger.LogError($"Failed to save flags for '{key}': {e}");
            }
        }
    }
}
