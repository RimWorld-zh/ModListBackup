﻿using ModListBackup.Settings;
using RimWorldHandler;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace ModListBackup.Handlers
{
    /// <summary>
    /// Class for handling ModsConfig
    /// </summary>
    static class ModsConfigHandler
    {
        /// <summary>
        /// Holds the config handler mode
        /// </summary>
        private static Mode CurrentMode { get; set; }

        /// <summary>
        /// Holds a copy of the loaded/saved data
        /// </summary>
        private static ModsConfigData Data { get; set; }

        internal static bool CanUndo = false;
        private static UndoActionType UndoAction { get; set; }

        static ModsConfigHandler()
        {
            Data = new ModsConfigData
            {
                buildNumber = RimWorld.VersionControl.CurrentBuild,
                activeMods = GetActiveMods()
            };
        }

        internal static bool DoUndoAction()
        {
            if (UndoAction != null && CanUndo)
            {
                return UndoAction.UndoLastAction();
            }
            else
            {
                Main.Log.Message("DoUndoAction was called but no UndoAction was set");
                return false;
            }
        }

        /// <summary>
        /// Save a state
        /// </summary>
        /// <param name="state">The state to save it into</param>
        internal static void SaveState(int state)
        {
            CurrentMode = Mode.Saving;

            UndoAction = new UndoActionType(state, LastActionType.backup);
            CanUndo = true;

            ExposeData(GenBackupStateFile(state));
        }

        /// <summary>
        /// Set the current active mods
        /// </summary>
        /// <param name="modsToActivate">The mods to set as active</param>
        internal static void SetActiveMods(List<string> modsToActivate)
        {
            ClearLoadedMods(true);
            foreach (string modID in modsToActivate)
            {
                ModsConfigAPI.SetActive(modID, true);
            }
            ModsConfigAPI.Save();
        }

        /// <summary>
        /// Check if a state is empty
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static bool StateIsSet(int state)
        {

            return File.Exists(GenBackupStateFile(state));
        }

        internal static string GetStateNamePretty(int state)
        {
            if (SettingsHandler.StateNamesSetting.Value == null || SettingsHandler.StateNamesSetting.Value.StateNames[state - 1].Trim() == "")
                return "Default_State_Name".Translate();
            else
                return SettingsHandler.StateNamesSetting.Value.GetStateName(state).Trim();
        }

        /// <summary>
        /// Backup the current ModsConfig.xml for safekeeping
        /// </summary>
        internal static void BackupCurrent()
        {
            File.Copy(GenFilePaths.ModsConfigFilePath, Path.Combine(Globals.DIR_BACKUPs, Globals.FILE_MODSCONFIG_NAME), true);
        }

        /// <summary>
        /// Restores the ModsConfig.xml Backup
        /// </summary>
        internal static void RestoreCurrent()
        {
            File.Copy(Path.Combine(Globals.DIR_BACKUPs, Globals.FILE_MODSCONFIG_NAME), GenFilePaths.ModsConfigFilePath, true);
        }

        /// <summary>
        /// Load a state
        /// </summary>
        /// <param name="state">The state to load from</param>
        internal static void LoadState(int state)
        {
            //TODO: check if no mods were loaded
            CurrentMode = Mode.Loading;

            UndoAction = new UndoActionType(state, LastActionType.restore);
            CanUndo = true;

            ExposeData(GenBackupStateFile(state));
        }

        /// <summary>
        /// Generates a filename for a state
        /// </summary>
        /// <param name="state">The state to generate a filename for</param>
        /// <returns>The filename for the state</returns>
        private static string GenBackupStateFile(int state)
        {
            if (Globals.SYNC_TO_STEAM)
                return Path.Combine(Globals.DIR_BACKUPs, state.ToString() + Globals.XML_FILE_PREFIX + Globals.RWS_FILE_PREFIX);
            else
                return Path.Combine(Globals.DIR_BACKUPs, state.ToString() + Globals.XML_FILE_PREFIX);
        }

        /// <summary>
        /// Gets a list of active mods in load order
        /// </summary>
        /// <returns>A list of the active mods identifier</returns>
        private static List<string> GetActiveMods()
        {
            List<string> result = new List<string>();
            foreach (ModMetaData mod in ModsConfigAPI.ActiveModsInLoadOrder())
                result.Add(mod.Identifier);
            return result;
        }

        /// <summary>
        /// Clears the current active mods
        /// </summary>
        /// <param name="removeCore">Set to true to remove core [Optional](Default:false)</param>
        private static void ClearLoadedMods(bool removeCore = false)
        {
            ModsConfigAPI.Reset();
            if(removeCore)
                ModsConfigAPI.SetActive(ModContentPack.CoreModIdentifier, false);
        }

        private static ModsConfigData ReadState(string filepath)
        {
            return XmlLoaderAPI.ItemFromXmlFile<ModsConfigData>(filepath, true);
        }

        /// <summary>
        /// Saves or Loads the state
        /// </summary>
        /// <param name="filepath">The filepath to the state</param>
        private static void ExposeData(string filepath, bool useUndoAction = false)
        {
            try
            {
                if (CurrentMode == Mode.Saving)
                {
                    Main.LogDebug("Saving state to {0}", filepath);

                    Data = new ModsConfigData
                    {
                        buildNumber = RimWorld.VersionControl.CurrentBuild,
                        activeMods = GetActiveMods()
                    };
                    XmlSaverAPI.SaveDataObject((object)Data, filepath);
                }
                else if (CurrentMode == Mode.Loading)
                {
                    Main.LogDebug("Loading state from {0}", filepath);

                    List<string> current = new List<string>();

                    Data = ReadState(filepath);

                    ClearLoadedMods(true);
                    foreach (string modID in Data.activeMods)
                        ModsConfigAPI.SetActive(modID, true);
                }
            }
            catch (System.Exception e)
            {
                //An error occurred, output to log and reset loaded mods
                Main.Log.ReportException(e, Globals.MOD_IDENTIFIER, true, "ExposeData");
                ClearLoadedMods();
            }
        }

        /// <summary>
        /// A Class to define the ModsConfig data to hold
        /// </summary>
        private class ModsConfigData
        {
            public int buildNumber = -1;
            public List<string> activeMods = new List<string>();
        }

        private class UndoActionType
        {
            private int State { get; set; }
            private ModsConfigData ModData { get; set;}
            private LastActionType LastAction { get; set; }

            public UndoActionType(int state, LastActionType lastAction = LastActionType.none)
            {
                Main.LogDebug("Creating last action type {1} for state {0}", state, lastAction.ToString());
                State = state;
                LastAction = lastAction;
                switch (LastAction)
                {
                    case LastActionType.restore:
                        ModData = new ModsConfigData
                        {
                            buildNumber = RimWorld.VersionControl.CurrentBuild,
                            activeMods = GetActiveMods()
                        };
                        break;
                    case LastActionType.backup:
                        if (!StateIsSet(state))
                        {
                            ModData = null;
                        }
                        else
                        {
                            ModData = ReadState(GenBackupStateFile(state));
                        }
                        break;
                    case LastActionType.none:
                    default:
                        Main.Log.Warning("Last Undo Action was not set with a type");
                        break;
                }
            }

            public bool UndoLastAction()
            {
                Main.LogDebug("Undoing last action type {1} for state {0}", State, LastAction.ToString());
                switch (LastAction)
                {
                    case LastActionType.restore:
                        Main.LogDebug("Restoring {0} active mods", ModData.activeMods.Count);
                        SetActiveMods(ModData.activeMods);
                        return true;
                    case LastActionType.backup:
                        if (ModData != null)
                        {
                            Main.LogDebug("Restored {0}'s last state", State);
                            XmlSaverAPI.SaveDataObject((object)ModData, GenBackupStateFile(State));
                        }
                        else {
                            Main.LogDebug("Attempting to delete {0}", State);
                            File.Delete(GenBackupStateFile(State));
                        }
                        return true;
                    case LastActionType.none:
                    default:
                        Main.Log.Warning("Last Undo Action was not set with a type, cannot undo last action");
                        return false;
                }
            }
        }

        /// <summary>
        /// Enum for easily setting current mode
        /// </summary>
        private enum Mode { Saving, Loading, Inactive }

        private enum LastActionType { restore, backup, none }
    }
}
