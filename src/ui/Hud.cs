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
        private const float countStepTime = 0.08f;
        private const float countMinTime = 0.2f;
        private const float countMaxTime = 0.8f;

        private IEnumerator coroutine;

        private Overlay overlay;
        private Image background;
        private Label label;

        // Tracked so a switch can animate the count, a fresh scene can't
        private FlagSet lastSet;
        private int lastActive = -1;

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
            string finalText = active != null && string.IsNullOrEmpty(active.name) == false
                ? active.name
                : $"Flag {set.active + 1}/{set.flags.Count}";

            int fromIndex = set == lastSet ? lastActive : -1;
            int toIndex = set.active;
            int total = set.flags.Count;

            lastSet = set;
            lastActive = toIndex;

            coroutine = DisplayRoutine(fromIndex, toIndex, total, finalText);
            Plugin.instance.StartCoroutine(coroutine);
        }

        /**
         * <summary>
         * Counts the "Flag N/M" text from the previous index to the
         * new one, then shows the final text, waits, and hides.
         * </summary>
         * <param name="fromIndex">The previous active index, or -1 to skip the count</param>
         * <param name="toIndex">The new active index</param>
         * <param name="total">The total flag count</param>
         * <param name="finalText">The text to settle on once the count finishes</param>
         */
        private IEnumerator DisplayRoutine(int fromIndex, int toIndex, int total, string finalText) {
            overlay.Show();

            if (fromIndex != -1 && fromIndex != toIndex && total > 0) {
                int steps = Mathf.Abs(toIndex - fromIndex);
                float duration = Mathf.Clamp(steps * countStepTime, countMinTime, countMaxTime);

                float t = 0f;
                while (t < duration) {
                    t += Time.deltaTime;
                    int shown = Mathf.RoundToInt(Mathf.Lerp(fromIndex, toIndex, t / duration));
                    label.SetText($"Flag {shown + 1}/{total}");
                    yield return null;
                }
            }

            label.SetText(finalText);

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
