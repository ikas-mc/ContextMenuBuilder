using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextMenuBuilder.Views.MenuPackage.PackageMenu
{
    /// <summary>
    /// Attached property that resolves a shell icon location into an <see cref="Image"/> source.
    /// </summary>
    public static class MenuItemIconLoader
    {
        public static string GetSource(DependencyObject obj)
        {
            return (string)obj.GetValue(SourceItemProperty);
        }

        public static void SetSource(DependencyObject obj, string value)
        {
            obj.SetValue(SourceItemProperty, value);
        }

        public static readonly DependencyProperty SourceItemProperty =
            DependencyProperty.RegisterAttached("Source", typeof(string), typeof(MenuItemIconLoader), new PropertyMetadata(null, ChangedCallback));

        public async static void ChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image != null && e.NewValue is string path)
            {
                image.Source = await AppLogoReader.ReadIconAsync(path);
                return;
            }

            if (null != image)
            {
                image.Source = null;
            }
        }
    }
}
