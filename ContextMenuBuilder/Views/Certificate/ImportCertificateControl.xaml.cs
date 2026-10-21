using ContextMenuBuilder.Modules.File;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextMenuBuilder
{
    public sealed partial class ImportCertificateControl : UserControl
    {
        private readonly CertificatesViewModel _viewModel;

        public ImportCertificateControl(CertificatesViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
        }

        private static AppLang Lang => AppContext.AppLang;

        public Task<bool> ConfirmAsync(CancellationToken cancellationToken)
        {
            return _viewModel.ImportAsync(PasswordInput.Password, cancellationToken);
        }

        private async void OnPickClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await PickerHelper.PickSingleFileAsync(picker =>
                {
                    picker.FileTypeFilter.Add(".pfx");
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                });

                if (file is null)
                {
                    return;
                }

                _viewModel.SetImportSource(file.Path, file.Name);
            }
            catch (Exception ex)
            {
                _viewModel.ReportError(string.Format(Lang.CertificatesStatusPickFileFailed, ex.Message));
            }
        }
    }
}
