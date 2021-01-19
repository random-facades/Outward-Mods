// MyMod.cs

/*   -------------  TODO LIST  -------------
 *     
 *     Get list of KNOWN Recipes that can be made with inventory items
 *     Fill up remaining ingredients based on inventory items
 *       Also allow clicking to show what other items satisfy that
 *     
 *     MAYBE FEATURES:
 *         Instead of Crafting button, Favorite/Unfavorite the recipe
 *           And show a star or something next to the recipe when next crafting
 *           Remove it once it has been crafted
 *     
 *   -------------  TOLEARN LIST  -------------
 *     
 *     How UI Creation/Displaying works
 *     How to spawn my own menus and close others
 *     How to create duplicates of menus
 *       OR how to temporarily modify 
 *     How to return to original UI
 *     How to disable crafting button or just remove it
 *       Don't forget about T button
 *     How to change background to show different crafting stations
 *     Probably more stuff, but forgot
 *     
 *   -------------  TOFIX (eventually) LIST  -------------
 *     
 *     
 *     
 */

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SharedModConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecipeAssistant
{
    public class Settings
    {
        public static bool Setting = false;
        public static string Setting_Name = "Setting";

        public static ModConfig Instance;

        public static void OnChanged()
        {
            Setting = (bool)Instance.GetValue(Setting_Name);
        }

        public static void SetupConfig()
        {
            var newConfig = new ModConfig
            {
                ModName = RecipeAssistant.NAME,
                SettingsVersion = RecipeAssistant.VersionNum,
                Settings = new List<BBSetting>
                {
                    new BoolSetting
                    {
                        Name = Setting_Name,
                        Description = "Disable Item Price Labels",
                        DefaultValue = false
                    }
                }
            };
            Instance = newConfig;
            Instance.Register();
            Instance.OnSettingsSaved += OnChanged;

            OnChanged();
        }
    }

    [BepInPlugin(ID, NAME, VERSION)]
    [BepInDependency("com.sinai.SharedModConfig", BepInDependency.DependencyFlags.HardDependency)]
    public class RecipeAssistant : BaseUnityPlugin
    {
        public const string ID = "com.random_facades.recipeassistant";
        public const string NAME = "Recipe Assistant";
        public const string VERSION = "0.1";
        public static double VersionNum = 0.1;

        public static RecipeAssistant Instance;
        public static ManualLogSource logboy;

        public static void Log(string message)
        {
            if (logboy != null)
                logboy.Log(LogLevel.Message, message);
        }

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            Settings.SetupConfig();

            Log("Recipe Assistant starting...");
        }
    }
}
