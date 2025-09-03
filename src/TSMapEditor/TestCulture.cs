using System;
using System.Globalization;
using TSMapEditor.I18N;
using TSMapEditor.Extensions;

namespace TSMapEditor
{
    public static class TestCulture
    {
        public static void TestCurrentCulture()
        {
            Console.WriteLine($"Current UI Culture: {CultureInfo.CurrentUICulture.Name}");
            Console.WriteLine($"Current Culture: {CultureInfo.CurrentCulture.Name}");
            
            // Force load Chinese translation
            var chineseCulture = new CultureInfo("zh-CN");
            Console.WriteLine($"Forcing Chinese culture: {chineseCulture.Name}");
            
            TranslationManager.LoadTranslationForCulture(chineseCulture);
            
            // Test some translations
            Console.WriteLine($"File translation: {"File".L10N()}");
            Console.WriteLine($"Edit translation: {"Edit".L10N()}");
            Console.WriteLine($"Settings translation: {"Settings".L10N()}");
        }
    }
}