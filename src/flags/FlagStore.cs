using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

using BepInEx;
using PeterO.Cbor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Stores one compressed CBOR flag set per scene.
     * </summary>
     */
    internal static class FlagStore {
        private const string modGuid = "com.github.marroxo.more-routing-flags";

        // Gzip only larger saves.
        private const int byteCompressionLimit = 256;

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
            return Path.Combine(Paths.ConfigPath, modGuid, $"{key}.dat");
        }

        /**
         * <summary>
         * Builds a legacy JSON path.
         * </summary>
         * <param name="key">The storage key</param>
         */
        private static string LegacyPathFor(string key) {
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
            string path = PathFor(key);

            if (File.Exists(path) == true) {
                return LoadCbor(key, path);
            }

            string legacyPath = LegacyPathFor(key);

            if (File.Exists(legacyPath) == true) {
                return LoadLegacyJson(key, legacyPath);
            }

            return new FlagSet();
        }

        /**
         * <summary>
         * Loads a CBOR flag set.
         * </summary>
         * <param name="key">The storage key being loaded, for logging</param>
         * <param name="path">The CBOR file's path</param>
         */
        private static FlagSet LoadCbor(string key, string path) {
            FlagSet set = new FlagSet();

            try {
                CBORObject root = CBORObject.DecodeFromBytes(
                    Decompress(File.ReadAllBytes(path))
                );

                CBORObject flags = root["flags"];

                for (int i = 0; i < flags.Count; i++) {
                    set.Add(Flag.FromCBOR(flags[i]));
                }

                set.Select(root["active"].AsInt32());
            }
            catch (Exception e) {
                logger.LogError($"Failed to load flags for '{key}': {e}");
                return new FlagSet();
            }

            logger.LogInfo($"Load('{key}'): {set.flags.Count} flags, active={set.active}");
            return set;
        }

        /**
         * <summary>
         * Loads and migrates a legacy JSON flag set.
         * </summary>
         * <param name="key">The storage key being loaded, for logging</param>
         * <param name="path">The legacy JSON file's path</param>
         */
        private static FlagSet LoadLegacyJson(string key, string path) {
            FlagSet set = new FlagSet();
            string[] lines;

            try {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) {
                logger.LogError($"Failed to load legacy flags for '{key}': {e}");
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
                    logger.LogError($"Failed to parse a legacy flag line for '{key}': {e}");
                }
            }

            set.Select(active);
            logger.LogInfo($"LoadLegacyJson('{key}'): {set.flags.Count} flags, active={set.active}");

            // Migrate without deleting the backup.
            Save(key, set);

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

            CBORObject flags = CBORObject.NewArray();
            foreach (Flag flag in set.flags) {
                flags.Add(flag.ToCBOR());
            }

            CBORObject root = CBORObject.NewMap()
                .Add("active", set.active)
                .Add("flags", flags);

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Compress(root.EncodeToBytes()));
                logger.LogInfo($"Save('{key}'): {set.flags.Count} flags, active={set.active}");
            }
            catch (Exception e) {
                logger.LogError($"Failed to save flags for '{key}': {e}");
            }
        }

        /**
         * <summary>
         * Compresses large byte arrays with gzip.
         * </summary>
         * <param name="bytes">The bytes to compress</param>
         */
        private static byte[] Compress(byte[] bytes) {
            if (bytes.Length < byteCompressionLimit) {
                return bytes;
            }

            using (MemoryStream stream = new MemoryStream()) {
            using (GZipStream gzip = new GZipStream(stream, CompressionMode.Compress)) {
                gzip.Write(bytes, 0, bytes.Length);
                gzip.Close();

                return stream.ToArray();
            }}
        }

        /**
         * <summary>
         * Decompresses gzip data or returns raw bytes.
         * </summary>
         * <param name="bytes">The bytes to decompress</param>
         */
        private static byte[] Decompress(byte[] bytes) {
            try {
                using (MemoryStream compressed = new MemoryStream(bytes)) {
                using (GZipStream gzip = new GZipStream(compressed, CompressionMode.Decompress)) {
                using (MemoryStream decompressed = new MemoryStream()) {
                    gzip.CopyTo(decompressed);
                    return decompressed.ToArray();
                }}}
            }
            catch (IOException) {
                return bytes;
            }
        }
    }
}
