using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Settings;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using WinRT;

namespace ContextMenuBuilder
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ComWrappersSupport.InitializeComWrappers();

            var instance = AppInstance.FindOrRegisterForKey("main");
            if (!instance.IsCurrent)
            {
                if (IsSingleInstanceEnabled())
                {
                    var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                    instance.RedirectActivationToAsync(activatedArgs).GetAwaiter().GetResult();
                    return;
                }
            }

            instance.Activated += OnActivated;

            XamlOptionalChanges.EnableChange(XamlChangeId.DefaultStyleOptimizations);
            XamlOptionalChanges.EnableChange(XamlChangeId.OptimizeApplyStyles);
            XamlOptionalChanges.EnableChange(XamlChangeId.IconNoGridOptimization);
            XamlOptionalChanges.EnableChange(XamlChangeId.DeferContextFlyoutInit);
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }

        private static void OnActivated(object? _, AppActivationArguments args)
        {
            if (Application.Current is App app)
            {

            }
        }

        private static bool IsSingleInstanceEnabled()
        {
            return true;
        }
    }
}
