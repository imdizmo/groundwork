using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Groundwork.Desktop;

/// <summary>
/// The script engine (⋯ → Script…, after the Valheim World Editor's Script tool, MIT): a C# script (top-level statements,
/// with the editor's API: ScriptApi.csx) is compiled in the app (Roslyn, against .NET's reference assemblies carried
/// inside), then run against a snapshot of the map in a process of its own (RunInChild), so that Stop can always end it.
/// What it changes (ground, paint, objects) comes back and goes into the map afterwards as one step in History (Apply).
/// The process is not a sandbox: a script has the user's rights, as any program they run.
/// </summary>
public static partial class ScriptHost
{
    private static readonly Lazy<string> Api = new(() =>
    {
        using Stream s = typeof(ScriptHost).Assembly.GetManifestResourceStream("Groundwork.ScriptApi.csx")
            ?? throw new InvalidOperationException("ScriptApi.csx is not embedded");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    });

    /// <summary>The usings every script gets, the API's globals among them.</summary>
    private const string Usings = "global using System;\nglobal using System.Linq;\nglobal using System.Collections.Generic;\nglobal using static Globals;\n";

    /// <summary>
    /// Compiles a script: its assembly and debug information (for the line an error happens on), or its mistakes
    /// ("line 3: ..." for the script's own lines; 20 at most). Image is null when it does not compile.
    /// </summary>
    public static (byte[]? Image, byte[]? Pdb, List<string> Errors) Compile(string code)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp12);
        SyntaxTree Tree(string text, string path) => CSharpSyntaxTree.ParseText(Microsoft.CodeAnalysis.Text.SourceText.From(text, Encoding.UTF8), parse, path);
        var user = Tree(code, "script");
        if (!((CompilationUnitSyntax)user.GetRoot()).Members.OfType<GlobalStatementSyntax>().Any())
        {
            return (null, null, ["The script has no statements to run."]);
        }
        // Every loop of the script's own stops when asked (Stop, the time limit); a script with syntax mistakes is left
        // as written, so its mistakes are reported as written.
        if (!user.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            user = Tree(new LoopChecks().Visit(user.GetRoot()).ToFullString(), "script");
        }
        var compilation = CSharpCompilation.Create("Script" + Guid.NewGuid().ToString("N")[..8],
            [Tree(Api.Value, "api"), Tree(Usings, "usings"), user],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Disable));
        using var ms = new MemoryStream();
        using var pdb = new MemoryStream();
        var result = compilation.Emit(ms, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d =>
        {
            var span = d.Location.GetLineSpan();
            return span.Path == "script" ? $"line {span.StartLinePosition.Line + 1}: {d.GetMessage()}" : d.GetMessage();
        }).Distinct().Take(20).ToList();
        return (result.Success ? ms.ToArray() : null, result.Success ? pdb.ToArray() : null, errors);
    }

    /// <summary>Where in the script an error happened ("line 12: "), when it did in the script's own code; else empty.</summary>
    public static string Where(Exception ex)
    {
        if (ex is ScriptException e)
        {
            return e.Where;
        }
        var frame = new System.Diagnostics.StackTrace(ex, true).GetFrames().FirstOrDefault(f => f.GetFileName() == "script" && f.GetFileLineNumber() > 0);
        return frame != null ? $"line {frame.GetFileLineNumber()}: " : "";
    }

    /// <summary>A script's error as the output shows it: "line 12: ArgumentException: …".</summary>
    public static string Describe(Exception ex) => $"{Where(ex)}{(ex is ScriptException e ? e.Kind : ex.GetType().Name)}: {ex.Message}";

    /// <summary>
    /// The program and arguments that start this editor as a script's process: the editor's own program when it runs as
    /// one (published), else the dotnet running it with the editor's assembly (a build, the tests).
    /// </summary>
    internal static (string File, string[] Arguments) ChildCommand()
    {
        string? exe = Environment.ProcessPath;
        bool dotnet = exe != null && Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (exe != null && !dotnet && Assembly.GetEntryAssembly() == typeof(ScriptHost).Assembly)
        {
            return (exe, ["--script-host"]);
        }
        string host = dotnet ? exe! : Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        string assembly = Path.Combine(AppContext.BaseDirectory, typeof(ScriptHost).Assembly.GetName().Name + ".dll");
        return (host, [assembly, "--script-host"]);
    }

    /// <summary>
    /// A call to the API's Check at the start of every loop's body and before every goto, so Stop and the time limit end
    /// a script whose own loops call nothing that checks. What it adds holds no line break: the lines keep their numbers.
    /// </summary>
    private sealed class LoopChecks : CSharpSyntaxRewriter
    {
        private static readonly StatementSyntax Check = SyntaxFactory.ParseStatement("global::__Host.Check();");

        /// <summary>The statement with the Check before it, in a block of their own, its trivia kept outside the block.</summary>
        private static BlockSyntax Checked(StatementSyntax statement) =>
            SyntaxFactory.Block(SyntaxFactory.Token(default, SyntaxKind.OpenBraceToken, default), [Check, statement.WithoutTrivia()], SyntaxFactory.Token(default, SyntaxKind.CloseBraceToken, default))
                .WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(statement.GetTrailingTrivia());

        /// <inheritdoc/>
        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
        {
            var n = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
        {
            var n = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var n = (ForStatementSyntax)base.VisitForStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var n = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            var n = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node) => Checked((GotoStatementSyntax)base.VisitGotoStatement(node)!);
    }
}
