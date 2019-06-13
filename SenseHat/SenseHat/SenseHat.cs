using System;
using System.Runtime.InteropServices;

namespace SenseHat {
    /// <summary>
    /// Misc. Raspberry Pi SenseHat Library Utils
    /// </summary>
    public static class SenseHat {
        public static readonly Version LibraryVersion = new Version(1, 1, 0, 0); // Version of Library

        // Probe to discover the dev name for the joystick
        [DllImport("stick.so")]
        private static extern string probe_sense_stick();

        /// <summary>
        /// Checks if SenseHat is attached
        /// </summary>
        /// <returns><c>true</c> if is attached; otherwise, <c>false</c>.</returns>
        public static bool IsAttached () {
            return !String.IsNullOrEmpty (probe_sense_stick ()); // Check if a valid device name is returned
        }
    }
}
