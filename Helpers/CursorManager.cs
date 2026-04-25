using System.Windows.Forms;

namespace AtopPlugin.Helpers
{
    /// <summary>
    /// Manages the five ATOP cursor forms per FAA60004721 Module 1, Lesson 2, Topic 1.
    ///
    ///   1. Arrow        – Up/Left-Pointing Arrow: normal point-and-click operations.
    ///   2. Cross        – Plus Symbol (+): ASD window and geographic coordinate input windows.
    ///   3. HollowX      – Hollow X: cursor not located within any window (vatSys application scope).
    ///   4. Move         – Crossing Arrow: mouse is moving a window by its title bar.
    ///   5. Resize(xxxx) – Uni-Direction Arrows: cursor on a resizable window border.
    /// </summary>
    public static class CursorManager
    {
        // ── The five ATOP cursor forms ───────────────────────────────────────────

        /// <summary>Up/Left-Pointing Arrow — normal point-and-click operations.</summary>
        public static Cursor Arrow => Cursors.Arrow;

        /// <summary>Plus Symbol (+) — ASD window and geographic coordinate input windows.</summary>
        public static Cursor Cross => Cursors.Cross;

        /// <summary>
        /// Hollow X — cursor not located within any window.
        /// No exact built-in Windows equivalent; approximated with <see cref="Cursors.No"/>.
        /// vatSys owns the application-level cursor; this is provided for completeness.
        /// </summary>
        public static Cursor HollowX => Cursors.No;

        /// <summary>Crossing Arrow — while moving a window by its title bar.</summary>
        public static Cursor Move => Cursors.SizeAll;

        /// <summary>Uni-Direction Arrow (N/S) — resizable window top/bottom border.</summary>
        public static Cursor ResizeNS => Cursors.SizeNS;

        /// <summary>Uni-Direction Arrow (E/W) — resizable window left/right border.</summary>
        public static Cursor ResizeWE => Cursors.SizeWE;

        /// <summary>Uni-Direction Arrow (NE/SW diagonal) — resizable window corner.</summary>
        public static Cursor ResizeNESW => Cursors.SizeNESW;

        /// <summary>Uni-Direction Arrow (NW/SE diagonal) — resizable window corner.</summary>
        public static Cursor ResizeNWSE => Cursors.SizeNWSE;

        // ── ASD control cursor override ──────────────────────────────────────────

        private static System.Windows.Forms.Control _asdControl;
        private static Cursor _savedAsdCursor;

        /// <summary>
        /// Overrides the cursor on the main ASD (Air Situation Display) control.
        /// The previous cursor is saved and can be restored with <see cref="ResetAsdCursor"/>.
        /// Typical use: set to <see cref="Cross"/> when entering geographic coordinate input mode.
        /// </summary>
        public static void SetAsdCursor(Cursor cursor)
        {
            var asd = FindAsdControl();
            if (asd == null) return;
            _savedAsdCursor ??= asd.Cursor;
            asd.Cursor = cursor;
        }

        /// <summary>
        /// Restores the ASD control cursor to its state before <see cref="SetAsdCursor"/> was called.
        /// </summary>
        public static void ResetAsdCursor()
        {
            var asd = FindAsdControl();
            if (asd == null || _savedAsdCursor == null) return;
            asd.Cursor = _savedAsdCursor;
            _savedAsdCursor = null;
        }

        private static System.Windows.Forms.Control FindAsdControl()
        {
            if (_asdControl?.IsDisposed == false)
                return _asdControl;

            foreach (Form form in Application.OpenForms)
            {
                var found = SearchControls(form, "ASDControlDX");
                if (found != null)
                {
                    _asdControl = found;
                    return found;
                }
            }
            return null;
        }

        private static System.Windows.Forms.Control SearchControls(System.Windows.Forms.Control parent, string typeName)
        {
            foreach (System.Windows.Forms.Control child in parent.Controls)
            {
                if (child.GetType().Name == typeName) return child;
                var found = SearchControls(child, typeName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
