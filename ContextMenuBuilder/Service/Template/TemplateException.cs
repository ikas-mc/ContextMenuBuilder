using System;

namespace ContextMenuBuilder.Service.Template
{
    public class TemplateException : Exception
    {
        public TemplateException(string message)
            : base(message)
        {
        }

        public TemplateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class TemplateValidationException : TemplateException
    {
        public TemplateValidationException(string message)
            : base(message)
        {
        }
    }
}
