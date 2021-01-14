// MyMod.cs
using System;
using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using SharedModConfig;

namespace NewGamePlus
{
    public class Settings
    {
        public static bool DisableNG = false;
        public static string DisableNG_Name = "DisableNG";

        public static bool DeleteKeys = true;
        public static string DeleteKeys_Name = "DeleteKeys";

        public static bool RemoveExalted = true;
        public static string RemoveExalted_Name = "RemoveExalted";
    }

    [BepInPlugin(ID, NAME, VERSION)]
    [BepInDependency("com.sinai.SharedModConfig", BepInDependency.DependencyFlags.HardDependency)]
    public class NewGamePlus : BaseUnityPlugin
    {
        const string ID = "com.random_facades.newgameplus";
        const string NAME = "New Game+";
        const string VERSION = "0.2";
        public static double VersionNum = double.Parse(VERSION);

        public static ModConfig config;

        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        public static Dictionary<string, int> ActiveLegacyLevels = new Dictionary<string, int>();
        public static NewGamePlus Instance;

        internal void Awake()
        {
            Instance = this;

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            logboy = Logger;
            logboy.Log(LogLevel.Message, "New Game Plus starting...");

            config = SetupConfig();
            config.Register();
            SettingsChanged();
            config.OnSettingsSaved += SettingsChanged;
        }

        private ModConfig SetupConfig()
        {
            var newConfig = new ModConfig
            {
                ModName = NAME,
                SettingsVersion = VersionNum,
                Settings = new List<BBSetting>
                {
                    new BoolSetting
                    {
                        Name = Settings.DisableNG_Name,
                        Description = "Disable Legacy Character Creation",
                        DefaultValue = false
                    },
                    new BoolSetting
                    {
                        SectionTitle = "Legacy Character Creation Settings",
                        Name = Settings.DeleteKeys_Name,
                        Description = "Delete Keys on Creation",
                        DefaultValue = true
                    },
                    new BoolSetting
                    {
                        Name = Settings.RemoveExalted_Name,
                        Description = "Remove Exalted & Life Drain on Creation",
                        DefaultValue = true
                    }
                }
            };

            return newConfig;
        }

        public static void SettingsChanged()
        {
            Settings.DisableNG = (bool)config.GetValue(Settings.DisableNG_Name);
            Settings.DeleteKeys = (bool)config.GetValue(Settings.DeleteKeys_Name);
            Settings.RemoveExalted = (bool)config.GetValue(Settings.RemoveExalted_Name);
        }

        public static bool setMaxStats = false;

        public static void CopySaveInfo()
        {
            if (itemList != null)
            {
                int legacyLevel = GetLegacyLevelFromPlayerSaveData(m_legacy.CharSave.PSave);
                legacyLevel++;
                Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                logboy.Log(LogLevel.Message, "Loading Legacy Gear from " + m_legacy.CharSave.PSave.Name);
                ActiveLegacyLevels[player.UID] = legacyLevel;

                player.Inventory.Pouch.SetSilverCount(m_legacy.CharSave.PSave.Money);

                typeof(CharacterRecipeKnowledge).GetMethod("LoadLearntRecipe", FLAGS).Invoke(player.Inventory.RecipeKnowledge, new object[] { m_legacy.CharSave.PSave.RecipeSaves });


                foreach (BasicSaveData data in itemList)
                {
                    Item item = ItemManager.Instance.GetItem(data.m_saveIdentifier.ToString());
                    if (item != null)
                    {
                        int loc = data.SyncData.IndexOf("<Hierarchy>");
                        if (loc != -1)
                        {
                            char type = data.SyncData.Substring(loc + 11, 1)[0];
                            if (type == '2')
                            {
                                item.ChangeParent(player.Inventory.Pouch.transform);
                                item.SetIsntNew();
                                Equipment clone = (Equipment)ItemManager.Instance.CloneItem(item);
                                clone.ChangeParent(player.Inventory.Pouch.transform);
                                player.Inventory.EquipItem(clone);
                                clone.ForceStartInit();

                                if (clone.GetType() == typeof(Bag))
                                {
                                    int loc1 = data.SyncData.IndexOf("BagSilver");
                                    if (loc1 != -1)
                                    {
                                        int len1 = data.SyncData.IndexOf(";", loc1) - loc1;
                                        string silver = data.SyncData.Substring(loc1 + 10, len1 - 10);
                                        if (int.TryParse(silver, out int money))
                                            ((Bag)clone).Container.SetSilverCount(money);
                                        else
                                            logboy.Log(LogLevel.Error, "Couldn't parse integer: " + silver);
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (BasicSaveData data in itemList)
                {
                    Item item = ItemManager.Instance.GetItem(data.m_saveIdentifier.ToString());
                    if (item == null)
                        logboy.Log(LogLevel.Error, "Couldn't get Item -- " + data.m_saveIdentifier.ToString());
                    else if (!(item is Quest) && (!Settings.DeleteKeys || !(item.GetType() == typeof(Item)) || !item.Name.Contains("Key")))
                    {
                        int loc = data.SyncData.IndexOf("<Hierarchy>");
                        if (loc != -1)
                        {
                            //logboy.Log(LogLevel.Message, "Item: " + item.GetType() + " - " + item.Name + " - " + item.name + " - " + data.SyncData);
                            int len = data.SyncData.IndexOf("<", loc + 11) - (loc + 11);
                            string type = data.SyncData.Substring(loc + 11, len);
                            if (type.StartsWith("1Pouch"))
                            {
                                item.ChangeParent(player.Inventory.Pouch.transform);
                                item.SetIsntNew();
                                Item clone = ItemManager.Instance.CloneItem(item);
                                if (item.RemainingAmount != clone.RemainingAmount)
                                    clone.RemainingAmount = item.RemainingAmount;
                                clone.ChangeParent(player.Inventory.Pouch.transform);
                                clone.ForceStartInit();
                            }
                            else
                            {
                                switch (type[0])
                                {
                                    case '1':
                                        item.ChangeParent(player.Inventory.Pouch.transform);
                                        player.Inventory.TakeItemToBag(item);
                                        item.ForceStartInit();
                                        break;
                                    case '2':
                                        // Do nothing, cause already equipped
                                        break;
                                    case '3':
                                        if (!typeof(Skill).IsAssignableFrom(item.GetType()))
                                            logboy.Log(LogLevel.Error, "Can't learn a non-skill: " + item.Name);
                                        else if (item.ItemID != 8100120 && item.ItemID != 8100010 && item.ItemID != 8100072 && item.ItemID != 8200600)
                                        {
                                            player.Inventory.TryUnlockSkill((Skill)item);
                                            if (!player.Inventory.LearnedSkill(item))
                                                logboy.Log(LogLevel.Error, "Failed to learn skill: " + item.Name);
                                        }
                                        break;
                                    default:
                                        logboy.Log(LogLevel.Error, "Unknown Item Detected: " + item.Name + " - " + data.SyncData);
                                        break;
                                }
                            }
                        }
                        else
                        {
                            logboy.Log(LogLevel.Error, "Hierarchy not found -- " + item.Name + " - " + data.SyncData);
                        }
                    }
                }

                player.ApplyQuicklots(m_legacy.CharSave.PSave);

                try
                {
                    int i = 0;
                    while (true)
                    {
                        QuickSlot slot = player.QuickSlotMngr.GetQuickSlot(i);
                        if (slot.ActiveItem is Skill)
                        {
                            Item skill = player.Inventory.SkillKnowledge.GetItemFromItemID(slot.ActiveItem.ItemID);
                            if (skill != null)
                                slot.SetQuickSlot(skill);
                            else
                                slot.CheckAndUpdateRefItem();
                        }
                        else
                        {
                            slot.CheckAndUpdateRefItem();
                        }

                        i++;
                    }
                }
                catch (Exception) { }

                PropertyInfo pi_DropBag = typeof(Character).GetProperty("HelpDropBagCount");
                pi_DropBag.SetValue(player, -legacyLevel);

                PropertyInfo pi_UseBandage = typeof(Character).GetProperty("HelpUseBandageCount");
                pi_UseBandage.SetValue(player, -legacyLevel);

                player.TargetingSystem.SetHelpLockCount(-legacyLevel);


                logboy.Log(LogLevel.Message, "Increasing Legacy Level to " + legacyLevel);


                itemList.Clear();
                itemList = null;
                m_legacy = null;

                setMaxStats = true;
            }
        }

        /*
        public static IEnumerator SetStatsToMax()
        {
            Character player = CharacterManager.Instance.GetFirstLocalCharacter();

            player.PlayerStats.RestoreAllVitals();


            yield return new WaitForSeconds(0.25f);
        }
        */

        // Copied from ItemManager
        private static string ItemSavesToString(BasicSaveData[] _itemSaves)
        {
            string text = "";
            for (int i = 0; i < _itemSaves.Length; i++)
            {
                string syncDataFromSaveData = Item.GetSyncDataFromSaveData(_itemSaves[i].SyncData);
                if (!string.IsNullOrEmpty(syncDataFromSaveData))
                {
                    text = text + syncDataFromSaveData + "~";
                }
            }
            return text;
        }

        public static ManualLogSource logboy;
        public static SaveInstance m_legacy;
        public static List<BasicSaveData> itemList;

        // To gain access to the legacy character save & to init item load
        [HarmonyPatch(typeof(CharacterCreationPanel), "OnConfirmSave")]
        public class CharacterCreationPanel_OnConfirmSave
        {
            [HarmonyPrefix]
            public static void Prefix(CharacterCreationPanel __instance, ref SaveInstance ___m_legacy)
            {
                if (!Settings.DisableNG && ___m_legacy != null)
                {
                    m_legacy = ___m_legacy;
                    itemList = new List<BasicSaveData>();
                    foreach (BasicSaveData data in ___m_legacy.CharSave.ItemList)
                        itemList.Add(data);

                    ItemManager.Instance.OnReceiveItemSync(ItemSavesToString(itemList.ToArray()), ItemManager.ItemSyncType.Character);
                }
            }
        }

        public static int GetLegacyLevelFromPlayerSaveData(PlayerSaveData save)
        {
            int ret = save.LegacyLevel;
            if (ret != 0) return ret;

            int bag = save.HelpDropBagCount;
            int bandage = save.HelpBandageCount;
            int lockon = save.HelpLockCount;

            if (bag < 0 && (bag == bandage || bag == lockon))
                return -bag;
            if (bandage < 0 && bandage == lockon)
                return -bandage;
            return 0;
        }

        public static void SaveLegacyLevelToPlayerSaveData(ref PlayerSaveData save, int level)
        {
            save.LegacyLevel = level;

            if (save.HelpDropBagCount == 0)
                save.HelpDropBagCount = -level;
            if (save.HelpBandageCount == 0)
                save.HelpBandageCount = -level;
            if (save.HelpLockCount == 0)
                save.HelpLockCount = -level;
        }

        // To trigger copying items to new character
        [HarmonyPatch(typeof(StartingEquipment), "Init")]
        public class StartingEquipment_Init
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                CopySaveInfo();

            }
        }

        // To change character creation settings to match Legacy Character
        [HarmonyPatch(typeof(CharacterCreationPanel), "OnLegacySelected", new Type[] { typeof(SaveInstance) })]
        public class CharacterCreationPanel_OnLegacySelected
        {
            [HarmonyPostfix]
            public static void Postfix(CharacterCreationPanel __instance, ref SaveInstance _instance)
            {
                // Character Creation Settings:
                //    Gender
                //    Race
                //    Face Style
                //    Hair Style
                //    Hair Color
                if (!Settings.DisableNG && _instance != null)
                {
                    CharacterVisualData legacy = _instance.CharSave.PSave.VisualData;

                    string legacyName = _instance.CharSave.PSave.Name;
                    string[] splits = legacyName.Split(' ');
                    if (splits[splits.Length - 1] == ToRoman(_instance.CharSave.PSave.LegacyLevel + 1))
                    {
                        legacyName = legacyName.Substring(0, legacyName.Length - (splits[splits.Length - 1].Length + 1));
                    }

                    __instance.OnCharacterNameEndEdit(legacyName + " " + ToRoman(_instance.CharSave.PSave.LegacyLevel + 2));

                    __instance.OnSexeChanged((int)legacy.Gender);
                    __instance.OnSkinChanged(legacy.SkinIndex);
                    __instance.OnHeadVariationChanged(legacy.HeadVariationIndex + 1);
                    __instance.OnHairStyleChanged(legacy.HairStyleIndex + 1);
                    __instance.OnHairColorChanged(legacy.HairColorIndex + 1);

                    FieldInfo fi_ccp;
                    CharacterCreationDisplay selector;

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_sexeSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetSelectedIndex((int)legacy.Gender);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_skinSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetSelectedIndex(legacy.SkinIndex);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_faceSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HeadVariationIndex + 1);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_hairStyleSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HairStyleIndex + 1);

                    fi_ccp = typeof(CharacterCreationPanel).GetField("m_hairColorSelector", FLAGS);
                    selector = (CharacterCreationDisplay)fi_ccp.GetValue(__instance);
                    selector.SetValue(legacy.HairColorIndex + 1);

                    typeof(CharacterCreationPanel).GetMethod("RefreshCharPreview", FLAGS).Invoke(__instance, new object[0]);
                }
            }
        }

        // Shamelessly stolen from https://stackoverflow.com/a/11749642 and modified
        public static string ToRoman(int number)
        {
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            return string.Empty;
        }

        // To fix issue with one of the 4 starter skills being in a quickslot when starting new game plus
        //     would cause the message of "skill not in inventory"
        [HarmonyPatch(typeof(QuickSlot), "Activate")]
        public class QuickSlot_Activate
        {
            [HarmonyPrefix]
            public static void Prefix(QuickSlot __instance, ref Character ___m_owner)
            {
                //logboy.Log(LogLevel.Message, "QuickSlot Activation for " + __instance.ActiveItem.Name + " - " + (__instance.ItemAsSkill == null));
                if (__instance.ActiveItem != null && !__instance.ItemIsSkill && __instance.ActiveItem is Skill)
                {
                    Item skill = ___m_owner.Inventory.SkillKnowledge.GetItemFromItemID(__instance.ActiveItem.ItemID);
                    if (skill != null)
                        __instance.SetQuickSlot(skill);
                    else
                        __instance.Clear();
                }
            }
        }

        // To save legacy level
        [HarmonyPatch(typeof(CharacterSave), "PrepareSave")]
        public class CharacterSave_PrepareSave
        {
            [HarmonyPostfix]
            public static void Postfix(CharacterSave __instance)
            {
                if(ActiveLegacyLevels.TryGetValue(__instance.CharacterUID, out int level))
                {
                    logboy.Log(LogLevel.Message, "Saving Legacy Level of " + level);
                    SaveLegacyLevelToPlayerSaveData(ref __instance.PSave, level);
                }
            }
        }

        // To load save's legacy level
        [HarmonyPatch(typeof(Character), "LoadPlayerSave", new Type[] { typeof(PlayerSaveData) })]
        public class Character_LoadPlayerSave
        {
            [HarmonyPostfix]
            public static void Postfix(ref PlayerSaveData _save)
            {
                int level = GetLegacyLevelFromPlayerSaveData(_save);
                if(level > 0)
                {
                    ActiveLegacyLevels[_save.UID] = level;
                    logboy.Log(LogLevel.Message, "Loaded Legacy Level of " + level);
                }
            }
        }

        // To set health to max when loading character in
        [HarmonyPatch(typeof(CharacterStats), "OnGameplayLoadingDone")]
        public class CharacterStats_OnGameplayLoadingDone
        {
            [HarmonyPostfix]
            public static void Postfix(CharacterStats __instance)
            {
                if (setMaxStats)
                {
                    logboy.Log(LogLevel.Message, "Resetting Stats");
                    setMaxStats = false;
                    __instance.RestoreAllVitals();
                }
            }
        }
    }
}