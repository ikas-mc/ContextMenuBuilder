using System;

namespace ContextMenuBuilder.Service.Certificate
{
    public class CertificateException : Exception
    {
        public CertificateException(string message)
            : base(message)
        {
        }

        public CertificateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class CertificateValidationException : CertificateException
    {
        public CertificateValidationException(string message)
            : base(message)
        {
        }
    }
}
