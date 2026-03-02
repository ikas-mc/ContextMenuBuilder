using Windows.ApplicationModel;

namespace ContextMenuCustomApp
{
    public static class AppVersion
    {
        public static ulong Current()
        {
            var v = Package.Current.Id.Version;
            return
                ((ulong)v.Major << 48) |
                ((ulong)v.Minor << 32) |
                ((ulong)v.Build << 16) |
                v.Revision;
        }
    }
}
