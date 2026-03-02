using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextMenuBuilder.Core.View.Common
{
    public static class MessageHelper
    {
        private static void UpdateStatus(bool busy)
        {
            if (Window.Current.Content is Frame shell && shell?.Content is IStatusSupport statusSupport)
            {
                statusSupport.UpdateStatus(busy);
            }
        }

        public static void UpdateMessage(bool show, MessageType messageType, string message = "")
        {
            if (Window.Current.Content is Frame shell && shell?.Content is IMessageSupport messageSupport)
            {
                messageSupport.UpdateMessage(show, messageType, message);
            }
        }
    }
}