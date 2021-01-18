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

namespace MoneyTooltip
{
    [BepInPlugin(ID, NAME, VERSION)]
    public class ItemPriceHelper : BaseUnityPlugin
    {
        const string ID = "com.random_facades.itempricehelper";
        const string NAME = "Item Price Helper";
        const string VERSION = "1.0";
        public static double VersionNum = double.Parse(VERSION);

        public static ItemPriceHelper Instance;

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            Log("Item Price Helper starting...");
        }

        public static ManualLogSource logboy;

        public static void Log(string message)
        {
            logboy.Log(LogLevel.Message, message);
        }

        [HarmonyPatch(typeof(ItemDisplay), "UpdateValueDisplay")]
        public class ItemDisplay_UpdateValueDisplay
        {
            [HarmonyPostfix]
            public static void Postfix(ItemDisplay __instance, ref GameObject ___m_valueHolder, ref UnityEngine.UI.Text ___m_lblValue)
            {
                if (___m_lblValue != null && ___m_valueHolder != null && __instance.RefItem != null && __instance.RefItem.IsSellable && !___m_valueHolder.activeSelf)
                {
                    float num = (float)At.GetField(__instance.RefItem, "m_overrideSellModifier");
                    if (num == -1)
                    {
                        num = 1 + __instance.CharacterUI.TargetCharacter.GetItemSellPriceModifier(null, __instance.RefItem);
                        num *= 0.3f;
                    }
                    num *= __instance.RefItem.RawCurrentValue;

                    ___m_valueHolder.SetActive(true);
                    ___m_lblValue.text = "~" + Mathf.RoundToInt(num).ToString();
                }
            }
        }
    }
}
