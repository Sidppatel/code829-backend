using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EventPlatform.Analyzers;

/// <summary>
/// Blocks direct EF Core LINQ access to non-view DbSets outside allowed paths.
/// The architectural rule is: reads and writes go through SPs, functions, or views.
/// View DbSets (property name ending in "Views") are allowed.
/// tests/** paths are exempt.
/// Per-method opt-out via [AllowDirectDbAccess] attribute or
/// a line-level `// ARCH-EXCEPTION:` comment.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectDbSetAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EP0001";

    private static readonly LocalizableString Title =
        "Direct EF DbSet access is forbidden";

    private static readonly LocalizableString MessageFormat =
        "Direct access to '{0}.{1}' is forbidden outside seeders/tests. Use an SP/function wrapper or a view DbSet instead. Whitelist with [AllowDirectDbAccess] or `// ARCH-EXCEPTION: <reason>` on the line.";

    private static readonly LocalizableString Description =
        "The Event Platform API must never read or write tables directly via EF LINQ. Route access through Db.Repositories.StoredProcedures.* or keyless view DbSets.";

    // Severity is Error: the backlog has been cleared. Existing intentional exceptions use
    // `// ARCH-EXCEPTION: <reason>` line comments with justification. New direct-DbSet access
    // fails the build.
#pragma warning disable RS2008 // Single-rule analyzer; release-tracking infra is overkill here.
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);
#pragma warning restore RS2008

    // Raw-SQL escape hatches. These are how SP-backed access threads through the DbSet
    // property without actually reading tables directly, and are expressly allowed.
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.Ordinal)
    {
        "FromSqlRaw", "FromSqlInterpolated", "FromSql"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
    {
        var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;

        // Match the pattern: <something>.<DbSetProperty>.<Method>(...).
        // This node is the inner ".DbSetProperty" when part of a call expression.
        if (memberAccess.Parent is not MemberAccessExpressionSyntax outer)
            return;

        // The outer member must be invoked.
        if (outer.Parent is not InvocationExpressionSyntax invocation)
            return;

        var methodName = outer.Name.Identifier.ValueText;

        // Raw SQL escape hatches are allowed on any DbSet — that's the whole point.
        if (AllowedMethods.Contains(methodName))
            return;

        // The inner access must be on a property that returns DbSet<T>.
        var propertySymbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IPropertySymbol;
        if (propertySymbol is null)
            return;

        if (!IsDbSet(propertySymbol.Type, out _))
            return;

        // Views are allowed.
        var propertyName = propertySymbol.Name;
        if (propertyName.EndsWith("Views", StringComparison.Ordinal))
            return;

        // Path-based whitelist: seeders and tests.
        var filePath = invocation.SyntaxTree.FilePath ?? string.Empty;
        if (IsWhitelistedPath(filePath))
            return;

        // [AllowDirectDbAccess] attribute on the containing method, class, or assembly.
        if (HasAllowDirectDbAccessAttribute(ctx, invocation))
            return;

        // Line-level escape hatch: `// ARCH-EXCEPTION: ...` comment on the invocation line.
        if (HasArchExceptionComment(invocation))
            return;

        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            propertyName,
            methodName);
        ctx.ReportDiagnostic(diagnostic);
    }

    private static bool IsDbSet(ITypeSymbol type, out string entityName)
    {
        entityName = string.Empty;
        if (type is not INamedTypeSymbol named) return false;
        if (named.ConstructedFrom.ToDisplayString() != "Microsoft.EntityFrameworkCore.DbSet<TEntity>")
            return false;
        if (named.TypeArguments.Length != 1) return false;
        entityName = named.TypeArguments[0].Name;
        return true;
    }

    private static bool IsWhitelistedPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        return normalized.Contains("/tests/")
            || normalized.Contains("/Tests/")
            || normalized.Contains(".Tests/")
            // api/Data/Repositories/*.cs (excluding the StoredProcedures/ subfolder)
            // are low-level data-access adapters below the API layer. The rule targets
            // controllers + services; adapters wrapping EF primitives for non-SP-
            // covered surfaces (legacy log/image/app_setting CRUD) are permitted here.
            // StoredProcedures/ files under this folder are still checked.
            || (normalized.Contains("/api/Data/Repositories/")
                && !normalized.Contains("/api/Data/Repositories/StoredProcedures/"));
    }

    private static bool HasAllowDirectDbAccessAttribute(SyntaxNodeAnalysisContext ctx, SyntaxNode node)
    {
        SyntaxNode? current = node;
        while (current != null)
        {
            if (current is MethodDeclarationSyntax method)
                return HasAttribute(ctx, method.AttributeLists);
            if (current is ClassDeclarationSyntax klass)
                return HasAttribute(ctx, klass.AttributeLists);
            if (current is PropertyDeclarationSyntax prop)
                return HasAttribute(ctx, prop.AttributeLists);
            current = current.Parent;
        }
        return false;
    }

    private static bool HasAttribute(SyntaxNodeAnalysisContext ctx, SyntaxList<AttributeListSyntax> attrLists)
    {
        foreach (var list in attrLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == "AllowDirectDbAccess" || name.EndsWith(".AllowDirectDbAccess", StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    private static bool HasArchExceptionComment(InvocationExpressionSyntax invocation)
    {
        // Check trivia on the statement containing this invocation.
        var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null) return false;

        var leading = statement.GetLeadingTrivia();
        var trailing = statement.GetTrailingTrivia();
        foreach (var t in leading.Concat(trailing))
        {
            if (!t.IsKind(SyntaxKind.SingleLineCommentTrivia) && !t.IsKind(SyntaxKind.MultiLineCommentTrivia))
                continue;
            var text = t.ToString();
            if (text.Contains("ARCH-EXCEPTION"))
                return true;
        }
        return false;
    }
}
