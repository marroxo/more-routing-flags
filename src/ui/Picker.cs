using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;
using UILib;
using UILib.Layouts;
using UILib.Notifications;
using UILib.Patches;
using UnityEngine;
using UnityEngine.EventSystems;

using UIButton = UILib.Components.Button;
using UECamera = UnityEngine.Camera;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Unity's built-in Button only fires on left-click -- this adds
     * a right-click callback to whatever GameObject it's attached to.
     * </summary>
     */
    internal class RightClickHandler : MonoBehaviour, IPointerClickHandler {
        internal Action onRightClick;

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.button == PointerEventData.InputButton.Right) {
                onRightClick?.Invoke();
            }
        }
    }

    /**
     * <summary>
     * A full-screen overlay with one clickable button per flag,
     * positioned in screen space every frame.
     * </summary>
     */
    internal class Picker {
        // How long buttons take to fly in from screen center on open
        private const float flyInDuration = 0.4f;

        private static Logger logger = new Logger(typeof(Picker));
        private static readonly FieldInfo playerField = AccessTools.Field(typeof(RoutingFlag), "player");

        private Overlay overlay;
        private List<UIButton> buttons = new List<UIButton>();
        private List<Flag> flags = new List<Flag>();
        private FlagSet currentSet;
        private Renderer activeGlowRenderer;
        private float openTime;

        // Pending flag placement.
        private Flag orientingFlag;
        private Vector3 preOrientPosition;
        private Quaternion preOrientRotation;

        internal bool isOpen { get; private set; }

        /**
         * <summary>
         * Initializes the picker, hidden until opened.
         * </summary>
         */
        internal Picker() {
            overlay = new Overlay(Screen.width, Screen.height);
            overlay.SetAnchor(AnchorType.Middle);
            overlay.SetLockMode(LockMode.Default);
        }

        /**
         * <summary>
         * Opens the picker over the given flag set, framing an
         * overview shot of every flag.
         * </summary>
         * <param name="set">The flag set to display</param>
         */
        internal void Open(FlagSet set) {
            if (isOpen == true) {
                return;
            }

            if (FlyCamera.FrameAll(set.flags) == false) {
                Notifier.Notify(
                    "More Routing Flags",
                    "Can't open the picker right now",
                    NotificationType.Normal
                );
                return;
            }

            currentSet = set;
            openTime = Time.time;

            Rebuild(set);
            FlagMarkers.SetPickable(true);
            SetActiveGlow(true);

            overlay.Show();
            isOpen = true;
        }

        /**
         * <summary>
         * Glows the active flag's real (live) cloth renderer -- it
         * has no marker of its own (FlagMarkers skips the active
         * index), so once its button hides at close range there'd be
         * no way to tell it apart otherwise. Uses an instanced
         * material (Renderer.material, not sharedMaterial) so this
         * never bleeds into the markers, which share the same source
         * material.
         * </summary>
         * <param name="on">Whether the glow should be on</param>
         */
        private void SetActiveGlow(bool on) {
            if (on == true) {
                if (Cache.routingFlag == null) {
                    return;
                }

                foreach (Renderer renderer in Cache.routingFlag.routingFlagTransform.GetComponentsInChildren<Renderer>(true)) {
                    if (renderer.gameObject.name.ToLowerInvariant() == "routingflag") {
                        activeGlowRenderer = renderer;
                        break;
                    }
                }

                if (activeGlowRenderer == null) {
                    return;
                }

                activeGlowRenderer.material.EnableKeyword("_EMISSION");
                activeGlowRenderer.material.SetColor("_EmissionColor", Cache.routingFlag.selectColor);
            }
            else if (activeGlowRenderer != null) {
                activeGlowRenderer.material.SetColor("_EmissionColor", Color.black);
                activeGlowRenderer = null;
            }
        }

        /**
         * <summary>
         * Rebuilds the button list to match the given flag set.
         * </summary>
         * <param name="set">The flag set to build buttons for</param>
         */
        private void Rebuild(FlagSet set) {
            foreach (UIButton button in buttons) {
                GameObject.Destroy(button.gameObject);
            }

            buttons.Clear();
            flags.Clear();

            Theme theme = Theme.GetTheme();

            for (int i = 0; i < set.flags.Count; i++) {
                Flag flag = set.flags[i];
                string text = string.IsNullOrEmpty(flag.name) == false ? flag.name : $"{i + 1}";

                UIButton button = new UIButton(text, 20);
                button.SetSize(80f, 40f);

                if (i == set.active) {
                    button.background.SetColor(theme.selectHighlight);
                }

                int index = i;
                button.onClick.AddListener(() => Select(set, index));

                RightClickHandler rightClick = button.gameObject.AddComponent<RightClickHandler>();
                rightClick.onRightClick = () => DeleteFlag(index);

                overlay.Add(button);
                buttons.Add(button);
                flags.Add(flag);
            }
        }

        /**
         * <summary>
         * Repositions every button in screen space, hiding any
         * that are behind the camera. Called every frame while open.
         * </summary>
         */
        internal void Update() {
            if (isOpen == false) {
                return;
            }

            if (FlyCamera.camera == null) {
                return;
            }

            if (orientingFlag != null) {
                UpdateOrienting();
                return;
            }

            // Checked directly: isCurrentlyNavigationMenu would close this the instant it opens
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) == true) {
                Close();
                return;
            }

            // Manual zoom toward the cursor, independent of the player
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (scroll != 0f) {
                Ray zoomRay = FlyCamera.camera.ScreenPointToRay(UnityEngine.Input.mousePosition);

                // Scales with overview size so Solemn Tempest doesn't feel too slow
                float speed = Mathf.Max(3f, FlyCamera.DistanceFromBoundsCenter() * 0.15f);
                FlyCamera.Zoom(zoomRay.direction, scroll * speed);
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) == true) {
                TryClickFlag();
            }

            if (UnityEngine.Input.GetMouseButtonDown(1) == true) {
                TryRightClickDelete();
            }

            // Middle mouse pans the overview.
            if (UnityEngine.Input.GetMouseButton(2) == true && FlyCamera.isFlying == false) {
                float panYaw = UnityEngine.Input.GetAxis("Mouse X") * LookSensitivityX();
                float panPitch = UnityEngine.Input.GetAxis("Mouse Y") * LookSensitivityY();
                FlyCamera.FreeLook(panYaw, panPitch);
            }

            if (PlacePressed() == true) {
                TryPlaceFlag();
            }

            for (int i = 0; i < buttons.Count; i++) {
                UIButton button = buttons[i];
                Vector3 worldPos = FlagPlacer.Resolve(flags[i]);
                Vector3 screenPos = FlyCamera.camera.WorldToScreenPoint(worldPos);

                bool behindCamera = screenPos.z < 0f;

                // WorldToScreenPoint mirrors x/y when the target is behind the
                // camera, flip back so the edge clamp below points the right way
                if (behindCamera == true) {
                    screenPos.x = Screen.width - screenPos.x;
                    screenPos.y = Screen.height - screenPos.y;
                }

                button.gameObject.SetActive(behindCamera == false);

                if (behindCamera == true) {
                    continue;
                }

                Vector3 target = ClampToScreenEdge(screenPos);

                float t = Mathf.Clamp01((Time.time - openTime) / flyInDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 flyInStart = new Vector3(Screen.width / 2f, Screen.height / 2f, target.z);

                RectTransform rect = button.rectTransform;
                rect.position = Vector3.Lerp(flyInStart, target, eased);
            }
        }

        /**
         * <summary>
         * Pulls a screen position back onto the visible screen along the
         * line from center to it, so off-screen flags still show a button
         * at the edge instead of vanishing.
         * </summary>
         * <param name="screenPos">The unclamped screen position</param>
         */
        private static Vector3 ClampToScreenEdge(Vector3 screenPos) {
            const float margin = 40f;

            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 fromCenter = new Vector2(screenPos.x, screenPos.y) - center;

            if (fromCenter.sqrMagnitude < 0.01f) {
                return screenPos;
            }

            float scaleX = (Screen.width / 2f - margin) / Mathf.Max(Mathf.Abs(fromCenter.x), 0.01f);
            float scaleY = (Screen.height / 2f - margin) / Mathf.Max(Mathf.Abs(fromCenter.y), 0.01f);
            float scale = Mathf.Min(1f, scaleX, scaleY);

            Vector2 clamped = center + fromCenter * scale;
            return new Vector3(clamped.x, clamped.y, screenPos.z);
        }

        /**
         * <summary>
         * Raycasts from the mouse into the scene and selects the flag
         * hit, if any -- the buttons stay as a fallback for flags
         * that are hard to see/aim at directly (occluded, too small,
         * clustered together).
         * </summary>
         */
        private void TryClickFlag() {
            bool hitActive;
            RaycastHit hit;
            int index = RaycastFlagIndex(out hitActive, out hit);

            if (hitActive == true) {
                // Active flag has no marker, clicking it just closes the picker
                Close();
                return;
            }

            if (index != -1) {
                Select(currentSet, index);
            }
        }

        /**
         * <summary>
         * Checks the vanilla placement binding.
         * </summary>
         */
        private bool PlacePressed() {
            Rewired.Player player = Cache.routingFlag != null && playerField != null
                ? (Rewired.Player) playerField.GetValue(Cache.routingFlag)
                : Rewired.ReInput.players.GetPlayer(0);

            return player != null && player.GetButtonDown("Interact");
        }

        /**
         * <summary>
         * Starts a flag placement at the camera target.
         * </summary>
         */
        private void TryPlaceFlag() {
            Ray ray = FlyCamera.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            bool hitActive;
            RaycastHit hit;
            int index = RaycastFlagIndex(ray, out hitActive, out hit);

            if (hitActive == true || index != -1) {
                return;
            }

            if (hit.collider == null || Cache.leavePeakScene == null) {
                return;
            }

            // Match vanilla placement validation.
            RaycastHit groundHit;
            bool foundGround = Physics.Raycast(ray, out groundHit, 200f, Cache.terrainMask)
                && (Cache.routingFlag == null
                    || (groundHit.collider.CompareTag("ClimbableRigidbody") == false
                        && groundHit.collider.name.Contains("ResetBox") == false
                        && groundHit.collider.gameObject.layer != 17));

            if (foundGround == false) {
                Notifier.Notify(
                    "More Routing Flags",
                    "Aim at solid ground to place a flag",
                    NotificationType.Normal
                );
                return;
            }

            if (currentSet.flags.Count >= Config.maxFlags.Value) {
                Notifier.Notify(
                    "More Routing Flags",
                    $"Flag limit reached ({Config.maxFlags.Value})",
                    NotificationType.Normal
                );
                return;
            }

            Vector3 offset = Cache.leavePeakScene.transform.InverseTransformPoint(groundHit.point);
            orientingFlag = new Flag(offset, groundHit.normal, 0f, 0f, "");

            preOrientPosition = FlyCamera.position;
            preOrientRotation = FlyCamera.rotation;

            FlyCamera.SnapTo(
                groundHit.point + groundHit.normal,
                Quaternion.LookRotation(-groundHit.normal)
            );

            Notifier.Notify(
                "More Routing Flags",
                "Look where you want this flag to face, then press E to confirm",
                NotificationType.Normal
            );
        }

        /**
         * <summary>
         * Updates the pending flag orientation.
         * </summary>
         */
        private void UpdateOrienting() {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) == true) {
                CancelOrient();
                return;
            }

            float deltaYaw = UnityEngine.Input.GetAxis("Mouse X") * LookSensitivityX();
            float deltaPitch = UnityEngine.Input.GetAxis("Mouse Y") * LookSensitivityY();
            FlyCamera.FreeLook(deltaYaw, deltaPitch);

            if (PlacePressed() == true) {
                ConfirmOrient();
            }
        }

        /**
         * <summary>
         * Gets the player yaw sensitivity.
         * </summary>
         */
        private static float LookSensitivityX() {
            return Cache.playerCamX != null ? Cache.playerCamX.sensitivityX : 2f;
        }

        /**
         * <summary>
         * Gets the player pitch sensitivity.
         * </summary>
         */
        private static float LookSensitivityY() {
            return Cache.playerCamY != null ? Cache.playerCamY.sensitivityY : 2f;
        }

        /**
         * <summary>
         * Saves the pending flag and returns to the overview.
         * </summary>
         */
        private void ConfirmOrient() {
            Vector3 euler = FlyCamera.rotation.eulerAngles;
            orientingFlag.camX = euler.y;
            orientingFlag.camY = euler.x > 180f ? euler.x - 360f : euler.x;

            currentSet.Add(orientingFlag);
            FlagStore.Save(Plugin.currentKey, currentSet);

            FlagMarkers.Rebuild(currentSet);
            FlagMarkers.SetPickable(true);
            Rebuild(currentSet);
            Plugin.instance.hud.Show(currentSet);

            Vector3 returnPos = preOrientPosition;
            Quaternion returnRot = preOrientRotation;
            orientingFlag = null;

            FlyCamera.FlyBackTo(returnPos, returnRot, null);
        }

        /**
         * <summary>
         * Discards the pending flag and returns to the overview.
         * </summary>
         */
        private void CancelOrient() {
            Vector3 returnPos = preOrientPosition;
            Quaternion returnRot = preOrientRotation;
            orientingFlag = null;

            FlyCamera.FlyBackTo(returnPos, returnRot, null);
        }

        /**
         * <summary>
         * Raycasts from the mouse and deletes whichever flag marker
         * was hit, if any. The active flag can't be deleted this way
         * (no marker to click), same as it can't be aimed-and-deleted
         * while it's the one you're standing at.
         * </summary>
         */
        private void TryRightClickDelete() {
            bool hitActive;
            RaycastHit hit;
            int index = RaycastFlagIndex(out hitActive, out hit);

            if (hitActive == false && index != -1) {
                DeleteFlag(index);
            }
        }

        /**
         * <summary>
         * Raycasts from the mouse into the scene, skipping clicks
         * that landed on a UI button first.
         * </summary>
         * <param name="hitActive">Whether the ray hit the active flag's live object</param>
         * <param name="hit">The raw raycast hit, for callers that need the world point/normal</param>
         * <returns>The index into <see cref="flags"/> hit, or -1</returns>
         */
        private int RaycastFlagIndex(out bool hitActive, out RaycastHit hit) {
            hitActive = false;
            hit = default(RaycastHit);

            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() == true
            ) {
                logger.LogInfo("RaycastFlagIndex: blocked, pointer over UI");
                return -1;
            }

            Ray ray = FlyCamera.camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            return RaycastFlagIndex(ray, out hitActive, out hit);
        }

        /**
         * <summary>
         * Finds a flag hit by a ray.
         * </summary>
         * <param name="ray">The ray to cast</param>
         * <param name="hitActive">Whether the ray hit the active flag's live object</param>
         * <param name="hit">The raw raycast hit, for callers that need the world point/normal</param>
         * <returns>The index into <see cref="flags"/> hit, or -1</returns>
         */
        private int RaycastFlagIndex(Ray ray, out bool hitActive, out RaycastHit hit) {
            hitActive = false;

            if (Physics.Raycast(ray, out hit, 200f) == false) {
                logger.LogInfo("RaycastFlagIndex: no hit");
                return -1;
            }

            logger.LogInfo($"RaycastFlagIndex: hit {hit.collider.gameObject.name} at {hit.point}");

            FlagMarkerRef markerRef = hit.collider.GetComponentInParent<FlagMarkerRef>();
            if (markerRef != null) {
                int index = flags.IndexOf(markerRef.flag);
                logger.LogInfo($"RaycastFlagIndex: has FlagMarkerRef, resolved index={index}");
                return index;
            }

            if (Cache.routingFlag != null
                && hit.collider.transform.IsChildOf(Cache.routingFlag.routingFlagTransform) == true
            ) {
                hitActive = true;
            }

            return -1;
        }

        /**
         * <summary>
         * Deletes a flag by index and refreshes the picker/markers/hud
         * to match.
         * </summary>
         * <param name="index">The index into the current set to delete</param>
         */
        private void DeleteFlag(int index) {
            currentSet.Select(index);
            currentSet.Remove();
            FlagStore.Save(Plugin.currentKey, currentSet);

            FlagMarkers.Rebuild(currentSet);
            FlagMarkers.SetPickable(true);
            Rebuild(currentSet);
            Plugin.instance.hud.Show(currentSet);

            if (currentSet.flags.Count == 0) {
                CloseAndFlyBack();
            }
        }

        /**
         * <summary>
         * Refreshes an open picker after an external deletion.
         * </summary>
         * <param name="set">The flag set, already updated by the caller</param>
         */
        internal void SyncAfterExternalDelete(FlagSet set) {
            if (isOpen == false) {
                return;
            }

            if (set.flags.Count == 0) {
                CloseAndFlyBack();
            }
            else {
                Rebuild(set);
                ScheduleReframe(set);
            }
        }

        // Delay before reframing after deletion.
        private const float reframeDelay = 0.3f;
        private IEnumerator reframeRoutine;

        /**
         * <summary>
         * Schedules one delayed overview reframe.
         * </summary>
         * <param name="set">The flag set to reframe on</param>
         */
        private void ScheduleReframe(FlagSet set) {
            if (reframeRoutine != null) {
                Plugin.instance.StopCoroutine(reframeRoutine);
            }

            reframeRoutine = ReframeAfterDelay(set);
            Plugin.instance.StartCoroutine(reframeRoutine);
        }

        private IEnumerator ReframeAfterDelay(FlagSet set) {
            yield return new WaitForSeconds(reframeDelay);
            reframeRoutine = null;
            FlyCamera.FrameAll(set.flags);
        }

        /**
         * <summary>
         * Closes the picker and returns to the player.
         * </summary>
         */
        private void CloseAndFlyBack() {
            Close(releaseCamera: false);

            if (Cache.playerCamera == null) {
                FlyCamera.Release();
                return;
            }

            FlyCamera.FlyBackTo(
                Cache.playerCamera.transform.position,
                Cache.playerCamera.transform.rotation,
                FlyCamera.Release
            );
        }

        /**
         * <summary>
         * Selects a flag: closes the picker and flies to it.
         * </summary>
         * <param name="set">The flag set the selection belongs to</param>
         * <param name="index">The index that was clicked</param>
         */
        private void Select(FlagSet set, int index) {
            set.Select(index);
            Flag flag = set.Active();

            Plugin.instance.hud.Show(set);
            Close(releaseCamera: false);

            FlyCamera.FlyTo(flag, () => {
                FlagPlacer.Apply(flag);
                FlagPlacer.Teleport(flag);
                FlyCamera.Release();
                FlagMarkers.Rebuild(set);
                Plugin.instance.hud.Show(set);
            });
        }

        /**
         * <summary>
         * Closes the picker.
         * </summary>
         * <param name="releaseCamera">
         * Whether this close should also release the fly camera back
         * to the player, used for the escape/picker-key path. A flag
         * selection releases the camera itself once its flight lands,
         * so it passes false here to avoid cutting the flight short.
         * </param>
         */
        internal void Close(bool releaseCamera = true) {
            if (isOpen == false) {
                return;
            }

            // Discard an unfinished placement.
            orientingFlag = null;

            if (reframeRoutine != null) {
                Plugin.instance.StopCoroutine(reframeRoutine);
                reframeRoutine = null;
            }

            isOpen = false;
            overlay.Hide(force: true);
            FlagMarkers.SetPickable(false);
            SetActiveGlow(false);

            if (releaseCamera == true) {
                FlyCamera.Release();
            }
        }
    }
}
