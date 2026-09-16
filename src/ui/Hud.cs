using System.Collections;

using UILib;
using UILib.Components;
using UILib.Layouts;
using UILib.Patches;
using UnityEngine;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * The small "Flag 2/5" overlay which pops up whenever the
     * active flag changes.
     * </summary>
     */
    internal class Hud {
        private IEnumerator coroutine;

        private Overlay overlay;
        private Image background;
        private Label label;

        /**
         * <summary>
         * Initializes the hud.
         * </summary>
         */
        internal Hud() {
            Theme theme = Theme.GetTheme();

            overlay = new Overlay(220f, 60f);
            overlay.SetAnchor(AnchorType.BottomRight);
            overlay.SetOffset(-20f, 20f);
            overlay.SetLockMode(LockMode.None);

            background = new Image(theme.background);
            background.SetFill(FillType.All);
            background.SetInheritTheme(false);
            background.SetContentLayout(LayoutType.Vertical);
            background.SetContentPadding(10);
            overlay.Add(background);

            label = new Label("", 22);
            label.SetSize(200f, 40f);
            background.Add(label);
        }

        /**
         * <summary>
         * Shows the hud with the current flag count, then hides it
         * again after <see cref="Config.hudWaitTime"/>.
         * </summary>
         * <param name="set">The flag set to display</param>
         */
        internal void Show(FlagSet set) {
            if (Config.showHud.Value == false || Plugin.instance == null) {
                return;
            }

            if (coroutine != null) {
                Plugin.instance.StopCoroutine(coroutine);
                coroutine = null;
            }

            Flag active = set.Active();
            string text = active != null && string.IsNullOrEmpty(active.name) == false
                ? active.name
                : $"Flag {set.active + 1}/{set.flags.Count}";

            label.SetText(text);

            coroutine = DisplayRoutine();
            Plugin.instance.StartCoroutine(coroutine);
        }

        /**
         * <summary>
         * Shows the overlay, waits, then hides it.
         * </summary>
         */
        private IEnumerator DisplayRoutine() {
            overlay.Show();

            float timer = 0f;
            while (timer < Config.hudWaitTime.Value) {
                timer += Time.deltaTime;
                yield return null;
            }

            overlay.Hide();
            coroutine = null;
        }
    }
}
