namespace Full_Metal_Paintball_Carmagnola.Helpers;

public static class StaffAssignmentOptions
{
    public static string? OnCallName(string? value)
    {
        var name = value?.Trim();
        return string.IsNullOrWhiteSpace(name)
            || name.Equals("In attesa", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Campo chiuso", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Nessuno", StringComparison.OrdinalIgnoreCase)
            || name == "--"
            ? null : name;
    }

    public static bool IsOnCall(string? name, string? onCall) =>
        OnCallName(onCall) is string validName
        && string.Equals(name?.Trim(), validName, StringComparison.OrdinalIgnoreCase);

    public static List<string> ForDay(IEnumerable<string> available, IEnumerable<string> active, string? onCall)
    {
        var activeNames = active.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = available.Where(activeNames.Contains).ToList();
        if (OnCallName(onCall) is string name)
            names.Add(name);
        return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
    }
}
