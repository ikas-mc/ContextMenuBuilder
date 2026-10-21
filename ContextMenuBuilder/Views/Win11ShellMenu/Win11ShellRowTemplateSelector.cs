using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextMenuBuilder
{
    public sealed partial class Win11ShellRowTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PackageTemplate { get; set; } = null!;

        public DataTemplate ComItemTemplate { get; set; } = null!;

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is Win11ShellPackageRow)
            {
                return PackageTemplate;
            }

            return ComItemTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
