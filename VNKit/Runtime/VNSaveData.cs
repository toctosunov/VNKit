using System;
using System.Collections.Generic;

namespace VNKit
{
    /// <summary>
    /// A snapshot of a VNPlayer: where the player is in the story and the story's variables.
    /// Plain data, JsonUtility-friendly (no Dictionary, no object references).
    /// </summary>
    [Serializable]
    public class VNSaveData
    {
        public string storyName;
        public string currentNodeId;
        public List<string> varNames = new List<string>();
        public List<string> varValues = new List<string>();
        public string savedAtIso;
    }
}
