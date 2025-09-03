using TSMapEditor.I18N;

namespace TSMapEditor.Extensions
{
    /// <summary>
    /// String extension methods for localization.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Localizes a string using the translation system.
        /// </summary>
        /// <param name="defaultValue">The default value (usually English text).</param>
        /// <param name="key">The translation key. If null, the default value is used as key.</param>
        /// <param name="notify">Whether to notify the translation system about missing keys.</param>
        /// <returns>The localized string or the default value if translation is not found.</returns>
        public static string L10N(this string defaultValue, string key = null, bool notify = true)
        {
            if (string.IsNullOrEmpty(defaultValue))
                return string.Empty;

            var translationKey = key ?? defaultValue;
            return Translation.Instance.LookUp(translationKey, defaultValue, notify);
        }

        /// <summary>
        /// Localizes a string for INI-based controls.
        /// </summary>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="controlName">The control name.</param>
        /// <param name="propertyName">The property name (e.g., "Text", "ToolTip").</param>
        /// <param name="notify">Whether to notify about missing keys.</param>
        /// <returns>The localized string.</returns>
        public static string L10N(this string defaultValue, string controlName, string propertyName, bool notify = true)
        {
            if (string.IsNullOrEmpty(defaultValue))
                return string.Empty;

            var key = $"INI:{controlName}:{propertyName}";
            return Translation.Instance.LookUp(key, defaultValue, notify);
        }
    }
}