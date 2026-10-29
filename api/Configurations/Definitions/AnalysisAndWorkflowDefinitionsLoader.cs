using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace api.Configurations;

/// <summary>Loads and validates workflow and analysis definitions from YAML.</summary>
public static class AnalysisAndWorkflowDefinitionsLoader
{
    // Unknown properties are rejected so typos in a definition fail at startup.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>Reads every YAML file in the two directories and validates them together.</summary>
    public static AnalysisAndWorkflowDefinitions LoadFromDirectories(
        string workflowsDirectory,
        string analysesDirectory
    ) => Build(ReadFiles(workflowsDirectory), ReadFiles(analysesDirectory));

    /// <summary>Parses and validates the documents, throwing with every problem found.</summary>
    public static AnalysisAndWorkflowDefinitions Build(
        IEnumerable<(string Source, string Yaml)> workflowDocuments,
        IEnumerable<(string Source, string Yaml)> analysisDocuments
    )
    {
        List<string> errors = new();
        var definitions = new AnalysisAndWorkflowDefinitions(
            IndexByName(Parse<WorkflowDefinition>(workflowDocuments, errors), w => w.Name, errors),
            IndexByName(Parse<AnalysisDefinition>(analysisDocuments, errors), a => a.Name, errors)
        );
        if (errors.Count == 0)
        {
            errors.AddRange(
                AnalysisAndWorkflowDefinitionsValidator.ValidateReferences(definitions)
            );
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid workflow/analysis definitions:\n- " + string.Join("\n- ", errors)
            );
        }
        return definitions;
    }

    private static List<(string Source, T Definition)> Parse<T>(
        IEnumerable<(string Source, string Yaml)> documents,
        List<string> errors
    )
        where T : class
    {
        List<(string Source, T Definition)> parsed = new();
        foreach (var (source, yaml) in documents)
        {
            try
            {
                if (Deserializer.Deserialize<T?>(yaml) is { } definition)
                {
                    parsed.Add((source, definition));
                }
                else
                {
                    errors.Add($"{source}: the file is empty");
                }
            }
            catch (YamlException exception)
            {
                errors.Add($"{source}: {exception.Message}");
            }
        }
        return parsed;
    }

    private static Dictionary<string, T> IndexByName<T>(
        List<(string Source, T Definition)> parsed,
        Func<T, string> nameOf,
        List<string> errors
    )
    {
        Dictionary<string, T> byName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> sourceByName = new(StringComparer.OrdinalIgnoreCase);

        foreach (var (source, definition) in parsed)
        {
            var name = nameOf(definition);
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"{source}: 'name' is required");
            }
            else if (!byName.TryAdd(name, definition))
            {
                errors.Add($"{source}: '{name}' is already defined in {sourceByName[name]}");
            }
            else
            {
                sourceByName[name] = source;
            }
        }

        return byName;
    }

    private static IEnumerable<(string Source, string Yaml)> ReadFiles(string directory) =>
        Directory
            .EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));
}
