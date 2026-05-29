using ContextMenuBuilder.Core.View.Common;
using System;

namespace ContextMenuBuilder
{
    public static class ShellContext
    {
        private static WeakReference<IMessageSupport?> _messageSupport = new(null);

        public static void UpdateMessageSupport(IMessageSupport messageSupport)
        {
            _messageSupport.SetTarget(messageSupport);
        }

        public static void UpdateMessage(bool show, MessageType messageType, string message)
        {
            if (_messageSupport.TryGetTarget(out var messageSupport))
            {
                messageSupport.UpdateMessage(show, messageType, message);
            }
        }

        public static void ShowMessage(string message, MessageType messageType = MessageType.Info)
        {
            if (_messageSupport.TryGetTarget(out var messageSupport))
            {
                messageSupport.UpdateMessage(true, messageType, message);
            }
        }
    }
}
