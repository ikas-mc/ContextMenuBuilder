
using ContextMenuBuilder.Modules.Lang;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;


namespace ContextMenuBuilder
{
    public class AppContext
    {
        public static App CurrentApp()
        {
            return Application.Current as App ?? throw new System.Exception();
        }

        public static AppLang AppLang { get; private set; } = new AppLang();

        public static Settings AppSettings { get; private set; } = new Settings();

        private static Task? _langTask;

        public static void Init()
        {
            _langTask = Task.Run(async () =>
            {
                AppBootstrap.Run(AppSettings);
                AppLang = await AppLanguageService.LoadAsync().ConfigureAwait(false);
            });
        }

        public static async Task WaitAll()
        {
            if (_langTask != null && !_langTask.IsCompleted)
            {
                await _langTask;
            }
        }
    }
}