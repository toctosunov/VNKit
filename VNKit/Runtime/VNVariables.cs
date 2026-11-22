using System.Collections.Generic;
using System.Globalization;

namespace VNKit
{
    /// <summary>
    /// Runtime values for a story's declared variables. Everything is stored as a string internally
    /// (that's what both VNCondition/VNVariableOp and the save system work with), typed accessors
    /// convert on the way in and out.
    /// </summary>
    public class VNVariables
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        readonly Dictionary<string, VNVarType> types = new Dictionary<string, VNVarType>();

        public void Init(VNStory story)
        {
            values.Clear();
            types.Clear();
            if (story == null) return;
            foreach (var def in story.variables)
            {
                if (def == null || string.IsNullOrEmpty(def.name)) continue;
                types[def.name] = def.type;
                values[def.name] = def.DefaultAsString();
            }
        }

        public VNVarType GetType(string name)
        {
            return types.TryGetValue(name, out var t) ? t : VNVarType.String;
        }

        public bool Has(string name) => values.ContainsKey(name);

        public bool GetBool(string name) =>
            values.TryGetValue(name, out var v) && (v == "true" || v == "1");

        public int GetInt(string name)
        {
            if (values.TryGetValue(name, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r))
                return r;
            return 0;
        }

        public float GetFloat(string name)
        {
            if (values.TryGetValue(name, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                return r;
            return 0f;
        }

        public string GetString(string name) => values.TryGetValue(name, out var v) ? v : null;

        public void SetBool(string name, bool value)
        {
            types[name] = VNVarType.Bool;
            values[name] = value ? "true" : "false";
        }

        public void SetInt(string name, int value)
        {
            types[name] = VNVarType.Int;
            values[name] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void SetFloat(string name, float value)
        {
            types[name] = VNVarType.Float;
            values[name] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void SetString(string name, string value)
        {
            types[name] = VNVarType.String;
            values[name] = value ?? "";
        }

        /// <summary>Raw snapshot for saving: parallel names/values, in whatever order the dictionary holds them.</summary>
        public void Snapshot(out List<string> names, out List<string> rawValues)
        {
            names = new List<string>(values.Count);
            rawValues = new List<string>(values.Count);
            foreach (var kv in values)
            {
                names.Add(kv.Key);
                rawValues.Add(kv.Value);
            }
        }

        /// <summary>Restores values from a save. Types are kept from Init(story); unknown names are added as strings.</summary>
        public void Restore(List<string> names, List<string> rawValues)
        {
            if (names == null || rawValues == null) return;
            int count = System.Math.Min(names.Count, rawValues.Count);
            for (int i = 0; i < count; i++)
            {
                values[names[i]] = rawValues[i];
                if (!types.ContainsKey(names[i])) types[names[i]] = VNVarType.String;
            }
        }
    }
}
