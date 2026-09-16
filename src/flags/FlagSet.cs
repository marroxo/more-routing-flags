using System.Collections.Generic;

namespace MoreRoutingFlags {
    /**
     * <summary>
     * Holds every flag placed on the current peak
     * and which one is currently active.
     * </summary>
     */
    internal class FlagSet {
        internal List<Flag> flags { get; private set; } = new List<Flag>();
        internal int active { get; private set; } = -1;

        /**
         * <summary>
         * Adds a new flag and makes it active.
         * </summary>
         * <param name="flag">The flag to add</param>
         */
        internal void Add(Flag flag) {
            flags.Add(flag);
            active = flags.Count - 1;
        }

        /**
         * <summary>
         * Overwrites the active flag.
         *
         * Falls back to <see cref="Add"/> if there is no active flag.
         * </summary>
         * <param name="flag">The flag to replace the active one with</param>
         */
        internal void Replace(Flag flag) {
            if (active == -1) {
                Add(flag);
                return;
            }

            flags[active] = flag;
        }

        /**
         * <summary>
         * Removes the active flag.
         *
         * The new active flag becomes the previous one, or none
         * if the set is now empty.
         * </summary>
         */
        internal void Remove() {
            if (active == -1) {
                return;
            }

            flags.RemoveAt(active);

            if (flags.Count == 0) {
                active = -1;
                return;
            }

            active = (active - 1 + flags.Count) % flags.Count;
        }

        /**
         * <summary>
         * Switches to the next flag, wrapping around.
         * </summary>
         */
        internal void Next() {
            if (flags.Count == 0) {
                return;
            }

            active = (active + 1) % flags.Count;
        }

        /**
         * <summary>
         * Switches to the previous flag, wrapping around.
         * </summary>
         */
        internal void Previous() {
            if (flags.Count == 0) {
                return;
            }

            active = (active - 1 + flags.Count) % flags.Count;
        }

        /**
         * <summary>
         * Selects a specific flag by index.
         * </summary>
         * <param name="index">The index to select</param>
         */
        internal void Select(int index) {
            if (index < 0 || index >= flags.Count) {
                return;
            }

            active = index;
        }

        /**
         * <summary>
         * Gets the currently active flag, or null if none.
         * </summary>
         */
        internal Flag Active() {
            if (active == -1) {
                return null;
            }

            return flags[active];
        }
    }
}
