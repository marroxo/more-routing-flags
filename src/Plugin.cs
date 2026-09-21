using System;
using System.Linq;

using BepInEx;
using HarmonyLib;
using ModMenu;
using UILib;
using UILib.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreRoutingFlags {
    [BepInDependency("com.github.Kaden5480.poy-ui-lib")]
    [BepInDependency(
        "com.github.Kaden5480.poy-mod-menu",
        BepInDependency.DependencyFlags.SoftDependency
    )]
    [BepInPlugin("com.github.marroxo.more-routing-flags", "More Routing Flags", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        internal static Plugin instance { get; private set; }
        internal Hud hud { get; private set; }
        internal Picker picker { get; private set; }

        internal static FlagSet currentSet;
        internal static string currentKey;

        // Hold-to-delete timings.
        private const float deleteHoldDelay = 0.4f;
        private const float deleteHoldInterval = 0.1f;
        private const float deleteHoldSyncInterval = 0.25f;
        private float deleteHoldTimer;
        private float deleteHoldSyncTimer;
        private bool deleteHoldPending;

        /**
         * <summary>
         * Executes when the plugin is being loaded.
         * </summary>
         */
        private void Awake() {
            instance = this;

            MoreRoutingFlags.Config.Init(this.Config);

            Patcher.Patch(typeof(FlagPlaced));
            Patcher.Patch(typeof(FlagTeleport));

            // Fail early on renamed vanilla fields.
            bool fieldsOk = FlagPlacer.ValidateFields()
                & FlagTeleport.ValidateFields()
                & Cache.ValidateFields();

            if (fieldsOk == true) {
                LogInfo(
                    $"MoreRoutingFlags v{PluginInfo.PLUGIN_VERSION} loaded cleanly"
                    + $" (build {PluginInfo.PLUGIN_BUILD_TIME})"
                );
            }
            else {
                LogError(
                    "MoreRoutingFlags loaded with missing vanilla references,"
                    + " see the errors above and docs/windows-build-todos.md"
                );
            }

            new FlyCamera();

            UIRoot.onInit.AddListener(() => {
                hud = new Hud();
                picker = new Picker();
                RegisterShortcuts();
            });

            SceneLoads.AddLoadListener(delegate {
                Cache.FindObjects();
                DistanceCamera.SceneLoad();

                bool isCustomLevel = CustomLevelManager.control != null && CustomLevelManager.control.InCustomStages == true;
                string peakName = isCustomLevel == true ? CustomLevelManager.control.peakName : null;
                currentKey = FlagStore.KeyFor(Cache.scene, isCustomLevel, peakName);
                currentSet = FlagStore.Load(currentKey);

                Flag active = currentSet.Active();
                if (active != null) {
                    FlagPlacer.Apply(active);

                    if (hud != null) {
                        hud.Show(currentSet);
                    }
                }

                FlagMarkers.Rebuild(currentSet);
            });

            SceneLoads.AddUnloadListener(delegate {
                picker?.Close();
                FlyCamera.Release();
                FlagMarkers.Clear();
                Cache.Clear();
            });

            // Register with Mod Menu as an optional dependency
            if (AccessTools.AllAssemblies().FirstOrDefault(
                    a => a.GetName().Name == "ModMenu"
                ) != null
            ) {
                Register();
            }
        }

        /**
         * <summary>
         * Registers with Mod Menu.
         * </summary>
         */
        private void Register() {
            ModInfo info = ModManager.Register(this);
            info.Add(typeof(MoreRoutingFlags.Config));

            // Show the current build time.
            info.description = $"Build: {PluginInfo.PLUGIN_BUILD_TIME}";
        }

        /**
         * <summary>
         * Registers live-updating shortcuts.
         * </summary>
         */
        private void RegisterShortcuts() {
            Shortcut next = new Shortcut(new[] { MoreRoutingFlags.Config.nextKeybind });
            next.onTrigger.AddListener(SwitchNext);
            UIRoot.AddShortcut(next);

            Shortcut previous = new Shortcut(new[] { MoreRoutingFlags.Config.previousKeybind });
            previous.onTrigger.AddListener(SwitchPrevious);
            UIRoot.AddShortcut(previous);

            Shortcut delete = new Shortcut(new[] { MoreRoutingFlags.Config.deleteKeybind });
            delete.onTrigger.AddListener(DeleteActive);
            UIRoot.AddShortcut(delete);

            Shortcut openPicker = new Shortcut(new[] { MoreRoutingFlags.Config.pickerKeybind });
            openPicker.onTrigger.AddListener(TogglePicker);
            UIRoot.AddShortcut(openPicker);
        }

        /**
         * <summary>
         * Switches to the next flag and flies there.
         * </summary>
         */
        private void SwitchNext() {
            if (MoreRoutingFlags.Config.IsActive() == false || currentSet == null || currentSet.flags.Count == 0) {
                return;
            }

            currentSet.Next();
            hud.Show(currentSet);
            SwitchTo(currentSet.Active());
        }

        /**
         * <summary>
         * Switches to the previous flag and flies there.
         * </summary>
         */
        private void SwitchPrevious() {
            if (MoreRoutingFlags.Config.IsActive() == false || currentSet == null || currentSet.flags.Count == 0) {
                return;
            }

            currentSet.Previous();
            hud.Show(currentSet);
            SwitchTo(currentSet.Active());
        }

        /**
         * <summary>
         * Flies to a flag and applies it as the vanilla active flag.
         * </summary>
         * <param name="flag">The flag to switch to</param>
         */
        private void SwitchTo(Flag flag) {
            if (flag == null) {
                return;
            }

            FlyCamera.SwitchTo(currentSet, flag, () => {
                FlagPlacer.Apply(flag);
                FlagPlacer.Teleport(flag);
                FlyCamera.Release();
                FlagMarkers.Rebuild(currentSet);
                hud.Show(currentSet);
            });
        }

        /**
         * <summary>
         * Deletes the aimed or active flag.
         * </summary>
         */
        private void DeleteActive() {
            if (RemoveActiveFlag() == true) {
                SyncAfterDelete();
            }
        }

        /**
         * <summary>
         * Removes a flag without refreshing UI or storage.
         * </summary>
         * <returns>Whether a flag was actually removed</returns>
         */
        private bool RemoveActiveFlag() {
            if (MoreRoutingFlags.Config.IsActive() == false || currentSet == null || currentSet.flags.Count == 0) {
                return false;
            }

            // The player camera is frozen while the picker is open.
            if (picker.isOpen == false) {
                int aimed = FindAimedFlagIndex();
                if (aimed != -1) {
                    currentSet.Select(aimed);
                }
            }

            if (currentSet.active == -1) {
                return false;
            }

            currentSet.Remove();
            return true;
        }

        /**
         * <summary>
         * Saves and refreshes a changed flag set.
         * </summary>
         */
        private void SyncAfterDelete() {
            FlagStore.Save(currentKey, currentSet);

            Flag active = currentSet.Active();
            if (active != null) {
                FlagPlacer.Apply(active);
            }
            else if (Cache.routingFlag != null) {
                Cache.routingFlag.ResetCurrentFlagPosition();
                Cache.routingFlag.usedRoutingFlag[Cache.routingFlag.currentPeak] = 1;
                Cache.routingFlag.currentlyUsingFlag = true;
            }

            // Keep the picker camera until it closes itself.
            if (picker.isOpen == false) {
                // Cancel any in-progress flight.
                FlyCamera.Release();
            }

            FlagMarkers.Rebuild(currentSet);
            hud.Show(currentSet);
            picker.SyncAfterExternalDelete(currentSet);
        }

        /**
         * <summary>
         * Finds the flag closest to the player's aim direction,
         * within a search cone and range, or -1 if none qualifies.
         * </summary>
         */
        private int FindAimedFlagIndex() {
            if (Cache.playerCamera == null || currentSet == null) {
                return -1;
            }

            const float maxRange = 20f;
            const float minDot = 0.985f; // ~10 degree cone

            Transform cam = Cache.playerCamera.transform;
            int best = -1;
            float bestDot = minDot;

            for (int i = 0; i < currentSet.flags.Count; i++) {
                Vector3 pos = FlagPlacer.Resolve(currentSet.flags[i]);
                Vector3 toFlag = pos - cam.position;
                float distance = toFlag.magnitude;

                if (distance > maxRange) {
                    continue;
                }

                float dot = Vector3.Dot(cam.forward, toFlag.normalized);
                if (dot > bestDot) {
                    bestDot = dot;
                    best = i;
                }
            }

            return best;
        }

        /**
         * <summary>
         * Opens or closes the picker.
         * </summary>
         */
        private void TogglePicker() {
            if (MoreRoutingFlags.Config.IsActive() == false || currentSet == null) {
                return;
            }

            if (picker.isOpen == true) {
                picker.Close();
            }
            else {
                picker.Open(currentSet);
            }
        }

        /**
         * <summary>
         * Executes each frame.
         * </summary>
         */
        private void Update() {
            if (picker != null) {
                picker.Update();
            }

            UpdateDeleteHold();
        }

        /**
         * <summary>
         * Repeats deletion after a held-key delay.
         * </summary>
         */
        private void UpdateDeleteHold() {
            if (MoreRoutingFlags.Config.IsActive() == false
                || UnityEngine.Input.GetKey(MoreRoutingFlags.Config.deleteKeybind.Value) == false
            ) {
                if (deleteHoldPending == true) {
                    deleteHoldPending = false;
                    SyncAfterDelete();
                }

                deleteHoldTimer = 0f;
                deleteHoldSyncTimer = 0f;
                return;
            }

            deleteHoldTimer += Time.deltaTime;

            if (deleteHoldTimer >= deleteHoldDelay) {
                if (RemoveActiveFlag() == true) {
                    deleteHoldPending = true;
                }

                deleteHoldTimer -= deleteHoldInterval;

                // Batch expensive refresh work.
                deleteHoldSyncTimer += deleteHoldInterval;
                if (deleteHoldSyncTimer >= deleteHoldSyncInterval && deleteHoldPending == true) {
                    deleteHoldPending = false;
                    deleteHoldSyncTimer = 0f;
                    SyncAfterDelete();
                }
            }
        }

        /**
         * <summary>
         * Logs a debug message.
         * </summary>
         * <param name="message">The message to log</param>
         */
        internal static void LogDebug(string message) {
#if DEBUG
            if (instance == null) {
                Console.WriteLine($"[Debug] MoreRoutingFlags: {message}");
                return;
            }

            instance.Logger.LogInfo(message);
#else
            if (instance != null) {
                instance.Logger.LogDebug(message);
            }
#endif
        }

        /**
         * <summary>
         * Logs an informational message.
         * </summary>
         * <param name="message">The message to log</param>
         */
        internal static void LogInfo(string message) {
            if (instance == null) {
                Console.WriteLine($"[Info] MoreRoutingFlags: {message}");
                return;
            }
            instance.Logger.LogInfo(message);
        }

        /**
         * <summary>
         * Logs an error message.
         * </summary>
         * <param name="message">The message to log</param>
         */
        internal static void LogError(string message) {
            if (instance == null) {
                Console.WriteLine($"[Error] MoreRoutingFlags: {message}");
                return;
            }
            instance.Logger.LogError(message);
        }
    }
}
