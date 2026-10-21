using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class CreateCertificateControl : UserControl
    {
        private readonly CertificatesViewModel _viewModel;

        public CreateCertificateControl(CertificatesViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            SubjectBox.Text = "CN=CMC-TEST";
            ValidDaysBox.Text = "3650";
        }

        public Task<bool> ConfirmAsync(CancellationToken cancellationToken)
        {
            return _viewModel.GenerateAsync(SubjectBox.Text, PasswordInput.Password, ValidDaysBox.Text, cancellationToken);
        }
    }
}
