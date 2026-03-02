namespace ContextMenuBuilder.Core.View.Common
{
    public interface IStatusSupport
    {
        void UpdateStatus(bool busy, string message = "");
    }
}
