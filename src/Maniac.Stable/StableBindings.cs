using Microsoft.Diagnostics.Runtime;

namespace Maniac.Stable;

static class StableBindings
{
    const string KeysType = "Microsoft.Xna.Framework.Input.Keys";

    public static ManiaKeyMap ReadMap(ClrHeap heap, int columns)
    {
        int[] xna = ReadXna(heap, columns);
        var vks = new ushort[xna.Length];
        for (int i = 0; i < xna.Length; i++)
        {
            if ((uint)xna[i] > 0xFF)
                throw new InvalidOperationException($"mania key {XnaKeyName(xna[i])} is not a virtual key.");
            vks[i] = (ushort)xna[i];
        }

        string spec;
        try
        {
            spec = SpecFromXna(xna);
        }
        catch
        {
            spec = FormatXna(xna);
        }

        return new ManiaKeyMap(spec, vks);
    }

    public static int[] ParseXnaNames(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 18)
            throw new InvalidOperationException($"mania layout '{text}' has {parts.Length} keys.");
        var keys = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            keys[i] = ParseXnaName(parts[i], text);
        return keys;
    }

    public static string SpecFromXna(int[] keys)
    {
        var chars = new char[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            chars[i] = SpecChar(keys[i], keys);
        return new string(chars);
    }

    static int[] ReadXna(ClrHeap heap, int columns)
    {
        if (columns is < 1 or > 18)
            throw new InvalidOperationException($"mania column count {columns} is not plausible.");

        var columnKeys = ReadColumnKeys(heap);
        if (columnKeys.Length == columns)
            return columnKeys;

        foreach (var layout in ReadLayouts(heap))
        {
            if (layout.Keys.Length == columns)
                return layout.Keys;
        }

        var fromConfig = ReadConfigLayout(heap, columns);
        if (fromConfig != null)
            return fromConfig;

        throw new InvalidOperationException(
            $"no live {columns}K mania keybinds (playfield columns={columnKeys.Length}, no ManiaLayouts{columns}K in config). Open a mania map.");
    }

    static int[]? ReadConfigLayout(ClrHeap heap, int columns)
    {
        string want = $"ManiaLayouts{columns}K";
        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.Type?.Name != "System.Collections.Generic.Dictionary<System.String, System.Object>")
                continue;
            int count;
            try
            {
                count = obj.ReadField<int>("count");
            }
            catch
            {
                continue;
            }

            if (count is < 50 or > 400)
                continue;
            try
            {
                string? value = FindDictString(obj, count, want);
                if (value != null)
                    return ParseXnaNames(value);
            }
            catch
            {
            }
        }

        return null;
    }

    static string? FindDictString(ClrObject dict, int count, string want)
    {
        var entries = dict.ReadObjectField("entries");
        if (entries.IsNull || !entries.IsArray)
            return null;
        var arr = entries.AsArray();
        int shown = 0;
        for (int i = 0; i < arr.Length && shown < count; i++)
        {
            var entry = arr.GetStructValue(i);
            if (entry.ReadField<int>("hashCode") < 0)
                continue;
            shown++;
            if (ReadEntryString(entry, "key") != want)
                continue;
            string described = DescribeBindable(entry);
            if (described.Length >= 2 && described[0] == '\'')
                return described.Trim('\'');
            if (LooksLikeLayout(described))
                return described;
        }

        return null;
    }

    static int ParseXnaName(string name, string text)
    {
        if (name.Length == 1 && char.IsLetter(name[0]))
            return char.ToUpperInvariant(name[0]);
        if (name.Length == 2 && name[0] == 'D' && name[1] is >= '0' and <= '9')
            return 48 + (name[1] - '0');
        return name switch
        {
            "Space" => 32,
            "OemSemicolon" => 186,
            "OemPlus" => 187,
            "OemComma" => 188,
            "OemMinus" => 189,
            "OemPeriod" => 190,
            "OemQuestion" => 191,
            "OemTilde" => 192,
            "LeftShift" or "LeftShiftKey" => 160,
            "RightShift" => 161,
            "LeftControl" or "LeftCtrl" => 162,
            "RightControl" or "RightCtrl" => 163,
            "LeftAlt" => 164,
            "RightAlt" => 165,
            "Tab" => 9,
            "Enter" => 13,
            "Escape" => 27,
            "Left" => 37,
            "Up" => 38,
            "Right" => 39,
            "Down" => 40,
            _ => throw new InvalidOperationException($"mania layout '{text}' has unknown key '{name}'.")
        };
    }

    static Layout[] ReadLayouts(ClrHeap heap)
    {
        var list = new List<Layout>();
        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.Type?.Name != StableLayout.ManiaLayoutType)
                continue;
            var keysObj = obj.ReadObjectField(StableLayout.ManiaLayoutKeysField);
            if (keysObj.IsNull || !keysObj.IsArray)
                continue;
            int columns = obj.ReadField<int>(StableLayout.ManiaLayoutColumnsField);
            int kind = obj.ReadField<int>(StableLayout.ManiaLayoutKindField);
            var keys = ReadKeysArray(keysObj.AsArray());
            if (keys.Length == 0)
                continue;
            list.Add(new Layout(columns, kind, keys));
        }

        return list.ToArray();
    }

    static int[] ReadColumnKeys(ClrHeap heap)
    {
        var rows = new List<(int Index, float X, int Key)>();
        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.Type?.Name != StableLayout.ManiaColumnType)
                continue;
            int key = ReadIntAt(obj, StableLayout.ManiaColumnKeyOffset);
            if (key == 0)
                continue;
            int index = ReadIntAt(obj, StableLayout.ManiaColumnIndexOffset);
            float x = ReadFloatAt(obj, StableLayout.ManiaColumnXOffset);
            rows.Add((index, x, key));
        }

        if (rows.Count == 0)
            return [];
        bool indices = rows.Select(r => r.Index).Distinct().Count() == rows.Count
            && rows.Min(r => r.Index) == 0
            && rows.Max(r => r.Index) == rows.Count - 1;
        var ordered = indices ? rows.OrderBy(r => r.Index) : rows.OrderBy(r => r.X);
        return ordered.Select(r => r.Key).ToArray();
    }

    static string ReadEntryString(ClrValueType entry, string field)
    {
        var f = entry.Type?.GetFieldByName(field);
        if (f == null)
            return "?";
        if (f.ElementType == ClrElementType.String)
            return f.ReadString(entry.Address, interior: true) ?? "";
        if (f.IsObjectReference)
        {
            var obj = f.ReadObject(entry.Address, interior: true);
            return obj.Type?.Name == "System.String" ? obj.AsString() ?? "" : obj.Type?.Name ?? "?";
        }

        if (f.ElementType == ClrElementType.Int32)
            return f.Read<int>(entry.Address, interior: true).ToString();
        return f.ElementType.ToString();
    }

    static string DescribeBindable(ClrValueType entry)
    {
        var f = entry.Type?.GetFieldByName("value");
        if (f == null)
            return "no-value";
        if (f.ElementType == ClrElementType.String)
            return "'" + (f.ReadString(entry.Address, interior: true) ?? "") + "'";
        if (f.ElementType == ClrElementType.Int32)
            return f.Read<int>(entry.Address, interior: true).ToString();
        if (!f.IsObjectReference)
            return f.ElementType.ToString();
        var obj = f.ReadObject(entry.Address, interior: true);
        if (obj.IsNull)
            return "null";
        if (obj.Type?.Name == "System.String")
            return "'" + obj.AsString() + "'";
        return DescribeObjectScalars(obj);
    }

    static string DescribeObjectScalars(ClrObject obj)
    {
        if (obj.Type == null)
            return "null";
        var parts = new List<string> { obj.Type.Name ?? "?" };
        foreach (var field in obj.Type.Fields)
        {
            try
            {
                if (field.ElementType == ClrElementType.String)
                {
                    string? s = field.ReadString(obj.Address, interior: false);
                    if (!string.IsNullOrEmpty(s) && s.Length < 120)
                        parts.Add($"{field.Name}='{s!}'");
                }
                else if (field.ElementType == ClrElementType.Int32)
                    parts.Add($"{field.Name}={field.Read<int>(obj.Address, interior: false)}");
                else if (field.IsObjectReference)
                {
                    var child = field.ReadObject(obj.Address, interior: false);
                    if (child.Type?.Name == "System.String")
                    {
                        string? s = child.AsString();
                        if (!string.IsNullOrEmpty(s) && s.Length < 120)
                            parts.Add($"{field.Name}='{s!}'");
                    }
                    else if (child.Type?.Name == KeysType + "[]")
                        parts.Add($"{field.Name}={FormatXna(ReadKeysArray(child.AsArray()))}");
                }
            }
            catch
            {
            }
        }

        return string.Join(" ", parts);
    }

    static int[] ReadKeysArray(ClrArray arr)
    {
        var keys = new int[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            keys[i] = arr.GetValue<int>(i);
        return keys;
    }

    static int ReadIntAt(ClrObject obj, int offset)
    {
        foreach (var field in obj.Type?.Fields ?? [])
        {
            if (field.Offset == offset && field.ElementType == ClrElementType.Int32)
                return field.Read<int>(obj.Address, interior: false);
        }

        throw new InvalidOperationException($"{obj.Type?.Name} has no Int32 at +{offset:X}");
    }

    static float ReadFloatAt(ClrObject obj, int offset)
    {
        foreach (var field in obj.Type?.Fields ?? [])
        {
            if (field.Offset == offset && field.ElementType == ClrElementType.Float)
                return field.Read<float>(obj.Address, interior: false);
        }

        throw new InvalidOperationException($"{obj.Type?.Name} has no Single at +{offset:X}");
    }

    static char SpecChar(int xna, int[] keys)
    {
        if (xna == 32)
            return '-';
        if (xna == 186)
            return ';';
        if (xna is >= 65 and <= 90)
            return char.ToLowerInvariant((char)xna);
        throw new InvalidOperationException(
            $"mania key {XnaKeyName(xna)} cannot be expressed as a key spec; map was [{FormatXna(keys)}].");
    }

    static string FormatXna(int[] keys) => string.Join(" ", keys.Select(XnaKeyName));

    static string XnaKeyName(int value)
    {
        if (value == 0)
            return "None";
        if (value == 32)
            return "Space";
        if (value is >= 65 and <= 90)
            return ((char)value).ToString();
        if (value is >= 48 and <= 57)
            return "D" + (value - 48);
        return value switch
        {
            186 => "OemSemicolon",
            187 => "OemPlus",
            188 => "OemComma",
            189 => "OemMinus",
            190 => "OemPeriod",
            192 => "OemTilde",
            9 => "Tab",
            13 => "Enter",
            16 => "Shift",
            27 => "Escape",
            37 => "Left",
            38 => "Up",
            39 => "Right",
            40 => "Down",
            45 => "Insert",
            112 => "F1",
            113 => "F2",
            114 => "F3",
            115 => "F4",
            118 => "F7",
            119 => "F8",
            120 => "F9",
            121 => "F10",
            123 => "F12",
            160 => "LeftShift",
            161 => "RightShift",
            162 => "LeftControl",
            163 => "RightControl",
            164 => "LeftAlt",
            165 => "RightAlt",
            _ => value.ToString()
        };
    }

    static bool LooksLikeLayout(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length is >= 2 and <= 18;
    }

    readonly record struct Layout(int Columns, int Kind, int[] Keys);
}

readonly record struct ManiaKeyMap(string Spec, ushort[] VirtualKeys);
