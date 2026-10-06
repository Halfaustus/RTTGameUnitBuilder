namespace RTTUnitEditor.UI
{
    // UI navigation only, not game objects or a configuration schema.
    internal sealed class NavigationPage
    {
        public readonly string Title;
        public NavigationPage(string title)
        {
            Title = title;
        }
        public static NavigationPage[] CreatePages()
        {
            return new[]
            {
                new NavigationPage("单位本体"),
                new NavigationPage("武器"),
                new NavigationPage("共享弹药"),
                new NavigationPage("武器分配"),
                new NavigationPage("单位配置")
            };
        }
    }
}
