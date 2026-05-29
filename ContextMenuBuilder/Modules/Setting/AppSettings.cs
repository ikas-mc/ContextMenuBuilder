namespace ContextMenuCustomApp.Common
{
    public partial class Settings
    {

        private readonly AppDataSettings MainSettingDao;

        public Settings()
        {
            MainSettingDao = new AppDataSettings("app-settings");
        }

        public string WinAppCliPath
        {
            get => MainSettingDao.GetValue(nameof(WinAppCliPath), "winapp.exe");
            set => MainSettingDao.SetValue(nameof(WinAppCliPath), value);
        }

        public string CertPath
        {
            get => MainSettingDao.GetValue(nameof(CertPath), string.Empty);
            set => MainSettingDao.SetValue(nameof(CertPath), value);
        }

        public string? CertPassword
        {
            get => MainSettingDao.GetValue(nameof(CertPassword), "password");
            set => MainSettingDao.SetValue(nameof(CertPassword), value);
        }

        public string MenuBackupPath
        {
            get => MainSettingDao.GetValue(nameof(MenuBackupPath), string.Empty);
            set => MainSettingDao.SetValue(nameof(MenuBackupPath), value);
        }

        public string MenuPackageTemplatePath
        {
            get => MainSettingDao.GetValue(nameof(MenuPackageTemplatePath), string.Empty);
            set => MainSettingDao.SetValue(nameof(MenuPackageTemplatePath), value);
        }

        public string MenuPackageIdPrefix
        {
            get => MainSettingDao.GetValue(nameof(MenuPackageIdPrefix), "CMC.");
            set => MainSettingDao.SetValue(nameof(MenuPackageIdPrefix), value);
        }

        public bool EnableMica
        {
            get => MainSettingDao.GetValue(nameof(EnableMica), false);
            set => MainSettingDao.SetValue(nameof(EnableMica), value);
        }

        public int PatchVersion
        {
            get => MainSettingDao.GetValue(nameof(PatchVersion), 0);
            set => MainSettingDao.SetValue(nameof(PatchVersion), value);
        }

        public ulong AppVersion
        {
            get => MainSettingDao.GetValue(nameof(AppVersion), (ulong)0);
            set => MainSettingDao.SetValue(nameof(AppVersion), value);
        }

        public bool EnableWizard
        {
            get => MainSettingDao.GetValue(nameof(EnableWizard), true);
            set => MainSettingDao.SetValue(nameof(EnableWizard), value);
        }

        public string CurrentLanguage
        {
            get => MainSettingDao.GetValue(nameof(CurrentLanguage), "");
            set => MainSettingDao.SetValue(nameof(CurrentLanguage), value);
        }

        public int ThemeType
        {
            get => MainSettingDao.GetValue(nameof(ThemeType), 0);
            set => MainSettingDao.SetValue(nameof(ThemeType), value);
        }

        public bool EnableUISound
        {
            get => MainSettingDao.GetValue(nameof(EnableUISound), false);
            set => MainSettingDao.SetValue(nameof(EnableUISound), value);
        }
    }
}