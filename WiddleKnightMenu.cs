using Satchel.BetterMenus;
using System.IO;
using System.Linq;

namespace WiddleKnight
{
    internal class WiddleKnightMenu
    {
        private static Menu MenuRef;
        private static string[] availableSkins = new string[] { "No skins found" };

        internal static Menu PrepareMenu()
        {
            LoadAvailableSkins();

            var menu = new Menu("WiddleKnight Skin", new Element[]
            {
                new HorizontalOption(
                    "Option",
                    "",
                    new string[] { "off", "CurrentKnight", "Custom" },
                    (index) => 
                    {
                        WiddleKnight.Instance.GlobalSettings.SelectedSkinOption = index;
                        WiddleKnight.Instance.OnOptionChanged();
                        UpdateDescription();
                    },
                    () => WiddleKnight.Instance.GlobalSettings.SelectedSkinOption,
                    Id: "SkinOption"
                ),
                new HorizontalOption(
                    "Skin",
                    "",
                    availableSkins,
                    (index) => 
                    {
                        WiddleKnight.Instance.GlobalSettings.CustomSubOption = index;
                        WiddleKnight.Instance.OnOptionChanged();
                    },
                    () => WiddleKnight.Instance.GlobalSettings.CustomSubOption,
                    Id: "CustomSubOption"
                )
                {
                    isVisible = false
                }
            });

            return menu;
        }

        private static void LoadAvailableSkins()
        {
            try
            {
                string modPath = Path.GetDirectoryName(typeof(WiddleKnight).Assembly.Location);
                string skinsPath = Path.Combine(modPath, "Skins");

                if (Directory.Exists(skinsPath))
                {
                    string[] skinFolders = Directory.GetDirectories(skinsPath);
                    
                    if (skinFolders.Length > 0)
                    {
                        availableSkins = skinFolders.Select(path => Path.GetFileName(path)).ToArray();
                        WiddleKnight.Instance.Log($"Found {availableSkins.Length} skins: {string.Join(", ", availableSkins)}");
                    }
                    else
                    {
                        availableSkins = new string[] { "No skins found" };
                        WiddleKnight.Instance.Log("Skins folder exists but is empty");
                    }
                }
                else
                {
                    availableSkins = new string[] { "No skins found" };
                    WiddleKnight.Instance.Log($"Skins folder not found at: {skinsPath}");
                }
            }
            catch (System.Exception e)
            {
                availableSkins = new string[] { "Error loading skins" };
                WiddleKnight.Instance.LogError($"Error loading skins: {e.Message}");
            }
        }

        internal static void UpdateDescription()
        {
            if (MenuRef == null) return;

            var skinOption = MenuRef.Find("SkinOption") as HorizontalOption;
            var customSubOption = MenuRef.Find("CustomSubOption");
            
            if (skinOption != null)
            {
                int selectedIndex = WiddleKnight.Instance.GlobalSettings.SelectedSkinOption;
                
                if (selectedIndex == 0)
                {
                    skinOption.Description = "";
                }
                else if (selectedIndex == 1)
                {
                    skinOption.Description = "Your Knights current skin";
                }
                else if (selectedIndex == 2)
                {
                    skinOption.Description = "Custom skin options";
                }
            }

            if (customSubOption != null)
            {
                customSubOption.isVisible = WiddleKnight.Instance.GlobalSettings.SelectedSkinOption == 2;
                MenuRef.Update();
            }
        }

        internal static MenuScreen GetMenu(MenuScreen lastMenu)
        {
            MenuRef ??= PrepareMenu();
            
            MenuRef.OnBuilt += (_, _) =>
            {
                UpdateDescription();
            };

            return MenuRef.GetMenuScreen(lastMenu);
        }
    }
}
