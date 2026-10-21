using ContextMenuCustomApp.Common;

namespace ContextMenuBuilder
{
    public partial class MenuPackageSettings
    {
        private readonly PackageAppDataSettings DllSettingDao;

        public MenuPackageSettings(string packageFamilyName)
        {
            DllSettingDao = new PackageAppDataSettings(packageFamilyName);
        }

        public string MenuName
        {
            get => DllSettingDao.GetValue("Custom_Menu_Name", "Open With");
            set => DllSettingDao.SetValue("Custom_Menu_Name", value);
        }

        public string MenuDarkIcon
        {
            get => DllSettingDao.GetValue("Custom_Menu_Dark_Icon", string.Empty);
            set => DllSettingDao.SetValue("Custom_Menu_Dark_Icon", value);
        }

        public string MenuLightIcon
        {
            get => DllSettingDao.GetValue("Custom_Menu_Light_Icon", string.Empty);
            set => DllSettingDao.SetValue("Custom_Menu_Light_Icon", value);
        }
    }
}
