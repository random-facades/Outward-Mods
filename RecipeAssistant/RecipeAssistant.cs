// MyMod.cs

/*   -------------  TODO LIST  -------------
 *     
 *     Initialize Recipe Assistant with item that was right clicked
 *     
 *     MAYBE FEATURES:
 *         Instead of Crafting button, Favorite/Unfavorite the recipe
 *           And show a star or something next to the recipe when next crafting
 *           Remove it once it has been crafted
 *     
 *   -------------  TOLEARN LIST  -------------
 *     
 *     How UI Creation/Displaying works
 *     Probably more stuff, but forgot
 *     
 *   -------------  TOFIX (eventually) LIST  -------------
 *     
 *     The Stash ???
 *        what was wrong with the stash?
 *     Get UI to focus on whichever panel is below cursor (mainly for scrolling)
 *     
 */

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecipeAssistant
{
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

        public static void AddItemToWrapper(string itemIDString)
        {
            Item item = ResourcesPrefabManager.Instance.GenerateItem(itemIDString);
            item.DoUpdate();
            item.RegisterUID();
            Instance.AddItem(item);
        }
    }

    [BepInPlugin(ID, NAME, VERSION)]
    public class RecipeAssistant : BaseUnityPlugin
    {
        public const string ID = "com.random_facades.recipeassistant";
        public const string NAME = "Recipe Assistant";
        public const string VERSION = "1.0";
        public static double VersionNum = 1.0;

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

            Log("Recipe Assistant starting...");
        }

        private static void CloseAssistant()
        {
            ContainerWrapper.DestroyPouch();
            AssistantActive = false;

            Button butt = ((RecipeResultDisplay)SideLoader.At.GetField(CraftMenu, "m_recipeResultDisplay"))?.GetComponent<Button>();
            if (butt != null)
                butt.enabled = true;

            highlight?.SetActive(false);

            CharUI.CloseAllMenus();
            CharUI = null;
            SelectedObject = null;
            SelectedItem = null;
        }

        public static CraftingMenu CraftMenu = null;
        public static Recipe.CraftingType[] CraftingTypes = { Recipe.CraftingType.Alchemy, Recipe.CraftingType.Cooking, Recipe.CraftingType.Survival };

        public static void LoadAllRecipesFor(ItemContainer container)
        {
            Character local = ((CharacterUI)SideLoader.At.GetField<UIElement>(CraftMenu, "m_characterUI"))?.TargetCharacter;

            List<Recipe> allRecipes = new List<Recipe>();
            foreach (Recipe.CraftingType type in CraftingTypes)
                allRecipes.AddRange(RecipeManager.Instance.GetRecipes(type, local));

            allRecipes.Sort(new Comparison<Recipe>(Recipe.SortByName));
            SideLoader.At.SetField(CraftMenu, "m_allRecipes", allRecipes);

            SideLoader.At.SetField(CraftMenu, "m_refreshComplexeRecipeRequired", true);

            SideLoader.At.Invoke(CraftMenu, "RefreshAutoRecipe", new object[0]);
        }

        public static Item SelectedItem = null;

        public static void FilterRecipes(Item item)
        {
            SelectedItem = item;
            int id = -1;
            if (SelectedItem != null)
                id = SelectedItem.ItemID;

            List<RecipeDisplay> recipeDisplays = (List<RecipeDisplay>)SideLoader.At.GetField(CraftMenu, "m_recipeDisplays");
            foreach (RecipeDisplay disp in recipeDisplays)
            {
                bool hasIngredient = false;

                if (id == -1)
                {
                    IList<int> best = (IList<int>)SideLoader.At.GetField(disp, "m_bestIngredients");
                    foreach (int val in best)
                        hasIngredient |= val != -1;
                }
                else
                {
                    List<int>[] compatible = (List<int>[])SideLoader.At.GetField(disp, "m_compatibleIngredients");
                    foreach (List<int> val in compatible)
                    {
                        if (val.Contains(id))
                        {
                            hasIngredient = true;
                            break;
                        }
                    }
                }
                disp.gameObject.SetActive(hasIngredient);
            }
            CraftMenu.OnRecipeSelected(-1);
        }

        private static List<Item> GetAllItemsFrom(Character player)
        {
            List<Item> items = new List<Item>();

            foreach (Item item in player.Inventory.Pouch.GetContainedItems())
                items.Add(item);

            if (player.Inventory.EquippedBag?.Container != null)
                foreach (Item item in player.Inventory.EquippedBag.Container.GetContainedItems())
                    items.Add(item);


            EquipmentSlot[] slots = player.Inventory.Equipment.EquipmentSlots;
            foreach (EquipmentSlot slot in slots)
                if (slot?.EquippedItem != null)
                    items.Add(slot.EquippedItem);
            return items;
        }

        private static ItemContainer CreateContainerFor(Character player)
        {
            if (ContainerWrapper.Instance == null)
                ContainerWrapper.Init();

            DictionaryExt<int, CompatibleIngredient> possibleIngredients = new DictionaryExt<int, CompatibleIngredient>();

            foreach (Recipe.CraftingType type in CraftingTypes)
                InventoryIngredients(TagSourceManager.GetCraftingIngredient(type), ref possibleIngredients, GetAllItemsFrom(player));

            foreach (int key in possibleIngredients.Keys)
                ContainerWrapper.AddItemToWrapper(key.ToString());

            return ContainerWrapper.Instance;
        }

        private static void InventoryIngredients(Tag _craftingStationTag, ref DictionaryExt<int, CompatibleIngredient> _sortedIngredient, IList<Item> _items)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].IsEnchanted && _items[i].HasTag(_craftingStationTag))
                {
                    CompatibleIngredient compatibleIngredient = null;
                    if (!(_items[i] is WaterContainer) || ((WaterContainer)_items[i]).RemainingUse == 0)
                    {
                        if (!_sortedIngredient.TryGetValue(_items[i].ItemID, out compatibleIngredient))
                        {
                            compatibleIngredient = new CompatibleIngredient(_items[i].ItemID);
                            _sortedIngredient.Add(_items[i].ItemID, compatibleIngredient);
                        }
                    }
                    else
                    {
                        Item waterItem = WaterItem.GetWaterItem(((WaterContainer)_items[i]).ContainedWater);
                        if (waterItem && !_sortedIngredient.TryGetValue(waterItem.ItemID, out compatibleIngredient))
                        {
                            compatibleIngredient = new CompatibleIngredient(waterItem.ItemID);
                            _sortedIngredient.Add(waterItem.ItemID, compatibleIngredient);
                        }
                    }
                    if (compatibleIngredient != null)
                    {
                        compatibleIngredient.AddOwnedItem(_items[i]);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CraftingMenu))]
        public class CraftingMenu_Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch("RefreshAutoRecipe")]
            public static void Postfix1(ref List<Recipe> ___m_allRecipes)
            {
                if (AssistantActive)
                    FilterRecipes(SelectedItem);
            }

            [HarmonyPrefix]
            [HarmonyPatch("RefreshAvailableIngredients")]
            public static bool Prefix2(ref DictionaryExt<int, CompatibleIngredient> ___m_availableIngredients, CharacterUI ___m_characterUI)
            {
                if (AssistantActive)
                {
                    for (int i = 0; i < ___m_availableIngredients.Count; i++)
                        ___m_availableIngredients.Values[i].Clear();

                    foreach (Recipe.CraftingType type in CraftingTypes)
                        InventoryIngredients(TagSourceManager.GetCraftingIngredient(type), ref ___m_availableIngredients, GetAllItemsFrom(___m_characterUI.TargetCharacter));
                    return false;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("TryCraft")]
            public static bool Prefix4()
            {
                return !AssistantActive;
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnRecipeSelected")]
            public static void Postfix4(CraftingMenu __instance, int ___m_lastRecipeIndex, ref List<KeyValuePair<int, Recipe>> ___m_complexeRecipes, ref Image ___m_imgCraftingBackground)
            {
                Recipe.CraftingType active = Recipe.CraftingType.Survival;
                if (___m_lastRecipeIndex != -1)
                    active = ___m_complexeRecipes[___m_lastRecipeIndex].Value.CraftingStationType;


                if (___m_imgCraftingBackground)
                {
                    Sprite overrideSprite;
                    switch (active)
                    {
                        case Recipe.CraftingType.Alchemy:
                            overrideSprite = (Sprite)SideLoader.At.GetField(__instance, "m_alchemyCraftingBg");
                            break;
                        case Recipe.CraftingType.Cooking:
                            overrideSprite = (Sprite)SideLoader.At.GetField(__instance, "m_cookingPotCraftingBg");
                            break;
                        case Recipe.CraftingType.Survival:
                        default:
                            overrideSprite = (Sprite)SideLoader.At.GetField(__instance, "m_survivalCraftingBg");
                            break;
                    }
                    ___m_imgCraftingBackground.overrideSprite = overrideSprite;
                }
            }
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
                    CraftMenu = (CraftingMenu)m_menus[(int)CharacterUI.MenuScreens.Crafting];

                    CraftedBox = CreateContainerFor(player);
                    environ.Show(CraftedBox);
                    ((Text)SideLoader.At.GetField(environ, "m_lblTitle")).text = "Ingredients";
                    ((Button)SideLoader.At.GetField(environ, "m_btnTakeAll")).gameObject.SetActive(false);
                    environ.SetIsParallel(CharacterUI.MenuScreens.Inventory);

                    CraftMenu.Show();

                    RecipeResultDisplay recipeResult = (RecipeResultDisplay)SideLoader.At.GetField(CraftMenu, "m_recipeResultDisplay");
                    Button butt = recipeResult.GetComponent<Button>();
                    butt.enabled = false;

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
                                ___m_lblSectionName.text = LocalizationManager.Instance.GetLoc(___m_menuTabs[i].TabName);
                        }
                    }

                    LoadAllRecipesFor(CraftedBox);

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
            public static bool Prefix1(EnvironmentItemDisplay __instance, ref List<ContainerDisplay> ___m_containerDisplayList, ref Button ___m_btnTakeAll)
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

            [HarmonyPrefix]
            [HarmonyPatch("OnItemSelected", new Type[] { typeof(IItemDisplay) })]
            public static bool Prefix2()
            {
                return !AssistantActive;
            }
        }

        [HarmonyPatch(typeof(WorldSave), "PrepareSave")]
        public class WorldSave_PrepareSave
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if (AssistantActive)
                    CloseAssistant();
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

        [HarmonyPatch(typeof(CookingFooterPanel), "UpdateVisibility", new Type[] { typeof(bool) })]
        public class CookingFooterPanel_UpdateVisibility
        {
            [HarmonyPostfix]
            public static void Postfix(CookingFooterPanel __instance)
            {
                if (AssistantActive)
                    __instance?.Footer?.InfoInputDisplay?.gameObject?.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(FooterPanel), "UpdateVisibility", new Type[] { typeof(bool) })]
        public class FooterPanel_UpdateVisibility
        {
            [HarmonyPostfix]
            public static void Postfix(FooterPanel __instance)
            {
                if (AssistantActive && __instance.LinkedMenuID == CharacterUI.MenuScreens.PreviewContainer)
                    __instance?.Footer?.AcceptInputDisplay?.gameObject?.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(ItemDisplayClick), "OnPointerClick")]
        public class ItemDisplayClick_OnPointerClick
        {
            [HarmonyPrefix]
            public static bool Prefix(ItemDisplayClick __instance)
            {
                return !AssistantActive || __instance.GetComponent<ItemDisplay>()?.ParentDisplay?.GetType() == typeof(ItemListSelector);
            }
        }

        public static Selectable SelectedObject = null;
        public static GameObject highlight = null;

        [HarmonyPatch(typeof(Selectable))]
        public class Selectable_Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch("UpdateSelectionState")]
            public static void Postfix1(Selectable __instance)
            {
                if (AssistantActive && __instance.transform.FindParentByName("Environment") != null)
                    SelectAndHighlightObject(__instance);
            }

            public static void SelectAndHighlightObject(Selectable obj)
            {
                ItemDisplay display = obj.gameObject.GetComponent<ItemDisplay>();
                int currentSelectionState = (int)SideLoader.At.GetField(obj, "m_CurrentSelectionState");
                if (display?.RefItem != null && currentSelectionState == 2)
                {
                    if (obj == SelectedObject)
                    {
                        SelectedObject = null;
                        FilterRecipes(null);
                        highlight.SetActive(false);
                    }
                    else
                    {
                        FilterRecipes(display.RefItem);
                        SelectedObject = obj;

                        if (highlight == null)
                        {
                            GameObject original = obj.gameObject.transform.FindInAllChildren("imgHighlight").gameObject;
                            highlight = Instantiate(original, new Vector3(0, 0, 0), Quaternion.identity);
                            highlight.name = "Test_Highlight";
                        }

                        highlight.transform.parent = SelectedObject.transform;
                        highlight.SetActive(true);
                        RectTransform highRect = highlight.GetComponent<RectTransform>();
                        highRect.anchoredPosition = new Vector2(0, 0);
                        highRect.offsetMax = new Vector2(5, 5);
                        highRect.offsetMin = new Vector2(-5, -5);
                    }
                }
            }
        }
    }
}
