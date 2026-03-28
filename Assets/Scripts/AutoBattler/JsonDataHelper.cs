using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoBattler
{
    internal static class JsonDataHelper
    {
        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static List<object> GetArray(Dictionary<string, object> source, string key)
        {
            if (source != null && source.TryGetValue(key, out var value) && value is List<object> list)
            {
                return list;
            }

            return new List<object>();
        }

        public static string GetString(Dictionary<string, object> source, string key, string fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            return value.ToString();
        }

        public static int GetInt(Dictionary<string, object> source, string key, int fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            return ParseInt(value, fallback);
        }

        public static float GetFloat(Dictionary<string, object> source, string key, float fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            return ParseFloat(value, fallback);
        }

        public static int GetModifiedInt(Dictionary<string, object> source, string key, int baseValue)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return baseValue;
            }

            return (int)Math.Round(ApplyNumericOverride(value, baseValue));
        }

        public static float GetModifiedFloat(Dictionary<string, object> source, string key, float baseValue)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return baseValue;
            }

            return (float)ApplyNumericOverride(value, baseValue);
        }

        public static T GetEnum<T>(Dictionary<string, object> source, string key, T fallback) where T : struct
        {
            var rawValue = GetString(source, key, string.Empty);
            return !string.IsNullOrWhiteSpace(rawValue) && Enum.TryParse(rawValue, true, out T parsed)
                ? parsed
                : fallback;
        }

        private static double ApplyNumericOverride(object value, double baseValue)
        {
            switch (value)
            {
                case long longValue:
                    return longValue;
                case double doubleValue:
                    return doubleValue;
                case string stringValue:
                    return ParseNumericExpression(stringValue, baseValue);
                default:
                    return baseValue;
            }
        }

        private static int ParseInt(object value, int fallback)
        {
            switch (value)
            {
                case long longValue:
                    return (int)longValue;
                case double doubleValue:
                    return (int)Math.Round(doubleValue);
                case string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return fallback;
            }
        }

        private static float ParseFloat(object value, float fallback)
        {
            switch (value)
            {
                case long longValue:
                    return longValue;
                case double doubleValue:
                    return (float)doubleValue;
                case string stringValue when float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return fallback;
            }
        }

        private static double ParseNumericExpression(string value, double baseValue)
        {
            var trimmed = value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return baseValue;
            }

            if (trimmed.Length > 1)
            {
                var operandText = trimmed.Substring(1).Trim();
                if (double.TryParse(operandText, NumberStyles.Float, CultureInfo.InvariantCulture, out var operand))
                {
                    switch (trimmed[0])
                    {
                        case '+':
                            return baseValue + operand;
                        case '-':
                            return baseValue - operand;
                        case '*':
                            return baseValue * operand;
                        case '/':
                            return Math.Abs(operand) < 0.00001d ? baseValue : baseValue / operand;
                    }
                }
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var absoluteValue))
            {
                return absoluteValue;
            }

            return baseValue;
        }
    }
}
