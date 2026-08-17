namespace FishSocial.Desktop
{
    public enum DesktopProductMenuAction
    {
        CurrentPond,
        WorldMap,
        Shop,
        Friends,
        CatchBag,
        Gallery,
        Settings,
        HideToTray,
        Quit,
    }

    public interface IDesktopProductMenuHandler
    {
        void HandleProductMenu(DesktopProductMenuAction action);
    }

    public static class DesktopProductMenuCommands
    {
        public const string Pond = "menu_pond";
        public const string Map = "menu_map";
        public const string Shop = "menu_shop";
        public const string Friends = "menu_friends";
        public const string CatchBag = "menu_catch";
        public const string Gallery = "menu_gallery";
        public const string Settings = "menu_settings";
        public const string HideToTray = "hide_to_tray";
        public const string Quit = "quit_app";

        public static bool TryParse(string command, out DesktopProductMenuAction action)
        {
            switch (command)
            {
                case Pond:
                    action = DesktopProductMenuAction.CurrentPond;
                    return true;
                case Map:
                    action = DesktopProductMenuAction.WorldMap;
                    return true;
                case Shop:
                    action = DesktopProductMenuAction.Shop;
                    return true;
                case Friends:
                    action = DesktopProductMenuAction.Friends;
                    return true;
                case CatchBag:
                    action = DesktopProductMenuAction.CatchBag;
                    return true;
                case Gallery:
                    action = DesktopProductMenuAction.Gallery;
                    return true;
                case Settings:
                    action = DesktopProductMenuAction.Settings;
                    return true;
                case HideToTray:
                    action = DesktopProductMenuAction.HideToTray;
                    return true;
                case Quit:
                    action = DesktopProductMenuAction.Quit;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }
    }
}
