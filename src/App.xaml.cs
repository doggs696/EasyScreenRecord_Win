using System.Configuration;
using System.Data;
using System.Windows;

namespace EasyScreenRecord;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
    public partial class App : Application
    {
        public static EasyScreenRecord.Services.ISettingsService SettingsService { get; private set; } = null!;
        public static EasyScreenRecord.Services.KeyVisualizationService KeyVisualizationService { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize Services
            SettingsService = new EasyScreenRecord.Services.SettingsService();
            KeyVisualizationService = new EasyScreenRecord.Services.KeyVisualizationService();

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception, "Dispatcher");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogError(e.ExceptionObject as Exception, "CurrentDomain");
        }

        private void LogError(Exception? ex, string source)
        {
            if (ex == null) return;
            string logContent = $"[{DateTime.Now}] {source} Unhandled Exception:\n{ex.ToString()}\n--------------------------------------------------\n";
            try
            {
                System.IO.File.AppendAllText("crash.log", logContent);
                MessageBox.Show($"アプリケーションがクラッシュしました。\nエラー詳細を 'crash.log' に保存しました。\n\nエラー: {ex.Message}", "Error - EasyScreenRecord", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                MessageBox.Show($"クラッシュしました (ログ保存失敗):\n{ex.ToString()}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

