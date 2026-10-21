using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ContextMenuBuilder.Views.MenuPackage.PackageMenu
{
    /// <summary>
    /// Loads a bitmap from a shell icon location ("file.exe,index") or an absolute URI.
    /// </summary>
    public static class AppLogoReader
    {
        public async static Task<BitmapImage?> ReadIconAsync(string url)
        {
            if (url.StartsWith('"') && url.EndsWith('"'))
            {
                url = url.Trim('"');
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new BitmapImage(uri);
            }

            int lastComma = url?.LastIndexOf(',') ?? -1;
            if (lastComma > 0 && int.TryParse(url.Substring(lastComma + 1), out int idx))
            {
                var file = url.Substring(0, lastComma).Trim('\"');
                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = await Task.Run(() => LoadExeIcon(file, idx));
                    if (null != stream)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.DecodePixelWidth = 40;
                        bitmap.DecodePixelHeight = 40;
                        bitmap.SetSource(stream.AsRandomAccessStream());
                        return bitmap;
                    }
                }
            }

            return null;
        }

        public static Stream? LoadExeIcon(string filePath, int iconIndex)
        {
            try
            {
                HICON[] largeIcons = new HICON[1];
                var count = PInvoke.ExtractIconEx(filePath, 0, largeIcons);
                if (count > 0 && largeIcons[0] != IntPtr.Zero)
                {
                    using var icon = System.Drawing.Icon.FromHandle(largeIcons[0]);
                    var ms = new MemoryStream();
                    icon.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    PInvoke.DestroyIcon(largeIcons[0]);
                    return ms;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }
    }
}
