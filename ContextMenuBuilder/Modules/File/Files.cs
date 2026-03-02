using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace ContextMenuBuilder.Modules.File
{
    public static class Files
    {
        public static async Task CopyFolderDeep(StorageFolder src, StorageFolder dest, bool replace, Func<string, string>? callback, CancellationTokenSource? cancellationTokenSource = null)
        {
            var items = await src.GetItemsAsync();
            if (null != items)
            {
                foreach (IStorageItem item in items)
                {
                    if (cancellationTokenSource?.IsCancellationRequested == true)
                    {
                        break;
                    }

                    if (item is StorageFile file)
                    {
                        NameCollisionOption collision = replace ? NameCollisionOption.ReplaceExisting : NameCollisionOption.GenerateUniqueName;
                        callback?.Invoke(file.Name);
                        await file.CopyAsync(dest, file.Name, collision);
                    }
                    else if (item is StorageFolder folder)
                    {
                        var descSub = await dest.CreateFolderAsync(folder.Name, CreationCollisionOption.OpenIfExists);
                        await CopyFolderDeep(folder, descSub, replace, callback).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
