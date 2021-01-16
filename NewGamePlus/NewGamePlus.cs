﻿// MyMod.cs

/*   -------------  TODO LIST  -------------
 *   
 *   * Stacking Status Effect for difficulty
 *   
 *   
 *   
 *   -------------  TOLEARN LIST  -------------
 *   
 *   * Status Effect creation through SideLoader
 *   * Probably more stuff, but forgot
 *   
 *   
 */

using System;
using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using SharedModConfig;
using SideLoader.SaveData;
using System.Collections;
using UnityEngine;

namespace NewGamePlus
{
    public class Settings
    {
        public static bool DisableNG = false;
        public static string DisableNG_Name = "DisableNG";

        public static bool DeleteKeys = true;
        public static string DeleteKeys_Name = "DeleteKeys";

        public static bool TransferExalted = false;
        public static string TransferExalted_Name = "TransferExalted";
    }

    // Class is destroyed after any function
    //    Well... not literally, but treat it like that
    public class NewGameExtension : PlayerSaveExtension
    {
        public int LegacyLevel;
        public int[] LegacySkills;

        // Save Variables so that class can be Serialized
        public override void Save(Character character, bool isWorldHost)
        {
            if (NewGamePlus.ActiveLegacyLevels.TryGetValue(character.UID, out int level))
                LegacyLevel = level;
            if (NewGamePlus.ActiveLegacySkills.TryGetValue(character.UID, out int[] skills))
                LegacySkills = skills;
        }

        // Variables have been init'd, apply them to world/char/mod
        public override void ApplyLoadedSave(Character character, bool isWorldHost)
        {
            // With Pre 0.2 Characters, LegacyLevel will be initialized in ActiveLegacyLevels by Character_LoadPlayerSave
            //    Character_LoadPlayerSave is ran before this
            if (!NewGamePlus.ActiveLegacyLevels.TryGetValue(character.UID, out _) || LegacyLevel != 0)
                NewGamePlus.ActiveLegacyLevels[character.UID] = LegacyLevel;

            if (LegacySkills != null)
                NewGamePlus.ActiveLegacySkills[character.UID] = LegacySkills;

            NewGamePlus.logboy.Log(LogLevel.Message, "Loaded Legacy Level for " + character.Name + ": " + LegacyLevel);
            if (NewGamePlus.ActiveLegacySkills.TryGetValue(character.UID, out int[] temp) && temp?.Length > 0)
                NewGamePlus.logboy.Log(LogLevel.Message, "Loaded LegacySkills for " + character.Name + ": " + temp.Length);
        }

        /*
        public static NewGameExtension LoadSaveExtensionFor(string uid)
        {
            string customFolder = (string)At.GetFieldStatic(typeof(SLSaveManager), "CUSTOM_FOLDER");
            string saveDataFolder = (string)At.GetFieldStatic(typeof(SLSaveManager), "SAVEDATA_FOLDER");
            string dir = $@"{saveDataFolder}\{uid}\{customFolder}\";

            foreach (string fileName in Directory.GetFiles(dir))
            {
                string typename = Serializer.GetBaseTypeOfXmlDocument(fileName);
                if (typeof(NewGameExtension).Name == typename)
                {
                    System.Xml.Serialization.XmlSerializer serializer = Serializer.GetXmlSerializer(typeof(NewGameExtension));
                    using (FileStream xml = File.OpenRead(fileName))
                    {
                        try
                        {
                            if (serializer.Deserialize(xml) is NewGameExtension ext)
                                return ext;
                            else
                                NewGamePlus.Log("Couldn't load NewGameExtension XML!");
                        }
                        catch (Exception)
                        {
                            NewGamePlus.Log("Exception loading NewGameExtension XML!");
                        }
                    }
                }
            }

            return null;
        }
        */
    }

    [BepInPlugin(ID, NAME, VERSION)]
    [BepInDependency("com.sinai.SharedModConfig", BepInDependency.DependencyFlags.HardDependency)]
    public class NewGamePlus : BaseUnityPlugin
    {
        const string ID = "com.random_facades.newgameplus";
        const string NAME = "New Game+";
        const string VERSION = "0.3";
        public static double VersionNum = double.Parse(VERSION);

        public static ModConfig config;

        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        public static Dictionary<string, int> ActiveLegacyLevels = new Dictionary<string, int>();
        public static Dictionary<string, int[]> ActiveLegacySkills = new Dictionary<string, int[]>();
        public static NewGamePlus Instance;
        public static NewGameExtension SaveExt;

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            StatusEffectManager.InitializeEffects();

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            config = SetupConfig();
            config.Register();
            SettingsChanged();
            config.OnSettingsSaved += SettingsChanged;

            Log("New Game Plus starting...");
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
                        Name = Settings.TransferExalted_Name,
                        Description = "Transfer Exalted & Life Drain on Creation",
                        DefaultValue = false
                    }
                }
            };

            return newConfig;
        }

        public static void SettingsChanged()
        {
            Settings.DisableNG = (bool)config.GetValue(Settings.DisableNG_Name);
            Settings.DeleteKeys = (bool)config.GetValue(Settings.DeleteKeys_Name);
            Settings.TransferExalted = (bool)config.GetValue(Settings.TransferExalted_Name);
        }

        public static bool setMaxStats = false;

        public static void CreateNewCharacter()
        {
            if (itemList != null)
            {
                int legacyLevel = m_legacy.CharSave.PSave.LegacyLevel;
                if (legacyLevel == 0)
                    legacyLevel = GetLegacyLevelFor(m_legacy.CharSave.CharacterUID);
                legacyLevel++;
                Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                logboy.Log(LogLevel.Message, "Loading Legacy Gear from " + m_legacy.CharSave.PSave.Name);
                ActiveLegacyLevels[player.UID] = legacyLevel;
                List<int> legacySkills = new List<int>();

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
                                        if (!(item is Skill))
                                        {
                                            logboy.Log(LogLevel.Error, "Can't learn a non-skill: " + item.Name);
                                            break;
                                        }
                                        // Check for Push Kick, Throw Lantern, Fire/Reload, or Dagger Slash (tutorial event will give duplicates)
                                        if (item.ItemID == 8100120 || item.ItemID == 8100010 || item.ItemID == 8100072 || item.ItemID == 8200600)
                                            break;
                                        // Check for Exalted, and remove it or add LifeDrain
                                        if (item.ItemID == 8205999)
                                        {
                                            if (!Settings.TransferExalted)
                                                break;
                                            foreach (BasicSaveData status in m_legacy.CharSave.PSave.StatusList)
                                            {
                                                if (status != null && !string.IsNullOrEmpty(status.SyncData))
                                                {
                                                    string _statusPrefabName = status.Identifier.ToString();
                                                    if (_statusPrefabName.Contains("Life Drain"))
                                                    {
                                                        string[] _splitData = StatusEffect.SplitNetworkData(status.SyncData);
                                                        StatusEffect statusEffectPrefab = ResourcesPrefabManager.Instance.GetStatusEffectPrefab(_statusPrefabName);
                                                        player.StatusEffectMngr.AddStatusEffect(statusEffectPrefab, null, _splitData);
                                                        break;
                                                    }
                                                }
                                            }

                                        }

                                        player.Inventory.TryUnlockSkill((Skill)item);
                                        if (!player.Inventory.LearnedSkill(item))
                                            logboy.Log(LogLevel.Error, "Failed to learn skill: " + item.Name);
                                        else
                                            legacySkills.Add(item.ItemID);

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
                pi_DropBag.SetValue(player, m_legacy.CharSave.PSave.HelpDropBagCount);

                PropertyInfo pi_UseBandage = typeof(Character).GetProperty("HelpUseBandageCount");
                pi_UseBandage.SetValue(player, m_legacy.CharSave.PSave.HelpBandageCount);

                player.TargetingSystem.SetHelpLockCount(m_legacy.CharSave.PSave.HelpLockCount);

                ActiveLegacySkills[player.UID] = legacySkills.ToArray();
                logboy.Log(LogLevel.Message, "Increasing Legacy Level to " + legacyLevel);


                itemList.Clear();
                itemList = null;
                m_legacy = null;

                setMaxStats = true;
            }
        }

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

        public static void Log(string message)
        {
            logboy.Log(LogLevel.Message, message);
        }

        public static int GetLegacyLevelFor(string uid)
        {
            if (!ActiveLegacyLevels.TryGetValue(uid, out int level))
            {
                NewGameExtension nge = PlayerSaveExtension.TryLoadExtension<NewGameExtension>(uid);
                if (nge != null)
                    level = nge.LegacyLevel;
                ActiveLegacyLevels[uid] = level;
            }
            return level;
        }

        private static bool CharacterHasLegacySkill(Character character, Skill skill)
        {
            return ActiveLegacySkills.TryGetValue(character.UID, out int[] skills) && skills.Contains(skill.ItemID);
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

        // To trigger copying items to new character
        [HarmonyPatch(typeof(StartingEquipment), "Init")]
        public class StartingEquipment_Init
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                CreateNewCharacter();
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
                    int legacyLevel = _instance.CharSave.PSave.LegacyLevel;
                    if (legacyLevel == 0)
                        legacyLevel = GetLegacyLevelFor(_instance.CharSave.CharacterUID);

                    string legacyName = _instance.CharSave.PSave.Name;
                    string[] splits = legacyName.Split(' ');
                    if (splits[splits.Length - 1] == ToRoman(legacyLevel + 1))
                    {
                        legacyName = legacyName.Substring(0, legacyName.Length - (splits[splits.Length - 1].Length + 1));
                    }

                    __instance.OnCharacterNameEndEdit(legacyName + " " + ToRoman(legacyLevel + 2));

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

        // LEGACY METHOD OF LOADING LegacyLevel
        //    Can't remove due to breaking older games
        [HarmonyPatch(typeof(Character), "LoadPlayerSave", new Type[] { typeof(PlayerSaveData) })]
        public class Character_LoadPlayerSave
        {
            [HarmonyPostfix]
            public static void Postfix(ref PlayerSaveData _save)
            {
                int level = _save.LegacyLevel;
                if (level > 0)
                {
                    ActiveLegacyLevels[_save.UID] = level;
                }
            }
        }

        // To set health to max when loading character in
        [HarmonyPatch(typeof(NetworkLevelLoader), "OnReportLoadingProgress", new Type[] { typeof(float) })]
        public class NetworkLevelLoader_OnReportLoadingProgress
        {
            [HarmonyPrefix]
            public static void Prefix(NetworkLevelLoader __instance, ref float _progress)
            {


                if(_progress >= 1f)
                {
                    Character player = CharacterManager.Instance.GetFirstLocalCharacter();
                    if (setMaxStats)
                    {
                        logboy.Log(LogLevel.Message, "Resetting Stats");
                        player.Stats.RestoreAllVitals();
                        setMaxStats = false;

                    }
                    // Do special stuff for legacy characters
                    if (ActiveLegacyLevels.TryGetValue(player.UID, out int level) && level > 0)
                    {
                        // Check if ActiveLegacySkills has the value, if not create it
                        if (!ActiveLegacySkills.TryGetValue(player.UID, out _))
                        {
                            List<int> skills = new List<int>();
                            foreach (Item item in player.Inventory.SkillKnowledge.GetLearnedItems())
                            {
                                if (!skills.Contains(item.ItemID))
                                    skills.Add(item.ItemID);
                            }
                            ActiveLegacySkills[player.UID] = skills.ToArray();
                            Log("Loaded LegacySkills for " + player.Name + ": " + skills.Count);
                        }

                        // Check if debuffs exist, if not apply them
                        int i = 0;
                        StatusEffect temp = player.StatusEffectMngr.GetStatusEffectOfName("Stretched Thin");
                        if (temp != null)
                            i = temp.StackCount;
                        for (; i < level; i++)
                            player.StatusEffectMngr.AddStatusEffect("Stretched Thin");
                    }
                }


            }
        }

        // to allow purchasing of ALL SKILLS!!!
        [HarmonyPatch(typeof(SkillSlot), "IsBlocked", new Type[] { typeof(Character), typeof(bool) })]
        public class SkillSlot_IsBlocked
        {
            [HarmonyPrefix]
            public static bool Prefix(SkillSlot __instance, ref bool __result, Character _character, ref bool _notify)
            {
                // If you gained this skill as a legacy skill, then show it as blocked
                if (NewGamePlus.CharacterHasLegacySkill(_character, __instance.Skill))
                {
                    __result = true;
                    return false;
                }

                if (__instance.SiblingSlot != null && NewGamePlus.CharacterHasLegacySkill(_character, __instance.SiblingSlot.Skill))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}