using System.Collections.Generic;

namespace AutoBattler
{
    public sealed class TerrainSpeedProfile
    {
        private readonly Dictionary<string, float> modifiers;

        public TerrainSpeedProfile(Dictionary<string, float> modifiers = null)
        {
            this.modifiers = modifiers != null
                ? new Dictionary<string, float>(modifiers, System.StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, float> Modifiers => modifiers;

        public float GetModifier(string terrainType)
        {
            if (string.IsNullOrWhiteSpace(terrainType))
            {
                return 1f;
            }

            return modifiers.TryGetValue(terrainType, out var modifier) ? modifier : 1f;
        }

        public float GetMaxModifier()
        {
            var maxModifier = 1f;
            foreach (var pair in modifiers)
            {
                maxModifier = UnityEngine.Mathf.Max(maxModifier, pair.Value);
            }

            return maxModifier;
        }

        public TerrainSpeedProfile WithOverrides(Dictionary<string, object> overrides)
        {
            var merged = new Dictionary<string, float>(modifiers, System.StringComparer.OrdinalIgnoreCase);
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    var baseValue = merged.TryGetValue(pair.Key, out var existingValue) ? existingValue : 1f;
                    merged[pair.Key] = UnityEngine.Mathf.Max(0.05f, JsonDataHelper.GetModifiedFloat(pair.Value, baseValue));
                }
            }

            return new TerrainSpeedProfile(merged);
        }

        public static TerrainSpeedProfile Empty { get; } = new TerrainSpeedProfile();
    }
}
