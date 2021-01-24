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
using UnityEngine.EventSystems;
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

        public static void AddItemToWrapper(string itemIDString)
        {
            Item item = ResourcesPrefabManager.Instance.GenerateItem(itemIDString);
            item.DoUpdate();
            item.RegisterUID();
            Instance.AddItem(item);
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
            ContainerWrapper.DestroyPouch();
            AssistantActive = false;
            CharUI.CloseAllMenus();
            CharUI = null;
            SelectedObject = null;
            SelectedItem = null;
        }

        /*
         * Crafting TODO:
         *   Load Extra Ingredients
         *   Force craft button disable (unless survival recipe)
         *   Hide/Remove free crafting
         *   Hide/Remove other recipes 
         *   Autoload targetted item
         * 
         * 
         * 
         * Tools:
         *   SetCraftButtonEnable
         *   SetRecipeResult
         *   RefreshItemDetailDisplay
         * 
         */

        public static CraftingMenu CraftMenu = null;
        public static Recipe.CraftingType[] CraftingTypes = { Recipe.CraftingType.Alchemy, Recipe.CraftingType.Cooking, Recipe.CraftingType.Survival };

        public static void LoadAllRecipesFor(ItemContainer container)
        {
            // TODO
            //   Show recipes from all crafting stations that the container has at least ONE ingredient for
            //   Change background image to match crafting station needed
            //   Confirm and VALIDATE that user cannot craft using this station

            Log("Selecting Container: " + container.Name);

            Character local = ((CharacterUI)SideLoader.At.GetField<UIElement>(CraftMenu, "m_characterUI"))?.TargetCharacter;


            List<Recipe> allRecipes = new List<Recipe>();
            foreach (Recipe.CraftingType type in CraftingTypes)
            {
                Log("Loading " + type.ToString() + " Recipes");
                allRecipes.AddRange(RecipeManager.Instance.GetRecipes(type, local));
            }
            allRecipes.Sort(new Comparison<Recipe>(Recipe.SortByName));
            SideLoader.At.SetField(CraftMenu, "m_allRecipes", allRecipes);
            Log("Loaded " + allRecipes.Count + " Recipes");

            SideLoader.At.SetField(CraftMenu, "m_refreshComplexeRecipeRequired", true);

            SideLoader.At.Invoke(CraftMenu, "RefreshAutoRecipe", new object[0]);
        }

        public static Item SelectedItem = null;

        public static void FilterRecipes(Item item)
        {
            // TODO
            //   Similar to above, just filter to any known recipes that include this item

            SelectedItem = item;
            int id = -1;
            if (SelectedItem != null)
                id = SelectedItem.ItemID;

            List<RecipeDisplay> recipeDisplays = (List<RecipeDisplay>)SideLoader.At.GetField(CraftMenu, "m_recipeDisplays");
            foreach (RecipeDisplay disp in recipeDisplays)
            {
                bool hasIngredient = false;

                IList<int> best = (IList<int>)SideLoader.At.GetField(disp, "m_bestIngredients");
                foreach (int val in best)
                    hasIngredient |= val == id;

                disp.gameObject.SetActive(hasIngredient);
            }
        }

        public static void OnChangeRecipe()
        {
            /*
             * CraftingMenu.Show()
             * 
	        if (this.m_singleIngredientBackground)
	        {
		        this.m_singleIngredientBackground.SetAlpha((float)(this.m_simpleMode ? 1 : 0));
	        }
	        if (this.m_multipleIngrenentsBrackground)
	        {
		        this.m_multipleIngrenentsBrackground.SetAlpha((float)((!this.m_simpleMode) ? 1 : 0));
	        }
	        for (int i = 1; i < this.m_ingredientSelectors.Length; i++)
	        {
		        this.m_ingredientSelectors[i].Show(!this.m_simpleMode);
	        }

	        int num = -1;
	        Sprite overrideSprite = null;
	        switch (this.m_craftingStationType)
	        {
	        case Recipe.CraftingType.Alchemy:
		        num = 0;
		        overrideSprite = this.m_alchemyCraftingBg;
		        break;
	        case Recipe.CraftingType.Cooking:
		        if (this.m_craftingStation.AllowComplexRecipe)
		        {
			        num = 3;
			        overrideSprite = this.m_cookingPotCraftingBg;
		        }
		        else
		        {
			        num = 2;
			        overrideSprite = this.m_cookingFireCraftingBg;
		        }
		        break;
	        case Recipe.CraftingType.Survival:
		        num = 1;
		        overrideSprite = this.m_survivalCraftingBg;
		        break;
	        case Recipe.CraftingType.Forge:
		        num = 4;
		        overrideSprite = this.m_alchemyCraftingBg;
		        break;
	        }
	        if (this.m_lblFreeRecipeDescription)
	        {
		        this.m_lblFreeRecipeDescription.text = LocalizationManager.Instance.GetLoc(CraftingMenu.m_freeRecipesLocKey[num]);
	        }
	        if (this.m_imgCraftingBackground)
	        {
		        this.m_imgCraftingBackground.overrideSprite = overrideSprite;
	        }
	        this.ResetFreeRecipeLastIngredients();
	        this.m_allRecipes = RecipeManager.Instance.GetRecipes(this.m_craftingStationType, base.LocalCharacter);
	        this.m_allRecipes.Sort(new Comparison<Recipe>(Recipe.SortByName));
	        this.m_refreshComplexeRecipeRequired = true;
	        this.RefreshAutoRecipe();
	        this.OnRecipeSelected(-1, true);
            */
        }

        [HarmonyPatch(typeof(CraftingMenu))]
        public class CraftingMenu_Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch("RefreshAutoRecipe")]
            public static void Postfix1(ref List<Recipe> ___m_allRecipes)
            {
                Log("CraftingMenu_Hooks + RefreshAutoRecipe -- " + ___m_allRecipes.Count);
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
                    {
                        ___m_availableIngredients.Values[i].Clear();
                    }
                    foreach (Recipe.CraftingType type in CraftingTypes)
                    {
                        Tag craftingIngredient = TagSourceManager.GetCraftingIngredient(type);
                        ___m_characterUI.TargetCharacter.Inventory.InventoryIngredients(craftingIngredient, ref ___m_availableIngredients);
                    }
                    return false;
                }
                return true;
            }
        }

        private static ItemContainer CreateContainerFor(Character player)
        {
            if (ContainerWrapper.Instance == null)
                ContainerWrapper.Init();

            List<Item> items = new List<Item>();

            foreach (Item item in player.Inventory.Pouch.GetContainedItems())
                items.Add(item);

            if (player.Inventory.EquippedBag?.Container != null)
                foreach (Item item in player.Inventory.EquippedBag.Container.GetContainedItems())
                    items.Add(item);

            EquipmentSlot[] slots = (EquipmentSlot[])SideLoader.At.GetField(player.Inventory.Equipment, "m_equipmentSlots");
            foreach (EquipmentSlot slot in slots)
                if (slot?.EquippedItem != null)
                    items.Add(slot.EquippedItem);

            DictionaryExt<int, CompatibleIngredient> possibleIngredients = new DictionaryExt<int, CompatibleIngredient>();

            foreach (Recipe.CraftingType type in CraftingTypes)
                InventoryIngredients(TagSourceManager.GetCraftingIngredient(type), ref possibleIngredients, items);

            foreach (int key in possibleIngredients.Keys)
                ContainerWrapper.AddItemToWrapper(key.ToString());

            return ContainerWrapper.Instance;
        }

        // Just stole this from CharacterInventory because reflection calls don't like the ref
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

        [HarmonyPatch(typeof(ItemDisplayClick), "OnPointerClick")]
        public class ItemDisplayClick_OnPointerClick
        {
            [HarmonyPrefix]
            public static bool Prefix(ItemDisplayClick __instance)
            {
                return !AssistantActive;
            }
        }

        public static Selectable SelectedObject = null;
        public static GameObject highlight = null;

        [HarmonyPatch(typeof(Selectable))]
        public class Selectable_Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch("UpdateSelectionState")]
            public static void Postfix2(Selectable __instance, BaseEventData eventData, ref int ___m_CurrentSelectionState)
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
