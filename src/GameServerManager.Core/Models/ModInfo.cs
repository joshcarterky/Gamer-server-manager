using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameServerManager.Core.Models
{
    /// <summary>
    /// Represents mod information for a game server.
    /// </summary>
    public class ModInfo : ObservableObject
    {
        private string _modId = string.Empty;
        private string _name = string.Empty;
        private int _orderIndex = 0;

        [JsonPropertyName("mod_id")]
        public string ModId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("order_index")]
        public int OrderIndex 
        {
            get => _orderIndex;
            set 
            { 
                _orderIndex = value; 
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        public ModInfo() { }

        public ModInfo(string modId, string name)
        {
            _modId = modId;
            _name = name;
            ModId = modId;
            Name = name;
            Enabled = true;
            OrderIndex = 0;
        }
    }

    /// <summary>
    /// Collection of mods for a server profile.
    /// </summary>
    public class ModCollection : ObservableCollection<ModInfo>
    {
        public ModCollection() { }

        public void AddOrUpdate(ModInfo mod)
        {
            var existing = this.FirstOrDefault(m => m.ModId == mod.ModId);
            if (existing != null)
            {
                // Update existing mod
                existing.Name = mod.Name;
                existing.Enabled = mod.Enabled;
                existing.Description = mod.Description;
                existing.Author = mod.Author;
                existing.Version = mod.Version;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            }
            else
            {
                Add(mod);
            }
        }

        public void RemoveByModId(string modId)
        {
            var mod = this.FirstOrDefault(m => m.ModId == modId);
            if (mod != null)
            {
                Remove(mod);
            }
        }

        public void Reorder(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Count || toIndex < 0 || toIndex >= Count)
                return;

            var item = this[fromIndex];
            Remove(item);
            Insert(toIndex, item);

            // Re-index all mods
            for (int i = 0; i < Count; i++)
            {
                this[i].OrderIndex = i;
            }
        }

        public List<string> GetEnabledModIds()
        {
            return this.Where(m => m.Enabled)
                       .OrderBy(m => m.OrderIndex)
                       .Select(m => m.ModId)
                       .ToList();
        }
    }
}
