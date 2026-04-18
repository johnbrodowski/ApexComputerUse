using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var config = AppConfig.Parse(args);
var sourceFiles = Directory
    .EnumerateFiles(config.RootDirectory, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (sourceFiles.Count == 0)
{
    Console.WriteLine($"No C# files found under '{config.RootDirectory}'.");
    return;
}

Directory.CreateDirectory(config.OutputDirectory);

var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
var trees = sourceFiles
    .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
    .ToList();

var refs = BuildMetadataReferences();
var compilation = CSharpCompilation.Create(
    assemblyName: "ProjectMapBuilderCompilation",
    syntaxTrees: trees,
    references: refs,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var methodsByKey = new Dictionary<string, MethodNode>(StringComparer.Ordinal);
var calls = new HashSet<CallEdge>(new CallEdgeComparer());

foreach (var tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    var root = tree.GetRoot();

    foreach (var declaration in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
        var symbol = model.GetDeclaredSymbol(declaration);
        if (symbol is null)
        {
            continue;
        }

        var key = BuildMethodKey(symbol);
        if (!methodsByKey.ContainsKey(key))
        {
            methodsByKey[key] = BuildMethodNode(symbol, declaration.SyntaxTree.FilePath, declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }
    }
}

foreach (var tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    var root = tree.GetRoot();

    foreach (var methodDecl in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
        var callerSymbol = model.GetDeclaredSymbol(methodDecl);
        if (callerSymbol is null)
        {
            continue;
        }

        var callerKey = BuildMethodKey(callerSymbol);
        if (!methodsByKey.ContainsKey(callerKey))
        {
            continue;
        }

        var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var calleeSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (calleeSymbol is null)
            {
                continue;
            }

            var calleeKey = BuildMethodKey(calleeSymbol);
            if (methodsByKey.ContainsKey(calleeKey))
            {
                calls.Add(new CallEdge(callerKey, calleeKey, "invoke"));
            }
        }

        var objectCreations = methodDecl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var ctorSymbol = model.GetSymbolInfo(creation).Symbol as IMethodSymbol;
            if (ctorSymbol is null)
            {
                continue;
            }

            var ctorKey = BuildMethodKey(ctorSymbol);
            if (methodsByKey.ContainsKey(ctorKey))
            {
                calls.Add(new CallEdge(callerKey, ctorKey, "new"));
            }
        }
    }
}

var map = new ProjectMap(
    GeneratedAtUtc: DateTime.UtcNow,
    RootDirectory: Path.GetFullPath(config.RootDirectory),
    TotalSourceFiles: sourceFiles.Count,
    Nodes: methodsByKey.Values.OrderBy(x => x.Namespace).ThenBy(x => x.TypeName).ThenBy(x => x.MethodName).ToList(),
    Edges: calls.OrderBy(x => x.From).ThenBy(x => x.To).ThenBy(x => x.Kind).ToList());

var jsonPath = Path.Combine(config.OutputDirectory, "project-map.json");
var mdPath = Path.Combine(config.OutputDirectory, "project-map.md");

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(jsonPath, JsonSerializer.Serialize(map, jsonOptions));
File.WriteAllText(mdPath, BuildMarkdown(map));

Console.WriteLine($"Map generated:\n- {jsonPath}\n- {mdPath}");
Console.WriteLine($"Nodes: {map.Nodes.Count}, Edges: {map.Edges.Count}, Files scanned: {map.TotalSourceFiles}");

static string BuildMethodKey(IMethodSymbol symbol)
{
    var fqType = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    var method = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    return $"{fqType}::{method}";
}

static MethodNode BuildMethodNode(IMethodSymbol symbol, string filePath, int line)
{
    var signature = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    return new MethodNode(
        Id: BuildMethodKey(symbol),
        Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
        TypeName: symbol.ContainingType?.Name ?? string.Empty,
        MethodName: symbol.Name,
        Signature: signature,
        FilePath: filePath,
        Line: line,
        Accessibility: symbol.DeclaredAccessibility.ToString(),
        IsStatic: symbol.IsStatic);
}

static IReadOnlyList<MetadataReference> BuildMetadataReferences()
{
    var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
        ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        ?? Array.Empty<string>();

    var refs = tpa
        .Select(path => MetadataReference.CreateFromFile(path))
        .Cast<MetadataReference>()
        .ToList();

    var extra = new[]
    {
        typeof(object).Assembly,
        typeof(Enumerable).Assembly,
        typeof(Console).Assembly,
        typeof(Uri).Assembly,
        Assembly.Load("System.Runtime")
    };

    foreach (var asm in extra)
    {
        if (!string.IsNullOrWhiteSpace(asm.Location) && File.Exists(asm.Location))
        {
            refs.Add(MetadataReference.CreateFromFile(asm.Location));
        }
    }

    return refs;
}

static string BuildMarkdown(ProjectMap map)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Project Map");
    builder.AppendLine();
    builder.AppendLine($"- Generated (UTC): {map.GeneratedAtUtc:O}");
    builder.AppendLine($"- Root directory: `{map.RootDirectory}`");
    builder.AppendLine($"- Source files scanned: {map.TotalSourceFiles}");
    builder.AppendLine($"- Methods/constructors discovered: {map.Nodes.Count}");
    builder.AppendLine($"- Internal call edges discovered: {map.Edges.Count}");
    builder.AppendLine();

    builder.AppendLine("## Top callers");
    builder.AppendLine();

    var topCallers = map.Edges
        .GroupBy(x => x.From)
        .Select(g => new { Caller = g.Key, Count = g.Count() })
        .OrderByDescending(x => x.Count)
        .ThenBy(x => x.Caller)
        .Take(30)
        .ToList();

    if (topCallers.Count == 0)
    {
        builder.AppendLine("No internal calls found.");
    }
    else
    {
        foreach (var row in topCallers)
        {
            builder.AppendLine($"- {row.Count,4} → `{row.Caller}`");
        }
    }

    builder.AppendLine();
    builder.AppendLine("## Sample edges");
    builder.AppendLine();

    foreach (var edge in map.Edges.Take(200))
    {
        builder.AppendLine($"- `{edge.From}` --({edge.Kind})-> `{edge.To}`");
    }

    return builder.ToString();
}

internal sealed record AppConfig(string RootDirectory, string OutputDirectory)
{
    public static AppConfig Parse(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var output = Path.Combine(root, "docs", "project-map");

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                root = args[++i];
                continue;
            }

            if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                output = args[++i];
            }
        }

        return new AppConfig(root, output);
    }
}

internal sealed record ProjectMap(
    DateTime GeneratedAtUtc,
    string RootDirectory,
    int TotalSourceFiles,
    IReadOnlyList<MethodNode> Nodes,
    IReadOnlyList<CallEdge> Edges);

internal sealed record MethodNode(
    string Id,
    string Namespace,
    string TypeName,
    string MethodName,
    string Signature,
    string FilePath,
    int Line,
    string Accessibility,
    bool IsStatic);

internal sealed record CallEdge(string From, string To, string Kind);

internal sealed class CallEdgeComparer : IEqualityComparer<CallEdge>
{
    public bool Equals(CallEdge? x, CallEdge? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        return string.Equals(x.From, y.From, StringComparison.Ordinal)
            && string.Equals(x.To, y.To, StringComparison.Ordinal)
            && string.Equals(x.Kind, y.Kind, StringComparison.Ordinal);
    }

    public int GetHashCode(CallEdge obj)
    {
        return HashCode.Combine(obj.From, obj.To, obj.Kind);
    }
}
