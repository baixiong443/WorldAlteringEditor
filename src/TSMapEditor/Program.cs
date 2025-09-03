using System;
using System.Windows.Forms;
using TSMapEditor.Rendering;
using TSMapEditor.I18N;

namespace TSMapEditor
{
    static class Program
    {
        public static string[] args;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Program.args = args;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Environment.CurrentDirectory = Application.StartupPath.Replace('\\', '/');
            
            Console.WriteLine("Starting application...");
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
            
            try
            {
                // Initialize translation system
                Console.WriteLine("Initializing translation system...");
                TranslationManager.Initialize();
                
                // Force load Chinese translation
                Console.WriteLine("Switching to Chinese language...");
                TranslationManager.SwitchLanguage("zh-CN");
                
                // Test culture and translation
                Console.WriteLine("Testing culture and translation...");
                TestCulture.TestCurrentCulture();

                Console.WriteLine("Starting GameClass...");
                new GameClass().Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException((Exception)e.ExceptionObject);
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception);
        }

        private static void HandleException(Exception ex)
        {
            MessageBox.Show("The map editor failed to launch.\r\n\r\nReason: " + ex.Message + "\r\n\r\n Stack trace: " + ex.StackTrace);
        }

        public static void DisableExceptionHandler()
        {
            Application.ThreadException -= Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }
    }
}
