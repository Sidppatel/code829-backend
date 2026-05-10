using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EventPlatform.Analyzers;

/// <summary>
/// EP0002: Forbid <c>PublicQuoteDto</c> construction outside
/// <c>PricingService.CalculatePublicQuoteAsync</c>. Stops accidental drift where
/// callers hand-build a public quote and skip the pricing-rule layer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicQuoteConstructionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EP0002";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "PublicQuoteDto construction is restricted",
        messageFormat: "PublicQuoteDto must only be constructed by PricingService.CalculatePublicQuoteAsync",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hand-built PublicQuoteDto values bypass the pricing-rule layer. Always go through PricingService.");
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        ITypeSymbol? type = ctx.Node switch
        {
            ObjectCreationExpressionSyntax o => ctx.SemanticModel.GetTypeInfo(o).Type,
            ImplicitObjectCreationExpressionSyntax i => ctx.SemanticModel.GetTypeInfo(i).Type,
            _ => null
        };
        if (type is null || type.Name != "PublicQuoteDto") return;

        var method = ctx.Node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null) return;
        var klass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (klass is null) return;

        if (klass.Identifier.Text == "PricingService"
            && method.Identifier.Text == "CalculatePublicQuoteAsync")
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Node.GetLocation()));
    }
}

/// <summary>
/// EP0003: Public controllers (in <c>api/Controllers/</c> not prefixed
/// <c>Admin</c> or <c>Developer</c>) must not reference the admin breakdown
/// surface — <c>AdminQuoteDto</c>, <c>PricingComputation</c>, or any property
/// access whose name ends in <c>SubtotalCents</c> / <c>FeeCents</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicControllerBreakdownAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EP0003";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Public controllers must not surface pricing breakdown",
        messageFormat: "Public controller references '{0}' — breakdown DTOs and per-line subtotal/fee fields are admin/developer only",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Public surfaces show one display total. Subtotal, fee, tax breakdown is for admin/developer/internal use only.");
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static bool IsPublicControllerFile(string filePath)
    {
        var normalized = (filePath ?? string.Empty).Replace('\\', '/');
        var idx = normalized.IndexOf("/api/Controllers/", StringComparison.Ordinal);
        if (idx < 0) return false;
        var fileName = System.IO.Path.GetFileName(normalized);
        if (fileName.StartsWith("Admin", StringComparison.Ordinal)) return false;
        if (fileName.StartsWith("Developer", StringComparison.Ordinal)) return false;
        return true;
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext ctx)
    {
        if (!IsPublicControllerFile(ctx.Node.SyntaxTree.FilePath)) return;
        var name = ((IdentifierNameSyntax)ctx.Node).Identifier.Text;
        if (name == "AdminQuoteDto" || name == "PricingComputation")
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Node.GetLocation(), name));
        }
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
    {
        if (!IsPublicControllerFile(ctx.Node.SyntaxTree.FilePath)) return;
        var ma = (MemberAccessExpressionSyntax)ctx.Node;
        var name = ma.Name.Identifier.Text;
        if (name.EndsWith("SubtotalCents", StringComparison.Ordinal)
            || name.EndsWith("FeeCents", StringComparison.Ordinal))
        {

            if (name == "PlatformFeeCents") return;
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ma.Name.GetLocation(), name));
        }
    }
}

/// <summary>
/// EP0004: Quote-DTO records (any record whose name ends in <c>QuoteDto</c>
/// except <c>AdminQuoteDto</c>) must not declare properties whose names end
/// in <c>SubtotalCents</c> / <c>FeeCents</c>. Catches breakdown leak at the
/// DTO definition layer too.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuoteDtoBreakdownAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EP0004";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Quote DTOs must not expose breakdown fields",
        messageFormat: "Quote DTO '{0}' declares breakdown property '{1}' — only AdminQuoteDto may carry subtotal/fee fields",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Public/checkout quote DTOs surface a single display total. Breakdown belongs on AdminQuoteDto only.");
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx)
    {
        var record = (RecordDeclarationSyntax)ctx.Node;
        var name = record.Identifier.Text;
        if (!name.EndsWith("QuoteDto", StringComparison.Ordinal)) return;
        if (name == "AdminQuoteDto") return;

        if (record.ParameterList is null) return;
        foreach (var p in record.ParameterList.Parameters)
        {
            var paramName = p.Identifier.Text;
            if (paramName.EndsWith("SubtotalCents", StringComparison.Ordinal)
                || paramName.EndsWith("FeeCents", StringComparison.Ordinal))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, p.GetLocation(), name, paramName));
            }
        }
    }
}
