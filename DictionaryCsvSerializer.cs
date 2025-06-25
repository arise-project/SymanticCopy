using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SymanticCopy;

//var stringDict = new Dictionary<string, string>
//{
//["ClassA"] = "NamespaceA.ClassA",
//["ClassB"] = "NamespaceB.ClassB"
//};

//var serializer = new DictionaryCsvSerializer();
//serializer.Serialize(stringDict, "string_dict.csv");

//// Deserialize
//var loadedStringDict = serializer.Deserialize<string, string>("string_dict.csv");

//public class ClassInfo
//{
//    public string Namespace { get; set; }
//    public int MethodCount { get; set; }
//    public bool IsPublic { get; set; }
//}

//var classDict = new Dictionary<string, ClassInfo>
//{
//    ["ClassA"] = new ClassInfo { Namespace = "NS1", MethodCount = 5, IsPublic = true },
//    ["ClassB"] = new ClassInfo { Namespace = "NS2", MethodCount = 3, IsPublic = false }
//};

//serializer.Serialize(classDict, "class_dict.csv");

//// Deserialize
//var loadedClassDict = serializer.Deserialize<string, ClassInfo>("class_dict.csv");

//// Handles commas, quotes, and newlines in values
//var complexDict = new Dictionary<string, string>
//{
//    ["Key1"] = "Value, with, commas",
//    ["Key2"] = "Value with \"quotes\"",
//    ["Key3"] = "Value with\nnewlines"
//};

//serializer.Serialize(complexDict, "complex.csv");


public class DictionaryCsvSerializer
{
    public void Serialize<TKey, TValue>(Dictionary<TKey, TValue> dictionary, string filePath)
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            File.WriteAllText(filePath, string.Empty);
            return;
        }

        var csvContent = new StringBuilder();

        // Handle different dictionary types
        if (typeof(TKey) == typeof(string) && typeof(TValue) == typeof(string))
        {
            SerializeStringDictionary(dictionary as Dictionary<string, string>, csvContent);
        }
        else if (typeof(TValue).IsClass && typeof(TValue) != typeof(string))
        {
            SerializeClassDictionary(dictionary, csvContent);
        }
        else
        {
            SerializeGenericDictionary(dictionary, csvContent);
        }

        File.WriteAllText(filePath, csvContent.ToString());
    }

    private void SerializeStringDictionary(Dictionary<string, string> dictionary, StringBuilder csvContent)
    {
        csvContent.AppendLine("Key,Value");

        foreach (var kvp in dictionary)
        {
            var escapedKey = EscapeCsvField(kvp.Key);
            var escapedValue = EscapeCsvField(kvp.Value);
            csvContent.AppendLine($"{escapedKey},{escapedValue}");
        }
    }

    private void SerializeClassDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, StringBuilder csvContent)
    {
        var properties = typeof(TValue).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = typeof(TValue).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var members = properties.Cast<MemberInfo>().Concat(fields.Cast<MemberInfo>()).ToArray();

        // Write header
        csvContent.Append("Key");
        foreach (var member in members)
        {
            csvContent.Append($",{member.Name}");
        }
        csvContent.AppendLine();

        // Write data rows
        foreach (var kvp in dictionary)
        {
            csvContent.Append(EscapeCsvField(kvp.Key?.ToString()));

            foreach (var member in members)
            {
                object value = null;

                if (member is PropertyInfo prop)
                {
                    value = prop.GetValue(kvp.Value);
                }
                else if (member is FieldInfo field)
                {
                    value = field.GetValue(kvp.Value);
                }

                csvContent.Append($",{EscapeCsvField(value?.ToString())}");
            }

            csvContent.AppendLine();
        }
    }

    private void SerializeGenericDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, StringBuilder csvContent)
    {
        csvContent.AppendLine("Key,Value");

        foreach (var kvp in dictionary)
        {
            var escapedKey = EscapeCsvField(kvp.Key?.ToString());
            var escapedValue = EscapeCsvField(kvp.Value?.ToString());
            csvContent.AppendLine($"{escapedKey},{escapedValue}");
        }
    }

    public Dictionary<TKey, TValue> Deserialize<TKey, TValue>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<TKey, TValue>();
        }

        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            return new Dictionary<TKey, TValue>();
        }

        if (typeof(TKey) == typeof(string) && typeof(TValue) == typeof(string))
        {
            return DeserializeStringDictionary(lines) as Dictionary<TKey, TValue>;
        }
        else if (typeof(TValue).IsClass && typeof(TValue) != typeof(string))
        {
            return DeserializeClassDictionary<TKey, TValue>(lines);
        }
        else
        {
            return DeserializeGenericDictionary<TKey, TValue>(lines);
        }
    }

    private Dictionary<string, string> DeserializeStringDictionary(string[] lines)
    {
        var dictionary = new Dictionary<string, string>();
        if (lines.Length < 2) return dictionary;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Length >= 2)
            {
                var key = UnescapeCsvField(parts[0]);
                var value = UnescapeCsvField(parts[1]);
                dictionary[key] = value;
            }
        }

        return dictionary;
    }

    private Dictionary<TKey, TValue> DeserializeClassDictionary<TKey, TValue>(string[] lines) where TValue : new()
    {
        var dictionary = new Dictionary<TKey, TValue>();
        if (lines.Length < 2) return dictionary;

        var header = lines[0].Split(',');
        var members = GetClassMembers<TValue>();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Length == 0) continue;

            try
            {
                var key = (TKey)Convert.ChangeType(UnescapeCsvField(parts[0]), typeof(TKey));
                var value = new TValue();

                for (int j = 1; j < parts.Length && j < header.Length; j++)
                {
                    var memberName = header[j];
                    if (members.TryGetValue(memberName, out var member))
                    {
                        var fieldValue = UnescapeCsvField(parts[j]);
                        SetMemberValue(value, member, fieldValue);
                    }
                }

                dictionary[key] = value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing line {i}: {ex.Message}");
            }
        }

        return dictionary;
    }

    private Dictionary<string, MemberInfo> GetClassMembers<T>()
    {
        var members = new Dictionary<string, MemberInfo>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            members[prop.Name] = prop;
        }

        foreach (var field in fields)
        {
            members[field.Name] = field;
        }

        return members;
    }

    private void SetMemberValue(object obj, MemberInfo member, string stringValue)
    {
        if (string.IsNullOrEmpty(stringValue)) return;

        try
        {
            object value = null;
            var memberType = member is PropertyInfo prop ? prop.PropertyType :
                           ((FieldInfo)member).FieldType;

            if (memberType == typeof(string))
            {
                value = stringValue;
            }
            else if (memberType.IsEnum)
            {
                value = Enum.Parse(memberType, stringValue);
            }
            else
            {
                value = Convert.ChangeType(stringValue, memberType, CultureInfo.InvariantCulture);
            }

            if (member is PropertyInfo property)
            {
                property.SetValue(obj, value);
            }
            else if (member is FieldInfo field)
            {
                field.SetValue(obj, value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting {member.Name}: {ex.Message}");
        }
    }

    private Dictionary<TKey, TValue> DeserializeGenericDictionary<TKey, TValue>(string[] lines)
    {
        var dictionary = new Dictionary<TKey, TValue>();
        if (lines.Length < 2) return dictionary;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Length >= 2)
            {
                try
                {
                    var key = (TKey)Convert.ChangeType(UnescapeCsvField(parts[0]), typeof(TKey));
                    var value = (TValue)Convert.ChangeType(UnescapeCsvField(parts[1]), typeof(TValue));
                    dictionary[key] = value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line {i}: {ex.Message}");
                }
            }
        }

        return dictionary;
    }

    private string EscapeCsvField(string field)
    {
        if (field == null) return string.Empty;
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private string UnescapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return field;
        if (field.StartsWith("\"") && field.EndsWith("\""))
        {
            return field.Substring(1, field.Length - 2).Replace("\"\"", "\"");
        }
        return field;
    }

    private string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var currentField = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i < line.Length - 1 && line[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        result.Add(currentField.ToString());
        return result.ToArray();
    }
}