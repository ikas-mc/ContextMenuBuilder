using ContextMenuCustomApp.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class CertificatesPage : Page
    {
        private CancellationTokenSource? _operationSource;
        private ContentDialog? _activeDialog;
        private bool _isActive;

        public CertificatesViewModel ViewModel { get; } = new();

        public CertificatesPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static AppLang Lang => AppContext.AppLang;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isActive = true;

            var source = ReplaceOperationSource();
            await ViewModel.InitializeAsync(source.Token);

            if (!_isActive)
            {
                return;
            }

            CurrentCertPasswordBox.Password = ViewModel.InitialPassword;
            AttachInteractionEvents();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isActive = false;
            _activeDialog?.Hide();
            DetachInteractionEvents();
            CancelOperations();
        }

        private void AttachInteractionEvents()
        {
            DetachInteractionEvents();
            CurrentCertPasswordBox.PasswordChanged += OnCurrentPasswordChanged;
        }

        private void DetachInteractionEvents()
        {
            CurrentCertPasswordBox.PasswordChanged -= OnCurrentPasswordChanged;
        }

        private async void OnImportCertificateClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            ViewModel.DismissMessage();

            var control = new ImportCertificateControl(ViewModel);
            var dialog = CreateDialog(Lang.CertificatesImportTitle, Lang.CertificatesImportButton, control);

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
            CurrentCertPasswordBox.Password = ViewModel.InitialPassword;
        }

        private async void OnCreateCertificateClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            ViewModel.DismissMessage();

            var control = new CreateCertificateControl(ViewModel);
            var dialog = CreateDialog(Lang.CertificatesCreateTitle, Lang.CertificatesCreateButton, control);

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
            CurrentCertPasswordBox.Password = ViewModel.InitialPassword;
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

        private void OnMessageClosed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            ViewModel.DismissMessage();
        }

        private async void OnReloadCertClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            if (await ViewModel.ReloadAsync(source.Token))
            {
                ViewModel.ReportSuccess(Lang.CertificatesStatusLoadSuccess);
            }
        }

        private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.SetCurrentPassword(CurrentCertPasswordBox.Password);
        }

        private async void OnInstallWithCliClicked(object sender, RoutedEventArgs e)
        {
            var source = ReplaceOperationSource();
            await ViewModel.InstallWithCliAsync(source.Token);
        }

        private void OnOpenCertManagerClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var info = new ProcessStartInfo("certlm.msc")
                {
                    UseShellExecute = true,
                };

                Process.Start(info);
            }
            catch (Exception ex)
            {
                ViewModel.ReportError(string.Format(Lang.CertificatesStatusOpenManagerFailed, ex.Message));
            }
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
