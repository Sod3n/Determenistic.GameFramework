using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Deterministic.GameFramework.SourceGenerators
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StructLayoutCodeFixProvider)), Shared]
    public class StructLayoutCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add [StructLayout(LayoutKind.Sequential, Pack = 1)]";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(StructLayoutAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent?
                .AncestorsAndSelf()
                .OfType<StructDeclarationSyntax>()
                .FirstOrDefault();
            if (declaration == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddStructLayoutAsync(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> AddStructLayoutAsync(
            Document document, StructDeclarationSyntax structDecl, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document;

            // Remove any existing StructLayout attribute
            var newStructDecl = RemoveExistingStructLayout(structDecl);

            // Build: [StructLayout(LayoutKind.Sequential, Pack = 1)]
            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("StructLayout"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("LayoutKind"),
                                SyntaxFactory.IdentifierName("Sequential"))),
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(1)))
                            .WithNameEquals(
                                SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Pack")))
                    })));

            var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(attribute))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            // Insert the StructLayout attribute as the first attribute
            var updatedStructDecl = newStructDecl.WithAttributeLists(
                newStructDecl.AttributeLists.Insert(0, attributeList));

            var newRoot = root.ReplaceNode(structDecl, updatedStructDecl);

            // Ensure using System.Runtime.InteropServices is present
            newRoot = EnsureUsingDirective(newRoot, "System.Runtime.InteropServices");

            return document.WithSyntaxRoot(newRoot);
        }

        private static StructDeclarationSyntax RemoveExistingStructLayout(StructDeclarationSyntax structDecl)
        {
            var newAttributeLists = new SyntaxList<AttributeListSyntax>();

            foreach (var attrList in structDecl.AttributeLists)
            {
                var filteredAttributes = attrList.Attributes
                    .Where(a =>
                    {
                        var name = a.Name.ToString();
                        return name != "StructLayout" && name != "StructLayoutAttribute"
                            && name != "System.Runtime.InteropServices.StructLayout"
                            && name != "System.Runtime.InteropServices.StructLayoutAttribute";
                    })
                    .ToArray();

                if (filteredAttributes.Length == attrList.Attributes.Count)
                {
                    // No StructLayout in this list, keep as-is
                    newAttributeLists = newAttributeLists.Add(attrList);
                }
                else if (filteredAttributes.Length > 0)
                {
                    // Some attributes remain after removing StructLayout
                    var newList = attrList.WithAttributes(SyntaxFactory.SeparatedList(filteredAttributes));
                    newAttributeLists = newAttributeLists.Add(newList);
                }
                // else: entire attribute list was just StructLayout, drop it
            }

            return structDecl.WithAttributeLists(newAttributeLists);
        }

        private static SyntaxNode EnsureUsingDirective(SyntaxNode root, string namespaceName)
        {
            if (root is CompilationUnitSyntax compilationUnit)
            {
                bool hasUsing = compilationUnit.Usings
                    .Any(u => u.Name?.ToString() == namespaceName);

                if (!hasUsing)
                {
                    var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))
                        .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

                    return compilationUnit.AddUsings(usingDirective);
                }
            }

            return root;
        }
    }
}
