using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SideLoader;
using SharedModConfig;
using UnityEngine.UI;
using System.IO;

namespace MoneyTooltip
{
    public class Settings
    {
        public static bool DisableIPH = false;
        public static string DisableIPH_Name = "DisableIPH";

        public static bool RemoveTilde = false;
        public static string RemoveTilde_Name = "RemoveTilde";

        public static bool ShowValue = false;
        public static string ShowValue_Name = "ShowValue";

        public static ModConfig Instance;

        public static void OnChanged()
        {
            DisableIPH = (bool)Instance.GetValue(DisableIPH_Name);
            RemoveTilde = (bool)Instance.GetValue(RemoveTilde_Name);
            ShowValue = (bool)Instance.GetValue(ShowValue_Name);
        }

        public static void SetupConfig()
        {
            var newConfig = new ModConfig
            {
                ModName = ItemPriceHelper.NAME,
                SettingsVersion = ItemPriceHelper.VersionNum,
                Settings = new List<BBSetting>
                {
                    new BoolSetting
                    {
                        Name = DisableIPH_Name,
                        Description = "Disable Item Price Labels",
                        DefaultValue = false
                    },
                    new BoolSetting
                    {
                        Name = RemoveTilde_Name,
                        Description = "Remove \"~\" in Labels",
                        DefaultValue = false
                    },
                    new BoolSetting
                    {
                        Name = ShowValue_Name,
                        Description = "Show Value in Labels (Price / Weight) instead of Price",
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
    public class ItemPriceHelper : BaseUnityPlugin
    {
        public const string ID = "com.random_facades.itempricehelper";
        public const string NAME = "Item Price Helper";
        public const string VERSION = "1.1";
        public static double VersionNum = 1.1;

        public static ItemPriceHelper Instance;

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            Settings.SetupConfig();

            Log("Item Price Helper starting...");

            AttemptToLoadResources();
        }

        public static Texture2D SilverTex = null;
        public static Sprite m_silverSprite = null;
        public static Sprite SilverSprite
        {
            get
            {
                if (m_silverSprite == null && SilverTex != null)
                    m_silverSprite = Sprite.Create(SilverTex, new Rect(0.0f, 0.0f, SilverTex.width, SilverTex.height), new Vector2(0.5f, 0.5f));
                return m_silverSprite;
            }
        }

        public static Texture2D WeightTex = null;
        public static Sprite m_weightSprite = null;
        public static Sprite WeightSprite
        {
            get
            {
                if (m_weightSprite == null && WeightTex != null)
                    m_weightSprite = Sprite.Create(WeightTex, new Rect(0.0f, 0.0f, WeightTex.width, WeightTex.height), new Vector2(0.5f, 0.5f));
                return m_weightSprite;
            }
        }

        public static void AttemptToLoadResources()
        {
            WeightTex = CustomTextures.LoadTexture(Directory.GetCurrentDirectory() + @"\BepInEx\plugins\ItemPriceHelper\tex_men_weightIcon.png", false, false);
            if (WeightTex != null && WeightSprite != null)
                Log("Loaded Weight Sprite Successfully!");
            else
                logboy.Log(LogLevel.Error, "Error while loading Weight Sprite");

            SilverTex = CustomTextures.LoadTexture(Directory.GetCurrentDirectory() + @"\BepInEx\plugins\ItemPriceHelper\tex_men_coinIcon.png", false, false);
            if (SilverTex != null && SilverSprite != null)
                Log("Loaded Silver Sprite Successfully!");
            else
                logboy.Log(LogLevel.Error, "Error while loading Silver Sprite");
        }

        public static ManualLogSource logboy;

        public static void Log(string message)
        {
            if (logboy != null)
                logboy.Log(LogLevel.Message, message);
        }

        public static void SetSpriteTo(GameObject obj, Sprite spr)
        {
            Image[] icons = obj.GetComponentsInChildren<Image>();
            foreach (Image icon in icons)
                if (icon.name == "CoinIcon" && icon.sprite != spr)
                    icon.sprite = spr;
        }

        [HarmonyPatch(typeof(ItemDisplay), "UpdateValueDisplay")]
        public class ItemDisplay_UpdateValueDisplay
        {
            [HarmonyPostfix]
            public static void Postfix(ItemDisplay __instance, ref GameObject ___m_valueHolder, ref Text ___m_lblValue)
            {
                // debugging fun

                if (___m_lblValue != null && ___m_valueHolder != null)
                {
                    if (!___m_valueHolder.activeSelf)
                    {
                        if (__instance.RefItem != null && __instance.RefItem.IsSellable && !(__instance.RefItem is Skill))
                        {
                            float num = (float)At.GetField(__instance.RefItem, "m_overrideSellModifier");
                            if (num == -1)
                            {
                                num = 1 + __instance.CharacterUI.TargetCharacter.GetItemSellPriceModifier(null, __instance.RefItem);
                                num *= 0.3f;
                            }

                            float price = num * __instance.RefItem.RawCurrentValue;

                            string labelText = "~";
                            if (Settings.RemoveTilde)
                                labelText = "";
                            if (Settings.ShowValue)
                            {
                                float actualWeight = Math.Min(__instance.RefItem.Weight, __instance.RefItem.RawWeight);
                                if (actualWeight == 0)
                                    labelText = '\u221E' + "x";
                                else
                                {
                                    float value = price / actualWeight;
                                    if (value > 10)
                                        labelText = Mathf.RoundToInt(value).ToString() + "x";
                                    else
                                        labelText = (Mathf.RoundToInt(value * 10) / 10f).ToString() + "x";
                                }
                                SetSpriteTo(___m_valueHolder, WeightSprite);
                            }
                            else
                            {
                                SetSpriteTo(___m_valueHolder, SilverSprite);
                                labelText += Mathf.RoundToInt(price).ToString();
                            }

                            ___m_valueHolder.SetActive(true);
                            ___m_lblValue.text = labelText;
                        }
                    }
                    else
                        SetSpriteTo(___m_valueHolder, SilverSprite);
                }
            }
        }
    }
}
