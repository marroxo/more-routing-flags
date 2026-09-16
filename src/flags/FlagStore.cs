using System;
using System.Collections.Generic;
using System.IO;

using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * The wrapper JsonUtility actually serializes.
     *
     * JsonUtility can't serialize a bare list at the top level, so
     * this holds the list and the active index together. Not nested
     * inside FlagStore -- JsonUtility silently drops List&lt;T&gt;
     * fields declared on a privately-nested class (confirmed: the
     * saved JSON only ever contained "active", never "flags").
     * </summary>
     */
    [Serializable]
    internal class FlagFile {
        public List<Flag> flags = new List<Flag>();
        public int active = -1;
    }

    /**
     * <summary>
     * Loads and saves a <see cref="FlagSet"/> to JSON, one file per scene.
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

            FlagFile file = null;

            try {
                file = JsonUtility.FromJson<FlagFile>(File.ReadAllText(path));
            }
            catch (Exception e) {
                logger.LogError($"Failed to load flags for '{key}': {e}");
                return set;
            }

            if (file == null) {
                return set;
            }

            foreach (Flag flag in file.flags) {
                set.Add(flag);
            }

            set.Select(file.active);

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
            FlagFile file = new FlagFile();

            file.flags = set.flags;
            file.active = set.active;

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(file, true));
            }
            catch (Exception e) {
                logger.LogError($"Failed to save flags for '{key}': {e}");
            }
        }
    }
}
