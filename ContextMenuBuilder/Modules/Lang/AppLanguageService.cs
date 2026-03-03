using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Storage;

namespace ContextMenuBuilder.Modules.Lang
{

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,WriteIndented =true)]
    [JsonSerializable(typeof(AppLang))]
    internal partial class AppLangJsonContext : JsonSerializerContext
    {
    }

    public class AppLanguageService
    {
        static string LanguagesFolderName = "languages";
        private static readonly List<string> _defaultLanguages = new List<string>() { "en-US" };

        public static async Task<AppLang> LoadAsync()
        {
            var langFileName = AppContext.AppSettings.CurrentLanguage;

            //default lang
            if (string.IsNullOrEmpty(langFileName) || !langFileName.EndsWith(".json"))
            {
                return new AppLang();
            }

            //custom lang
            try
            {
                return await LoadCustomAsync(langFileName);
            }
            catch (Exception)
            {
                return new AppLang();
            }
        }

        public static Task<AppLang> LoadDefualtAsync(string langFileName = null)
        {
            return Task.FromResult(new AppLang());
        }

        public static Task<AppLang> LoadCustomAsync(string langFileName)
        {
            return Task.Run(async () =>
            {
                var langFile = await GetCustomLanguageFileAsync(langFileName);
                var langContent = await FileIO.ReadTextAsync(langFile);

                return JsonSerializer.Deserialize(langContent, AppLangJsonContext.Default.AppLang) ?? throw new Exception($"Deserialize {langFileName} error");
            });
        }

        public static async Task<List<LangInfo>> QueryLangList()
        {
            return await Task.Run(async () =>
            {
                var langInfoList = new List<LangInfo>();
                _defaultLanguages.ForEach(name =>
                {
                    var language = tryParseLanguageTag(name);
                    if (null != language)
                    {
                        var langInfo = LangInfo.Create(name, name, language.DisplayName, true);
                        langInfoList.Add(langInfo);
                    }
                });

                var langsFolder = await GetCustomLanguagesFolderAsync();
                var langFiles = await langsFolder.GetFilesAsync();

                foreach (var file in langFiles)
                {
                    var fileName = file.Name;
                    if (fileName.EndsWith(".json"))
                    {
                        var name = Path.GetFileNameWithoutExtension(fileName);
                        var language = tryParseLanguageTag(name);
                        if (null != language)
                        {
                            LangInfo langInfo = LangInfo.Create(name, fileName, language.DisplayName, false);
                            langInfoList.Add(langInfo);
                        }
                    }
                }
                return langInfoList;
            });
        }

        public static async Task<StorageFolder> GetCustomLanguagesFolderAsync()
        {
            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(LanguagesFolderName);
            if (item is StorageFolder storageFolder)
            {
                return storageFolder;
            }
            else
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(LanguagesFolderName, CreationCollisionOption.OpenIfExists);
                return folder;
            }
        }

        public static async Task<StorageFile> GetCustomLanguageFileAsync(string langFileName)
        {
            string path = Path.Combine(AppDataPaths.GetDefault().LocalAppData, LanguagesFolderName, langFileName);
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            return file;
        }

        public static async Task AddCustomLanguageFileAsync(StorageFile file, bool back)
        {
            var fileName = file.Name;
            if (!fileName.EndsWith(".json"))
            {
                throw new Exception("Language file format is not json");
            }

            var langContent = await FileIO.ReadTextAsync(file);
            try
            {
                JsonSerializer.Deserialize(langContent, AppLangJsonContext.Default.AppLang);
            }
            catch (Exception e)
            {
                throw new Exception($"Language file parse error,{e.Message}");
            }

            var langsFolder = await GetCustomLanguagesFolderAsync();

            if (back)
            {
                var oldFile = await langsFolder.TryGetItemAsync(fileName);
                if (oldFile != null)
                {
                    await oldFile.RenameAsync(fileName + ".back", NameCollisionOption.GenerateUniqueName);
                }
            }

            await file.CopyAsync(langsFolder, fileName, NameCollisionOption.ReplaceExisting);
        }

        private static readonly AppLangJsonContext s_langJsonContext =
        new(new JsonSerializerOptions(AppLangJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

        public static async Task ExportLanguageToFileAsync(Func<string, Task<StorageFile?>> fileFunc)
        {
            var fileName = AppContext.AppSettings.CurrentLanguage;

            //default lang
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".json"))
            {
                fileName = _defaultLanguages.First() + ".json";
            }

            var file = await fileFunc(fileName);
            if (null == file)
            {
                return;
            }

            AppLang applang = await LoadAsync();

            var options = new JsonSerializerOptions(AppLangJsonContext.Default.Options)
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 不转义中文
                WriteIndented = true
            };

            await FileIO.WriteTextAsync(file, JsonSerializer.Serialize(applang, s_langJsonContext.AppLang));
        }

        public static void UpdateLangSetting(LangInfo langInfo)
        {
            AppContext.AppSettings.CurrentLanguage = langInfo.FileName;
            ApplicationLanguages.PrimaryLanguageOverride = langInfo.Name;
        }

        public static Language? tryParseLanguageTag(string name)
        {
            try
            {
                return new Language(name);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public record LangInfo(string Name, string FileName, string DisplayName, bool IsDefault)
    {
        public static LangInfo Create(string name, string fileName, string displayName, bool isDefault)
        {
            var langInfo = new LangInfo(name, fileName, displayName, isDefault);
            return langInfo;
        }
    }
}