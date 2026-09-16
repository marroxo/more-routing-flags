using System;
using System.Collections.Generic;

using UILib;
using UILib.Layouts;
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
        // Below this world-unit distance the 3D flag replaces its button
        private const float closeButtonDistance = 12f;

        private static Logger logger = new Logger(typeof(Picker));

        private Overlay overlay;
        private List<UIButton> buttons = new List<UIButton>();
        private List<Flag> flags = new List<Flag>();
        private FlagSet currentSet;
        private Renderer activeGlowRenderer;

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

            currentSet = set;

            FlyCamera.FrameAll(set.flags);

            Rebuild(set);
            FlagMarkers.SetPickable(true);
            SetActiveGlow(true);
            FlyCamera.SetClutterHidden(true, set.flags);

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

            // Checked directly: isCurrentlyNavigationMenu would close this the instant it opens
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) == true) {
                Close();
                return;
            }

            if (FlyCamera.camera == null) {
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

                // Close enough to click the 3D flag directly, button is redundant now
                bool closeEnough = behindCamera == false && screenPos.z < closeButtonDistance;
                button.gameObject.SetActive(closeEnough == false);

                if (closeEnough == true) {
                    continue;
                }

                RectTransform rect = button.rectTransform;
                rect.position = ClampToScreenEdge(screenPos);
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
            int index = RaycastFlagIndex(out hitActive);

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
         * Raycasts from the mouse and deletes whichever flag marker
         * was hit, if any. The active flag can't be deleted this way
         * (no marker to click), same as it can't be aimed-and-deleted
         * while it's the one you're standing at.
         * </summary>
         */
        private void TryRightClickDelete() {
            bool hitActive;
            int index = RaycastFlagIndex(out hitActive);

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
         * <returns>The index into <see cref="flags"/> hit, or -1</returns>
         */
        private int RaycastFlagIndex(out bool hitActive) {
            hitActive = false;

            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() == true
            ) {
                logger.LogInfo("RaycastFlagIndex: blocked, pointer over UI");
                return -1;
            }

            Ray ray = FlyCamera.camera.ScreenPointToRay(UnityEngine.Input.mousePosition);

            RaycastHit hit;
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
                Close();
            }
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

            Close(releaseCamera: false);

            FlyCamera.FlyTo(flag, () => {
                FlagPlacer.Apply(flag);
                FlagPlacer.Teleport();
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

            isOpen = false;
            overlay.Hide();
            FlagMarkers.SetPickable(false);
            SetActiveGlow(false);
            FlyCamera.SetClutterHidden(false);

            if (releaseCamera == true) {
                FlyCamera.Release();
            }
        }
    }
}
