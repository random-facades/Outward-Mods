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
 *     The Stash
 *     Backpack duplication
 *     
 */

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SharedModConfig;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    public class ContainerWrapper
    {
        public static ItemContainer Instance;
        public static DictionaryExt<string, Item> ContainedItems
        {
            get
            {
                if (Instance == null)
                    return null;
                return (DictionaryExt<string, Item>)GetFieldHelper("m_containedItems");
            }
            set
            {
                SetFieldHelper("m_containedItems", value);
            }
        }

        public static void Init()
        {
            Instance = (ItemContainer)ResourcesPrefabManager.Instance.GenerateItem("0");
            Instance.IsGenerated = true;
            Instance.RegisterUID();

            SetFieldHelper("m_canRemoveItems", false);
            Instance.CanContainMoney = false;
            ContainedItems = new DictionaryExt<string, Item>();
        }

        public static void SetFieldHelper(string fieldName, object value)
        {
            SideLoader.At.SetField(Instance, fieldName, value);
        }

        public static object GetFieldHelper(string fieldName)
        {
            return SideLoader.At.GetField(Instance, fieldName);
        }

        public static void DestroyPouch()
        {
            foreach (Item thing in Instance.GetContainedItems())
                ItemManager.Instance.DestroyItem(thing.UID);

            ItemManager.Instance.DestroyItem(Instance.UID);
            Instance = null;
        }

        public static Item ProcessItem(Item pre)
        {
            pre.DoUpdate();
            pre.RegisterUID();
            RecipeAssistant.Log("Processing: " + pre.Name + " - " + pre.UID);
            return pre;
        }

        public static void LoadItemsFromContainer(ItemContainer box)
        {
            if (box != null)
                foreach (Item item in box.GetContainedItems())
                    if (item != null && !Instance.ContainsOfSameID(item.ItemID))
                        Instance.AddItem(ProcessItem(ResourcesPrefabManager.Instance.GenerateItem(item.ItemIDString)));
        }

        internal static void LoadItemsFromEquipment(CharacterEquipment equipment)
        {
            EquipmentSlot[] slots = (EquipmentSlot[])SideLoader.At.GetField(equipment, "m_equipmentSlots");
            RecipeAssistant.Log("Did it work? " + (slots != null));

            foreach (EquipmentSlot slot in slots)
            {
                if (slot?.EquippedItem != null && !Instance.ContainsOfSameID(slot.EquippedItem.ItemID))
                    Instance.AddItem(ProcessItem(ItemManager.Instance.CloneItem(slot.EquippedItem)));
            }
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

        public const int ACTION_ID = 33;

        public static RecipeAssistant Instance;
        public static ManualLogSource logboy;
        public static ItemContainer CraftedBox;
        public static bool AssistantActive = false;
        public static CharacterUI CharUI;

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

        private static void CloseAssistant()
        {
            Log("Closing Assistant");
            ContainerWrapper.DestroyPouch();
            AssistantActive = false;
            CharUI.CloseAllMenus();
            CharUI = null;
            Log("Closed Assistant");
        }

        private static ItemContainer CreateContainerFor(Character player)
        {
            if (ContainerWrapper.Instance == null)
                ContainerWrapper.Init();

            Log("CreateContainerFor");
            ContainerWrapper.LoadItemsFromContainer(player.Inventory.Pouch);
            ContainerWrapper.LoadItemsFromContainer(player.Inventory.EquippedBag?.Container);
            ContainerWrapper.LoadItemsFromEquipment(player.Inventory.Equipment);

            return ContainerWrapper.Instance;
        }

        [HarmonyPatch(typeof(ItemDisplayOptionPanel))]
        public class ItemDisplayOptionPanel_HarmonyPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("GetActiveActions", new Type[] { typeof(GameObject) })]
            public static void Postfix_GetActiveActions(ItemDisplayOptionPanel __instance, ref List<int> __result)
            {
                if (AssistantActive)
                    __result.Clear();
                else
                    __result.Add(ACTION_ID);
            }

            [HarmonyPrefix]
            [HarmonyPatch("GetActionText", new Type[] { typeof(int) })]
            public static bool Prefix_GetActionText(int _actionID, ref string __result)
            {
                if (_actionID == ACTION_ID)
                {
                    __result = "Show Recipes";
                    return false;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("ActionHasBeenPressed", new Type[] { typeof(int) })]
            public static bool Prefix_ActionHasBeenPressed(int _actionID, ref ItemDisplay ___m_activatedItemDisplay, ref Item ___m_pendingItem, ref CharacterUI ___m_characterUI)
            {
                if (_actionID == ACTION_ID)
                {
                    AssistantActive = true;
                    CharUI = ___m_characterUI;
                    MenuPanel[] m_menus = (MenuPanel[])SideLoader.At.GetField(___m_characterUI, "m_menus");

                    Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                    CharUI.CloseAllMenus();

                    EnvironmentItemDisplay environ = (EnvironmentItemDisplay)m_menus[(int)CharacterUI.MenuScreens.PreviewContainer];
                    CraftingMenu craft = (CraftingMenu)m_menus[(int)CharacterUI.MenuScreens.Crafting];

                    CraftedBox = CreateContainerFor(player);
                    environ.Show(CraftedBox);
                    ((Text)SideLoader.At.GetField(environ, "m_lblTitle")).text = "Inventory";
                    ((Button)SideLoader.At.GetField(environ, "m_btnTakeAll")).gameObject.SetActive(false);
                    environ.SetIsParallel(CharacterUI.MenuScreens.Inventory);

                    craft.Show();


                    Text ___m_lblSectionName = (Text)SideLoader.At.GetField(CharUI, "m_lblSectionName");
                    MenuTab[] ___m_menuTabs = (MenuTab[])SideLoader.At.GetField(CharUI, "m_menuTabs");


                    // To change tab & tab name to Crafting (in proper language)
                    CharacterUI.MenuScreens _menu = CharacterUI.MenuScreens.Crafting;
                    for (int i = 0; i < ___m_menuTabs.Length; i++)
                    {
                        if (___m_menuTabs[i].Tab)
                        {
                            ___m_menuTabs[i].Tab.OnMenuDisplayed(_menu);
                            if (___m_menuTabs[i].Tab.LinkedMenuID == _menu && ___m_lblSectionName)
                            {
                                ___m_lblSectionName.text = LocalizationManager.Instance.GetLoc(___m_menuTabs[i].TabName);
                            }
                        }
                    }

                    ___m_activatedItemDisplay = null;
                    ___m_pendingItem = null;
                    CharUI.ContextMenu.Hide();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(EnvironmentItemDisplay))]
        public class EnvironmentItemDisplay_RefreshShowTakeAll
        {
            [HarmonyPrefix]
            [HarmonyPatch("RefreshShowTakeAll")]
            public static bool Prefix(EnvironmentItemDisplay __instance, ref List<ContainerDisplay> ___m_containerDisplayList, ref Button ___m_btnTakeAll)
            {
                if (___m_containerDisplayList[0] != null)
                {
                    if (CraftedBox != null && CraftedBox == ___m_containerDisplayList[0].ReferencedContainer)
                    {
                        RectTransform viewport = __instance.transform.FindInAllChildren("Viewport").GetComponent<RectTransform>();
                        viewport.anchoredPosition = new Vector2(0, 0);
                        viewport.offsetMin = new Vector2(0, 0);

                        ___m_btnTakeAll.gameObject.SetActive(false);
                        return false;
                    }
                    else
                    {
                        RectTransform viewport = __instance.transform.FindInAllChildren("Viewport").GetComponent<RectTransform>();
                        viewport.anchoredPosition = new Vector2(0, -45f);
                        viewport.offsetMin = new Vector2(0, 0);
                    }

                }
                return true;
            }
        }

        [HarmonyPatch(typeof(UIElement), "Hide")]
        public class UIElement_Hide
        {
            [HarmonyPrefix]
            public static void Prefix(UIElement __instance)
            {
                if (AssistantActive && (__instance is EnvironmentItemDisplay || __instance is CraftingMenu))
                    CloseAssistant();
            }
        }

        [HarmonyPatch(typeof(FooterPanel), "UpdateVisibility", new Type[] { typeof(bool) })]
        public class FooterPanel_UpdateVisibility
        {
            [HarmonyPostfix]
            public static void Postfix(FooterPanel __instance)
            {
                if (AssistantActive && __instance.LinkedMenuID == CharacterUI.MenuScreens.PreviewContainer)
                {
                    __instance?.Footer?.AcceptInputDisplay?.gameObject?.SetActive(false);
                }
            }
        }
    }
}
