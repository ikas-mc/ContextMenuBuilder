using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ContextMenuBuilder.Modules.File
{
    public static class PickerHelper
    {
        public static async Task<StorageFile?> PickSingleFileAsync(Action<FileOpenPicker>? configure = null)
        {
            var picker = new FileOpenPicker();
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSingleFileAsync();
        }

        public static async Task<StorageFile?> PickSaveFileAsync(Action<FileSavePicker>? configure = null)
        {
            var picker = new FileSavePicker();
            configure?.Invoke(picker);
            EnsureFileTypeChoices(picker.FileTypeChoices);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSaveFileAsync();
        }

        public static async Task<StorageFolder?> PickFolderAsync(Action<FolderPicker>? configure = null)
        {
            var picker = new FolderPicker();
            configure?.Invoke(picker);
            EnsureFileTypeFilter(picker.FileTypeFilter);

            if (!TryAttachWindow(picker))
            {
                return null;
            }

            return await picker.PickSingleFolderAsync();
        }

        private static void EnsureFileTypeFilter(IList<string> filter)
        {
            if (filter.Count == 0)
            {
                filter.Add("*");
            }
        }

        private static void EnsureFileTypeChoices(IDictionary<string, IList<string>> choices)
        {
            if (choices.Count == 0)
            {
                choices["All files"] = new List<string> { "*" };
            }
        }

        private static bool TryAttachWindow(object picker)
        {
            if (App.MainWindow is null)
            {
                return false;
            }

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
            return true;
        }
    }
}
