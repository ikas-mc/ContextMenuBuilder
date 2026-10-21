using CommunityToolkit.Mvvm.ComponentModel;
using ContextMenuBuilder.Core.Log;
using ContextMenuBuilder.Service.Win11ShellMenu;
using ContextMenuCustomApp.Common;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ContextMenuBuilder
{
    public sealed partial class Win11ShellMenuViewModel : ObservableObject
    {
        private readonly Win11ShellMenuService _service = new();
        private readonly AppLang _lang = AppContext.AppLang;
        private readonly ILogger _logger = AppLog.For<Win11ShellMenuViewModel>();

        private IReadOnlyList<Win11ShellComItem> _allItems = Array.Empty<Win11ShellComItem>();

        public ObservableCollection<object> Items { get; } = new();

        [ObservableProperty]
        public partial string FilterText
        {
            get; set;
        } = string.Empty;

        [ObservableProperty]
        public partial string StatusMessage
        {
            get; private set;
        } = string.Empty;

        [ObservableProperty]
        public partial bool IsBusy
        {
            get; private set;
        }

        public bool IsNotBusy => !IsBusy;

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        public void ReportStatus(string message)
        {
            StatusMessage = message;
        }

        public async Task<bool> LoadAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                _allItems = await _service.LoadAsync(cancellationToken).ConfigureAwait(true);
                ApplyFilter();
                StatusMessage = string.Format(_lang.Win11ShellMenuStatusLoaded, _allItems.Count, CountPackageRows());
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Shell menu load canceled");
                return false;
            }
            catch (ShellMenuException e)
            {
                _logger.Error(e, "Shell menu load rejected");
                StatusMessage = e.Message;
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Shell menu load failed");
                StatusMessage = string.Format(_lang.Win11ShellMenuStatusLoadFailed, e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> SetBlockedAsync(Win11ShellComItem item, bool blocked, CancellationToken cancellationToken)
        {
            if (item is null)
            {
                return false;
            }

            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;

            try
            {
                await _service.SetEnabledAsync(item.MenuComId, !blocked, cancellationToken).ConfigureAwait(true);
                item.IsEnabled = !blocked;
                StatusMessage = string.Empty;
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Shell menu block state change canceled, clsid={Clsid}", item.MenuComId);
                return false;
            }
            catch (ShellMenuException e)
            {
                _logger.Error(e, "Shell menu block state change rejected, clsid={Clsid}", item.MenuComId);
                StatusMessage = e.Message;
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Shell menu block state change failed, clsid={Clsid}", item.MenuComId);
                StatusMessage = string.Format(_lang.Win11ShellMenuStatusSetBlockedFailed, e.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> LaunchAsync(Win11ShellPackageRow row, CancellationToken cancellationToken)
        {
            if (row is null)
            {
                return false;
            }

            try
            {
                await _service.LaunchAsync(row.IdFullName, cancellationToken).ConfigureAwait(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Shell menu package launch canceled, fullName={FullName}", row.IdFullName);
                return false;
            }
            catch (ShellMenuException e)
            {
                _logger.Error(e, "Shell menu package launch rejected, fullName={FullName}", row.IdFullName);
                StatusMessage = e.Message;
                return false;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Shell menu package launch failed, fullName={FullName}", row.IdFullName);
                StatusMessage = e.Message;
                return false;
            }
        }

        private void ApplyFilter()
        {
            var filter = (FilterText ?? string.Empty).Trim();

            Items.Clear();

            var groups = new Dictionary<string, List<Win11ShellComItem>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (var item in _allItems)
            {
                if (!item.Matches(filter))
                {
                    continue;
                }

                if (!groups.TryGetValue(item.IdFullName, out var bucket))
                {
                    bucket = new List<Win11ShellComItem>();
                    groups[item.IdFullName] = bucket;
                    order.Add(item.IdFullName);
                }

                bucket.Add(item);
            }

            order.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var key in order)
            {
                FlushGroup(groups[key]);
            }
        }

        private void FlushGroup(List<Win11ShellComItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var first = items[0];
            Items.Add(new Win11ShellPackageRow
            {
                IdName = first.IdName,
                IdFullName = first.IdFullName,
                PackageDisplayName = first.PackageDisplayName,
                ApplicationDisplayName = first.ApplicationDisplayName,
                InstallPath = first.PackageInstallPath,
                ItemCount = items.Count,
                PackageLogo = first.PackageLogo,
            });

            foreach (var item in items)
            {
                Items.Add(item);
            }
        }

        private int CountPackageRows()
        {
            var count = 0;
            foreach (var row in Items)
            {
                if (row is Win11ShellPackageRow)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
