using System;
using System.IO;
using UnityEngine;

namespace VNKit
{
    /// <summary>
    /// Save slots on disk as plain JSON under Application.persistentDataPath/VNSaves.
    /// The VNStory asset itself is never written to a slot (it's content, not state) -
    /// the caller supplies it when loading, same as any other scene reference.
    /// </summary>
    public static class VNSaveSystem
    {
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "VNSaves");

        static string PathFor(int slot) => Path.Combine(SaveDirectory, "slot" + slot + ".json");

        public static bool HasSave(int slot) => File.Exists(PathFor(slot));

        /// <summary>Writes the player's current frame and variables to a slot.</summary>
        public static void Save(VNPlayer player, int slot)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var data = player.CaptureState();
            data.savedAtIso = DateTime.UtcNow.ToString("o");

            Directory.CreateDirectory(SaveDirectory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(PathFor(slot), json);
        }

        /// <summary>
        /// Loads a slot into the given story and applies it to the player. The story must be the same
        /// one the slot was saved from (or at least share the frame ids / variable names); a mismatch
        /// is logged but the load still proceeds on a best-effort basis.
        /// </summary>
        public static bool Load(VNPlayer player, VNStory story, int slot)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (story == null) throw new ArgumentNullException(nameof(story));

            string path = PathFor(slot);
            if (!File.Exists(path)) return false;

            VNSaveData data;
            try
            {
                data = JsonUtility.FromJson<VNSaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"VNSaveSystem: slot {slot} is corrupt ({e.Message}).");
                return false;
            }
            if (data == null) return false;

            if (!string.IsNullOrEmpty(data.storyName) && data.storyName != story.name)
                Debug.LogWarning($"VNSaveSystem: slot {slot} was saved from '{data.storyName}', loading into '{story.name}'.");

            player.ApplyState(story, data);
            return true;
        }

        /// <summary>Reads only the metadata of a slot (for a save-slot list UI) without touching a player.</summary>
        public static bool TryReadMeta(int slot, out VNSaveData data)
        {
            data = null;
            string path = PathFor(slot);
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<VNSaveData>(File.ReadAllText(path));
                return data != null;
            }
            catch
            {
                return false;
            }
        }

        public static void DeleteSave(int slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
