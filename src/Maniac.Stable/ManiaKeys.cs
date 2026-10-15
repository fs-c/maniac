namespace Maniac.Stable;

static class ManiaKeys
{
    public static string Default(int columns) => columns switch
    {
        1 => "d",
        2 => "fj",
        3 => "dfj",
        4 => "dfjk",
        5 => "df-jk",
        6 => "sdfjkl",
        7 => "sdf-jkl",
        8 => "asdfjkl;",
        9 => "asdf-jkl;",
        _ => throw new InvalidOperationException($"no default key map for {columns}K")
    };

    public static ushort[] Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new InvalidOperationException("key map is empty.");

        var keys = new ushort[spec.Length];
        for (int i = 0; i < spec.Length; i++)
            keys[i] = VirtualKey(spec[i], spec);
        return keys;
    }

    static ushort VirtualKey(char c, string spec)
    {
        if (c is '-' or '_')
            return 0x20;
        if (c == ';')
            return 0xBA;
        char upper = char.ToUpperInvariant(c);
        if (upper is >= 'A' and <= 'Z')
            return upper;
        throw new InvalidOperationException($"key map '{spec}' has '{c}'; use letters, '-' for space, ';' for semicolon.");
    }
}
