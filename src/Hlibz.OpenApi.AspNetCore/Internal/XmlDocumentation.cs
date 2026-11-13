using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Hlibz.OpenApi.Internal;

/// <summary>
/// Reads the <c>&lt;summary&gt;</c> of a type or property from the XML documentation file next
/// to its assembly (<c>GenerateDocumentationFile</c> in a project; a package's own XML file,
/// which the build copies to the output with <c>CopyDocumentationFilesFromPackages</c>),
/// flattened to one line: inline code and keywords as markdown code, a <c>&lt;see cref&gt;</c> as
/// the name it refers to. <c>&lt;remarks&gt;</c> are never read, so
/// notes meant only for developers stay out of the API docs.
/// </summary>
internal static class XmlDocumentation
{
    private static readonly char[] CrefSeparators = [':', '.'];

    private static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<string, string>>
        Summaries = new();

    /// <summary>
    /// Gets the summary of <paramref name="type"/> (a closed generic type's, from its
    /// definition).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The summary, or <see langword="null"/> when there is none to read.</returns>
    public static string? TypeSummary(Type type) =>
        DocumentationName(type) is { } name ? Summary(type.Assembly, "T:" + name) : null;

    /// <summary>
    /// Gets the summary of <paramref name="property"/>, read from the type that declares it; for a
    /// record's positional property, the record's <c>&lt;param&gt;</c> of the same name.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The summary, or <see langword="null"/> when there is none to read.</returns>
    public static string? PropertySummary(PropertyInfo property) =>
        property.DeclaringType is { } declaringType && DocumentationName(declaringType) is { } name
            ? Summary(declaringType.Assembly, $"P:{name}.{property.Name}")
            : null;

    /// <summary>
    /// Gets where an assembly's XML documentation file is expected: next to the assembly, or, for
    /// an assembly bundled into a single-file app (no location), in the app's directory, where
    /// publishing leaves the XML files.
    /// </summary>
    /// <param name="location">The assembly's location; empty when it has none.</param>
    /// <param name="assemblyName">The assembly's simple name.</param>
    /// <param name="baseDirectory">The app's directory.</param>
    /// <returns>The path of the XML file, which may not exist.</returns>
    internal static string DocumentationPath(string location, string assemblyName, string baseDirectory) =>
        string.IsNullOrEmpty(location)
            ? Path.Combine(baseDirectory, assemblyName + ".xml")
            : Path.ChangeExtension(location, ".xml");

    private static string? Summary(Assembly assembly, string memberId) =>
        Summaries.GetOrAdd(assembly, Load).GetValueOrDefault(memberId);

    /// <summary>
    /// The name a type is documented under: nested types joined with a dot, and a closed generic
    /// type (<c>PagedData&lt;City&gt;</c>) under its definition.
    /// </summary>
    private static string? DocumentationName(Type type)
    {
        Type definition = type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;
        return definition.FullName?.Replace('+', '.');
    }

    private static Dictionary<string, string> Load(Assembly assembly)
    {
        Dictionary<string, string> summaries = new(StringComparer.Ordinal);
        if (assembly.GetName().Name is not { } assemblyName)
        {
            return summaries;
        }

        string path = DocumentationPath(Location(assembly), assemblyName, AppContext.BaseDirectory);
        if (!File.Exists(path))
        {
            return summaries;
        }

        Dictionary<string, string> recordParameters = new(StringComparer.Ordinal);
        foreach (XElement member in XDocument.Load(path).Descendants("member"))
        {
            if (member.Attribute("name")?.Value is not { } name)
            {
                continue;
            }

            if (member.Element("summary") is { } summary && Flatten(summary) is { Length: > 0 } text)
            {
                summaries[name] = text;
            }

            // A positional record property is documented by the record's <param> tag.
            if (name.StartsWith("T:", StringComparison.Ordinal))
            {
                foreach (XElement parameter in member.Elements("param"))
                {
                    if (parameter.Attribute("name")?.Value is { } parameterName
                        && Flatten(parameter) is { Length: > 0 } parameterText)
                    {
                        recordParameters[$"P:{name[2..]}.{parameterName}"] = parameterText;
                    }
                }
            }
        }

        foreach ((string property, string text) in recordParameters)
        {
            summaries.TryAdd(property, text);
        }

        return summaries;
    }

    // Assembly.Location is empty in a single-file app (IL3000); DocumentationPath handles that.
    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "An empty location falls back to the app's directory.")]
    private static string Location(Assembly assembly) => assembly.Location;

    private static string Flatten(XElement element)
    {
        StringBuilder builder = new();
        Append(builder, element);
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void Append(StringBuilder builder, XElement element)
    {
        foreach (XNode node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement { Name.LocalName: "c" or "code" } code:
                    builder.Append('`').Append(code.Value.Trim()).Append('`');
                    break;
                case XElement { Name.LocalName: "see" or "seealso" } see:
                    AppendReference(builder, see);
                    break;
                case XElement { Name.LocalName: "paramref" or "typeparamref" } reference:
                    builder.Append('`').Append(reference.Attribute("name")?.Value).Append('`');
                    break;
                case XElement other:
                    Append(builder, other);
                    break;
                default:
                    break;
            }
        }
    }

    private static void AppendReference(StringBuilder builder, XElement see)
    {
        if (!see.IsEmpty)
        {
            Append(builder, see);
            return;
        }

        if (see.Attribute("langword")?.Value is { } langword)
        {
            builder.Append('`').Append(langword).Append('`');
            return;
        }

        builder.Append(see.Attribute("href")?.Value ?? ShortName(see.Attribute("cref")?.Value));
    }

    /// <summary>
    /// A cref as a reader would name it: a type by its name (<c>PagedData</c>, not
    /// <c>PagedData`1</c>), a member with its type (<c>City.Name</c>).
    /// </summary>
    private static string? ShortName(string? cref)
    {
        if (cref is null)
        {
            return null;
        }

        int parameters = cref.IndexOf('(', StringComparison.Ordinal);
        string name = parameters < 0 ? cref : cref[..parameters];
        string[] segments = [.. name.Split(CrefSeparators).Select(segment => segment.Split('`')[0])];

        return cref.StartsWith("T:", StringComparison.Ordinal) || segments.Length < 3
            ? segments[^1]
            : $"{segments[^2]}.{segments[^1]}";
    }
}
