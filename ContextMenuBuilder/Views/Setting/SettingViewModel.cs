using ContextMenuBuilder.Modules.File;
using ContextMenuBuilder.Modules.Lang;
using ContextMenuCustomApp.Common;
using ContextMenuCustomApp.View.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;


namespace ContextMenuBuilder
{
    public partial class SettingViewModel : BaseViewModel
    {
        public AppLang AppLang { get; } = AppContext.AppLang;

        private static readonly Uri ProjectHomepageUri = new("https://github.com/ikas-mc/ContextMenuBuilder");

        public SettingViewModel()
        {
        }

        public Uri ProjectHomepage => ProjectHomepageUri;

        public string AppName()
        {
            return Package.Current.DisplayName;
        }

        public string AppVersion()
        {
            return string.Format("{0}.{1}.{2}",
             Package.Current.Id.Version.Major,
             Package.Current.Id.Version.Minor,
             Package.Current.Id.Version.Build);
        }


        public async Task ClearTempFolder()
        {
            await RunWith(async () =>
            {
                IStorageItem folder = await ApplicationData.Current.TemporaryFolder.TryGetItemAsync("files");
                if (folder is StorageFolder filesFolder)
                {
                    var tempFolder = new DirectoryInfo(filesFolder.Path);
                    tempFolder.Delete(true);
                }
            }).ConfigureAwait(false);
        }

        #region language

        private List<LangInfo>? _languages;

        public List<LangInfo> Languages
        {
            get => _languages ?? [];
            set => SetProperty(ref _languages, value);
        }

        public async Task LoadLanguages()
        {
            var languages = await RunWith(async () =>
            {
                return await AppLanguageService.QueryLangList();
            });
            Languages = languages ?? new List<LangInfo>();
        }

        public void UpdateLangSetting(LangInfo langInfo)
        {
            AppLanguageService.UpdateLangSetting(langInfo);
        }

        public async Task ExportLang()
        {
            await RunWith(() =>
            {
                return AppLanguageService.ExportLanguageToFileAsync(suggestedFileName =>
                    PickerHelper.PickSaveFileAsync(picker =>
                    {
                        picker.SuggestedStartLocation = PickerLocationId.Desktop;
                        picker.SuggestedFileName = suggestedFileName ?? string.Empty;
                        picker.FileTypeChoices.Add("Json", new List<string> { ".json" });
                    }));
            });
        }

        public async Task ImportLang()
        {
            await RunWith(async () =>
            {
                var file = await PickerHelper.PickSingleFileAsync(picker =>
                {
                    picker.SuggestedStartLocation = PickerLocationId.Desktop;
                    picker.FileTypeFilter.Add(".json");
                });

                if (file is null)
                {
                    return;
                }

                await AppLanguageService.AddCustomLanguageFileAsync(file, true);
            });

            await LoadLanguages();
        }

        public LangInfo? GetCurrentLang()
        {
            var langFileName = AppContext.AppSettings.CurrentLanguage;
            var langInfo = Languages.Find(x => x.FileName == langFileName);
            if (null == langInfo)
            {
                langInfo = Languages.FirstOrDefault();
            }
            return langInfo;
        }

        public async void OpenLanguagesFolder()
        {
            await RunWith(async () =>
            {
                var folder = await AppLanguageService.GetCustomLanguagesFolderAsync();
                await Launcher.LaunchFolderAsync(folder);
            });
        }

        #endregion
    }
}