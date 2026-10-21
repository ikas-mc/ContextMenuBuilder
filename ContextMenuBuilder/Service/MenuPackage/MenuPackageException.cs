using System;

namespace ContextMenuBuilder.Service.MenuPackage
{
    public class MenuPackageException : Exception
    {
        public MenuPackageException(string message)
            : base(message)
        {
        }

        public MenuPackageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class MenuPackageValidationException : MenuPackageException
    {
        public MenuPackageValidationException(string message)
            : base(message)
        {
        }
    }
}
