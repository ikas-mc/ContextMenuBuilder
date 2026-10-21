using System;

namespace ContextMenuBuilder.Service.Win11ShellMenu
{
    public class ShellMenuException : Exception
    {
        public ShellMenuException(string message)
            : base(message)
        {
        }

        public ShellMenuException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
