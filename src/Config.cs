using BepInEx.Configuration;
using ModMenu.Config;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Holds the config for this mod.
     * </summary>
     */
    internal static class Config {
        // General
        [Field("Enabled")]
        internal static ConfigEntry<bool> enabled;

        // Not FieldType.Slider: Mod Menu's slider unboxes as float, throws on an int ConfigEntry
        [Field("Max Flags")]
        internal static ConfigEntry<int> maxFlags;

        [Field("Show Inactive Flags")]
        internal static ConfigEntry<bool> showInactive;

        // Camera
        [Field("Fly Duration", FieldType.Slider, min=0f, max=3f)]
        internal static ConfigEntry<float> flyDuration;

        [Field("Fly Pull Back", FieldType.Slider, min=0f, max=20f)]
        internal static ConfigEntry<float> flyPullBack;

        // Keybinds
        [Field("Next Flag")]
        internal static ConfigEntry<KeyCode> nextKeybind;

        [Field("Previous Flag")]
        internal static ConfigEntry<KeyCode> previousKeybind;

        [Field("Delete Flag")]
        internal static ConfigEntry<KeyCode> deleteKeybind;

        [Field("Open Picker")]
        internal static ConfigEntry<KeyCode> pickerKeybind;

        [Field("Replace Active Flag (hold while placing)")]
        internal static ConfigEntry<KeyCode> replaceKeybind;

        // UI
        [Field("Show Hud")]
        internal static ConfigEntry<bool> showHud;

        [Field("Hud Wait Time", FieldType.Slider, min=0f, max=8f)]
        internal static ConfigEntry<float> hudWaitTime;

        /**
         * <summary>
         * Initializes the config by binding to the
         * provided `ConfigFile`.
         * </summary>
         * <param name="configFile">The config file to bind to</param>
         */
        internal static void Init(ConfigFile configFile) {
            // General
            enabled = configFile.Bind(
                "General", "enabled", true,
                "Whether this mod is enabled."
            );

            maxFlags = configFile.Bind(
                "General", "maxFlags", 20,
                "The maximum number of flags allowed per peak."
            );

            showInactive = configFile.Bind(
                "General", "showInactive", true,
                "Whether inactive flags should be shown as dimmed markers."
            );

            // Camera
            flyDuration = configFile.Bind(
                "Camera", "flyDuration", 2f,
                "How long the camera takes to fly between flags, in seconds."
                + " Set to 0 to disable the flight entirely."
            );

            flyPullBack = configFile.Bind(
                "Camera", "flyPullBack", 6f,
                "How far the camera pulls back before flying to a flag."
            );

            // Keybinds
            nextKeybind = configFile.Bind(
                "Keybinds", "nextKeybind", KeyCode.RightBracket,
                "The keybind to switch to the next flag."
            );

            previousKeybind = configFile.Bind(
                "Keybinds", "previousKeybind", KeyCode.LeftBracket,
                "The keybind to switch to the previous flag."
            );

            deleteKeybind = configFile.Bind(
                "Keybinds", "deleteKeybind", KeyCode.Delete,
                "The keybind to delete the active flag."
            );

            pickerKeybind = configFile.Bind(
                "Keybinds", "pickerKeybind", KeyCode.LeftAlt,
                "The keybind to open the flag picker."
            );

            replaceKeybind = configFile.Bind(
                "Keybinds", "replaceKeybind", KeyCode.LeftControl,
                "Hold this while placing a flag to overwrite the active flag"
                + " instead of adding a new one."
            );

            // UI
            showHud = configFile.Bind(
                "UI", "showHud", true,
                "Whether the flag count hud should be shown."
            );

            hudWaitTime = configFile.Bind(
                "UI", "hudWaitTime", 2f,
                "How long the hud stays visible after a change, in seconds."
            );
        }
    }
}
