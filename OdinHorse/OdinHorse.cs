using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using PieceManager;
using CreatureManager;
using ServerSync;
using UnityEngine;
using System.Collections.Generic;

namespace OdinHorse
{
    [BepInPlugin(HGUIDLower, ModName, ModVersion)]
    public partial class OdinHorse : BaseUnityPlugin
    {

        #region All Variables

        public const string ModVersion = "1.0.0";
        public const string ModName = "OdinMonstersReaper";
        internal const string Author = "Raelaziel";
        internal const string HGUID = Author + "." + "OdinMonstersReaper";
        internal const string HGUIDLower = "Raelaziel.OdinMonstersReaper";
        private const string HarmonyGUID = "Harmony." + Author + "." + ModName;
        private static string ConfigFileName = HGUIDLower + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        public static string ConnectionError = "";
        internal static Creature raeHorse;
        
        //logger
        public static readonly ManualLogSource CreatureManagerModTemplateLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        //config
        private readonly ConfigSync ConfigSync = new(ModName)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        //harmony
        private readonly Harmony _harmony = new(HGUIDLower);

        #endregion

        private void Awake()
        {
            _serverConfigLocked = Config.Bind("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            #region Creature and Assets

            raeHorse = new("odinhorse", "rae_OdinHorse")
            {
                Biome = Heightmap.Biome.Meadows,
                SpawnChance = 30,
                GroupSize = new Range(1, 2),
                CheckSpawnInterval = 600,
                SpecificSpawnTime = SpawnTime.Day,
                RequiredWeather = new List<Weather> { Weather.ClearSkies },
                Maximum = 1
            };
            raeHorse.Localize()
                .English("Horse")
                .German("Horse")
                .French("Horse")
                .Polish("Koń");
            raeHorse.Drops["Coal"].Amount = new Range(1, 2);
            raeHorse.Drops["Coal"].DropChance = 100f;
            raeHorse.Drops["Flametal"].Amount = new Range(1, 1);
            raeHorse.Drops["Flametal"].DropChance = 10f;
            Patches.HorseObject = raeHorse.Prefab;
            raeHorse.Prefab.AddComponent<BagOnEnable>();

            Item raeHorseSaddle = new Item("odinhorse", "rae_SaddleHorse");
            raeHorseSaddle.Name.English("Horse Saddl"); // You can use this to fix the display name in code
            raeHorseSaddle.Description.English("Saddle for your horse");
             raeHorseSaddle.Crafting.Add(CraftingTable.Workbench, 1);
            raeHorseSaddle.MaximumRequiredStationLevel = 5; // Limits the crafting station level required to upgrade or repair the item to 5
            raeHorseSaddle.RequiredItems.Add("Iron", 120);
            raeHorseSaddle.RequiredItems.Add("WolfFang", 20);
            raeHorseSaddle.RequiredItems.Add("Silver", 40);
            raeHorseSaddle.CraftAmount = 1; // We really want to dual wield these

            GameObject rae_Horse_stomp = ItemManager.PrefabManager.RegisterPrefab("odinhorse", "rae_Horse_stomp");
            GameObject rae_OdinHorse_ragdoll = ItemManager.PrefabManager.RegisterPrefab("odinhorse", "rae_OdinHorse_ragdoll");

            #endregion

            // Check & Patch
            SetupWatcher();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
        }

        #region Configs

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                CreatureManagerModTemplateLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                CreatureManagerModTemplateLogger.LogError($"There was an issue loading your {ConfigFileName}");
                CreatureManagerModTemplateLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        public static ConfigEntry<bool>?
            _serverConfigLocked;

        

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }


        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion

        public class BagOnEnable : MonoBehaviour
        {
            private void OnEnable()
            {
                var HumanoidAI = gameObject.GetComponent<Humanoid>();
                var znet = gameObject.GetComponent<ZNetView>();
                if (HumanoidAI.IsTamed())
                {
                    if (znet.GetZDO().GetBool("isTamed") == false) return;
                    var temptransform =
                        OdinHorse.raeHorse.Prefab.transform.Find(
                            "Visual/horse_BIP/horse Pelvis/horse Spine/horse Spine1/BagBag");
                    if (temptransform)
                    {
                        temptransform.gameObject.SetActive(true);
                    }
                }
                else
                {
                    var temptransform =
                        OdinHorse.raeHorse.Prefab.transform.Find(
                            "Visual/horse_BIP/horse Pelvis/horse Spine/horse Spine1/BagBag");
                    if (temptransform)
                    {
                        temptransform.gameObject.SetActive(false);
                    }
                }

            }
        }
    }
}