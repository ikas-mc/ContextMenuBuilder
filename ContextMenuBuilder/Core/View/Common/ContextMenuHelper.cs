using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ContextMenuBuilder.Core.View.Common
{
    public static class ContextMenuHelper
    {
        public static void ShowAt(FlyoutBase flyout, FrameworkElement placementTarget)
        {
            var option = new FlyoutShowOptions() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
            flyout.ShowAt(placementTarget, option);
        }

        public static void ShowAt(FlyoutBase flyout, UIElement currentTarget, ContextRequestedEventArgs args)
        {
            if (args.OriginalSource is FrameworkElement element)
            {
                if (args.TryGetPosition(currentTarget, out var point))
                {
                    var option = new FlyoutShowOptions() { Position = point };
                    flyout.ShowAt(currentTarget, option);
                }
                else
                {
                    flyout.ShowAt(element);
                }
            }
        }
    }
}