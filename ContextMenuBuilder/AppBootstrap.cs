using ContextMenuBuilder.Modules.Setting;
using ContextMenuCustomApp;
using System.IO;
using Windows.Storage;

namespace ContextMenuBuilder
{
    public class AppBootstrap
    {
        public static void Run(AppSettings settings)
        {
            if (settings.AppVersion <= 0)
            {
                var localFolder = ApplicationData.Current.LocalFolder.Path;
                settings.MenuBackupPath = Path.Combine(localFolder, "menus-backup");
                settings.CertPassword = "password";
                settings.CertPath = Path.Combine(localFolder, "cert.pfx");
                settings.MenuPackageTemplatePath = Path.Combine(localFolder, "template");

                settings.AppVersion = AppVersion.Current();
            }
        }
    }
}
