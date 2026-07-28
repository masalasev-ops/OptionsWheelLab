using System.Reflection;
using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Composes the configuration key paths a bound options type can read.
/// </summary>
/// <remarks>
/// The key path is the section path and the property name joined by a colon,
/// matching how the configuration binder resolves them, so a nested options
/// class contributes its own segment.
/// </remarks>
internal static class OptionsKeyWalker
{
    /// <summary>
    /// Types that hold a value rather than a group of values, so the walk stops
    /// at them instead of recursing into their members.
    /// </summary>
    private static bool IsLeaf(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(Uri);

    internal static IReadOnlyList<string> KeysOf(BoundSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var keys = new List<string>();
        Walk(section.OptionsType, section.Path, keys, []);
        return keys;
    }

    internal static IReadOnlyList<string> KeysOf(IEnumerable<BoundSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        return sections.SelectMany(KeysOf).ToList();
    }

    private static void Walk(Type type, string prefix, List<string> keys, HashSet<Type> visiting)
    {
        // A type that contains itself would otherwise recurse forever.
        if (!visiting.Add(type))
        {
            return;
        }

        foreach (var property in Settable(type))
        {
            var path = $"{prefix}:{property.Name}";
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (IsLeaf(propertyType))
            {
                keys.Add(path);
                continue;
            }

            // A collection binds by index rather than by name, so its element
            // type contributes no documentable key path of its own.
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType))
            {
                keys.Add(path);
                continue;
            }

            Walk(propertyType, path, keys, visiting);
        }

        visiting.Remove(type);
    }

    private static IEnumerable<PropertyInfo> Settable(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal);
}
