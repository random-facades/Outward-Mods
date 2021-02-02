/*   
 *   -------------  TODO LIST  -------------
 *   
 *   
 *   
 *   -------------  TOLEARN LIST  -------------
 *   
 *   * Probably more stuff, but forgot
 *   
 *   -------------  TOFIX (eventually) LIST  -------------
 *   
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
using SideLoader;

namespace SoulWeaver
{
    [BepInPlugin(ID, NAME, VERSION)]
    public class SoulWeaverCore : BaseUnityPlugin
    {
        const string ID = "com.random_facades.soulweaver";
        const string NAME = "New Game+";
        const string VERSION = "0.1";
        //  Can't be fancy cause localization issues
        //public static double VersionNum = double.Parse(VERSION);
        public static double VersionNum = 0.1;

        const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

        public static SoulWeaverCore Instance;
        private static ManualLogSource logboy;

        internal void Awake()
        {
            Instance = this;
            logboy = Logger;

            var harmony = new Harmony(ID);
            harmony.PatchAll();

            Log("New Game Plus starting...");
        }

        public static void Log(string message)
        {
            logboy.Log(LogLevel.Message, message);
        }
    }
}
