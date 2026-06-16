using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services
{
    /// <summary>
    /// Service to manage mods across server profiles.
    /// </summary>
    public class ModManagerService : IModManagerService
    {
        private readonly string _modDataDirectory;
        private readonly string _profileDirectory;

        public event Action? ModsChanged;

        public ModManagerService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _modDataDirectory = Path.Combine(appData, "GameServerManager", "ModData");
            _profileDirectory = Path.Combine(_modDataDirectory, "Profiles");
            
            Directory.CreateDirectory(_profileDirectory);
        }

        public void AddOrUpdateMod(string profileId, ModInfo mod)
        {
            var modFile = GetProfileModFile(profileId);
            var mods = LoadMods(modFile);
            var existing = mods.FirstOrDefault(m => m.ModId == mod.ModId);
            
            if (existing != null)
            {
                existing.Name = mod.Name;
                existing.Enabled = mod.Enabled;
                existing.Description = mod.Description;
                existing.Author = mod.Author;
                existing.Version = mod.Version;
                existing.OrderIndex = mod.OrderIndex;
            }
            else
            {
                mods.Add(mod);
            }

            SaveMods(modFile, mods);
            ModsChanged?.Invoke();
        }

        public void RemoveMod(string profileId, string modId)
        {
            var modFile = GetProfileModFile(profileId);
            var mods = LoadMods(modFile);
            var modToRemove = mods.FirstOrDefault(m => m.ModId == modId);
            
            if (modToRemove != null)
            {
                mods.Remove(modToRemove);
                SaveMods(modFile, mods);
                ModsChanged?.Invoke();
            }
        }

        public void ReorderMod(string profileId, int fromIndex, int toIndex)
        {
            var modFile = GetProfileModFile(profileId);
            var mods = LoadMods(modFile);
            
            if (fromIndex < 0 || fromIndex >= mods.Count || toIndex < 0 || toIndex >= mods.Count)
                return;

            var item = mods[fromIndex];
            mods.RemoveAt(fromIndex);
            mods.Insert(toIndex, item);

            for (int i = 0; i < mods.Count; i++)
            {
                mods[i].OrderIndex = i;
            }

            SaveMods(modFile, mods);
            ModsChanged?.Invoke();
        }

        public List<ModInfo> GetEnabledMods(string profileId)
        {
            var modFile = GetProfileModFile(profileId);
            var mods = LoadMods(modFile);
            return mods.Where(m => m.Enabled).OrderBy(m => m.OrderIndex).ToList();
        }

        public List<ModInfo> GetAllMods(string profileId)
        {
            var modFile = GetProfileModFile(profileId);
            return LoadMods(modFile);
        }

        private string GetProfileModFile(string profileId)
        {
            return Path.Combine(_profileDirectory, $"{profileId}.json");
        }

        private List<ModInfo> LoadMods(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<ModInfo>();

            try
            {
                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions();
                // Deserialize as ModInfo list
                var mods = JsonSerializer.Deserialize<List<ModInfo>>(json, options);
                return mods ?? new List<ModInfo>();
            }
            catch
            {
                return new List<ModInfo>();
            }
        }

        private void SaveMods(string filePath, List<ModInfo> mods)
        {
            BackupExistingFile(filePath);
            var json = JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        private void BackupExistingFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var backupDirectory = Path.Combine(_modDataDirectory, "Backups", DateTime.UtcNow.ToString("yyyyMMdd"));
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(
                backupDirectory,
                $"{Path.GetFileName(filePath)}.{DateTime.UtcNow:HHmmss}.bak");
            File.Copy(filePath, backupPath, overwrite: true);
        }
    }

    public interface IModManagerService
    {
        event Action? ModsChanged;
        
        void AddOrUpdateMod(string profileId, ModInfo mod);
        void RemoveMod(string profileId, string modId);
        void ReorderMod(string profileId, int fromIndex, int toIndex);
        List<ModInfo> GetEnabledMods(string profileId);
        List<ModInfo> GetAllMods(string profileId);
    }
}
