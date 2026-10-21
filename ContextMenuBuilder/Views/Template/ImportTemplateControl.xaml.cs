using ContextMenuBuilder.Modules.File;
using ContextMenuBuilder.Service.Template;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class ImportTemplateControl : UserControl
    {
        private readonly TemplateViewModel _viewModel;

        public ImportTemplateControl(TemplateViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
        }

        private static AppLang Lang => AppContext.AppLang;

        public Task<bool> ConfirmAsync(CancellationToken cancellationToken)
        {
            return _viewModel.ImportAsync(cancellationToken);
        }

        private async void OnPickClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await PickerHelper.PickSingleFileAsync(picker =>
                {
                    picker.FileTypeFilter.Add(".msix");
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
                _viewModel.ReportError(string.Format(Lang.TemplateStatusPickFileFailed, ex.Message));
            }
        }

        private async void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var launched = await Launcher.LaunchUriAsync(TemplateService.DownloadUri);
                if (launched)
                {
                    return;
                }

                _viewModel.ReportError(string.Format(Lang.TemplateStatusDownloadFailed, "Unknown error"));
            }
            catch (Exception ex)
            {
                _viewModel.ReportError(string.Format(Lang.TemplateStatusDownloadFailed, ex.Message));
            }
        }
    }
}
