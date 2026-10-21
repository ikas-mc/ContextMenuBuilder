using ContextMenuBuilder.Service.Template;
using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextMenuBuilder
{
    public sealed partial class TemplatePage : Page
    {
        private CancellationTokenSource? _operationSource;
        private ContentDialog? _activeDialog;

        public TemplateViewModel ViewModel { get; } = new();

        public TemplatePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static AppLang Lang => AppContext.AppLang;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            await ViewModel.InitializeAsync(source.Token);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _activeDialog?.Hide();
            CancelOperations();
        }

        private async void OnImportTemplateClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            ViewModel.DismissMessage();

            var control = new ImportTemplateControl(ViewModel);
            var dialog = CreateDialog(Lang.TemplateImportTitle, Lang.TemplateImportButton, control);

            dialog.PrimaryButtonClick += async (dialogSender, args) =>
            {
                var deferral = args.GetDeferral();
                dialogSender.IsPrimaryButtonEnabled = false;

                try
                {
                    args.Cancel = !await control.ConfirmAsync(source.Token);
                }
                finally
                {
                    dialogSender.IsPrimaryButtonEnabled = true;
                    deferral.Complete();
                }
            };

            await ShowDialogAsync(dialog);
        }

        private async void OnReloadClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            if (await ViewModel.LoadAsync(source.Token))
            {
                ViewModel.ReportSuccess(Lang.TemplateStatusLoaded);
            }
        }

        private async void OnOpenTemplateFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = ViewModel.TemplateFolderPath;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    ViewModel.ReportError(Lang.TemplateStatusNoTemplate);
                    return;
                }

                Directory.CreateDirectory(folder);
                await Launcher.LaunchFolderPathAsync(folder);
            }
            catch (Exception ex)
            {
                ViewModel.ReportError(string.Format(Lang.TemplateStatusOpenFolderFailed, ex.Message));
            }
        }

        private async void OnDownloadTemplateClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var launched = await Launcher.LaunchUriAsync(TemplateService.DownloadUri);
                if (launched)
                {
                    return;
                }

                ViewModel.ReportError(string.Format(Lang.TemplateStatusDownloadFailed, "Unknown error"));
            }
            catch (Exception ex)
            {
                ViewModel.ReportError(string.Format(Lang.TemplateStatusDownloadFailed, ex.Message));
            }
        }

        private async Task ShowDialogAsync(ContentDialog dialog)
        {
            _activeDialog = dialog;

            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                _activeDialog = null;
            }
        }

        private ContentDialog CreateDialog(string title, string primaryButtonText, object content)
        {
            return new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = Lang.CommonClose,
                DefaultButton = ContentDialogButton.Primary,
            };
        }

        private CancellationTokenSource ReplaceOperationSource()
        {
            CancelOperations();
            var source = new CancellationTokenSource();
            _operationSource = source;
            return source;
        }

        private void CancelOperations()
        {
            var source = _operationSource;
            if (source is null)
            {
                return;
            }

            _operationSource = null;
            source.Cancel();
            source.Dispose();
        }
    }
}
