namespace RoomOrchestra
{
    /// <summary>
    /// How the interface is drawn (A11Y-08: interface scale and contrast): one View button steps through these.
    /// Large text makes the control labels bigger; high contrast puts pure white text and bright icons on
    /// near-black slabs. Pure rules; ViewSettings applies them.
    /// </summary>
    public enum ViewMode { Standard, LargeText, HighContrast, LargeHighContrast }

    public static class ViewModes
    {
        public const int Count = 4;
        /// <summary>How much bigger large text is.</summary>
        public const float LargeTextScale = 1.3f;

        // Owner, headset 2026-11-08: "use the high contrast as the default and leave the button to adjust the size of the
        // text ... I like the highlighted yellow." High contrast is always on; the button (Text size) only switches
        // between standard and large text.
        /// <summary>The setting a new player starts with.</summary>
        public const ViewMode Default = ViewMode.HighContrast;

        /// <summary>Always high contrast: an older saved setting keeps only its text size.</summary>
        public static ViewMode WithContrast(ViewMode mode) => IsLarge(mode) ? ViewMode.LargeHighContrast : ViewMode.HighContrast;

        /// <summary>The Text size button: standard and large text, in turn (always high contrast).</summary>
        public static ViewMode Next(ViewMode mode) => IsLarge(mode) ? ViewMode.HighContrast : ViewMode.LargeHighContrast;
        public static bool IsLarge(ViewMode mode) => mode == ViewMode.LargeText || mode == ViewMode.LargeHighContrast;
        public static bool IsHighContrast(ViewMode mode) => mode == ViewMode.HighContrast || mode == ViewMode.LargeHighContrast;
        public static float TextScale(ViewMode mode) => IsLarge(mode) ? LargeTextScale : 1f;

        public static UiTerm Term(ViewMode mode) => mode switch
        {
            ViewMode.LargeText => UiTerm.ViewLarge,
            ViewMode.LargeHighContrast => UiTerm.ViewLarge,
            _ => UiTerm.ViewStandard,
        };

        /// <summary>WCAG relative luminance of a linear-ish sRGB colour (0..1 channels).</summary>
        public static double Luminance(double r, double g, double b)
        {
            static double C(double c) => c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
            return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
        }

        /// <summary>WCAG contrast ratio between two luminances (1..21).</summary>
        public static double Contrast(double l1, double l2) => (System.Math.Max(l1, l2) + 0.05) / (System.Math.Min(l1, l2) + 0.05);
    }
}
