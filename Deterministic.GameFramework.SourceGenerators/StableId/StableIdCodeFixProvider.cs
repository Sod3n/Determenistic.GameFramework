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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StableIdCodeFixProvider)), Shared]
    public class StableIdCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(StableIdAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (declaration == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Generate StableId Attribute",
                    createChangedDocument: c => AddStableIdAsync(context.Document, declaration, c),
                    equivalenceKey: "Generate StableId Attribute"),
                diagnostic);
        }

        private async Task<Document> AddStableIdAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return document;
            
            var compilation = semanticModel.Compilation;

            // Generate a random GUID for the StableId
            var generatedId = System.Guid.NewGuid();
            var guidString = generatedId.ToString();

            // Create the attribute: [Deterministic.GameFramework.Core.StableId("guid-string")]
            var parsedAttributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory.ParseName("Deterministic.GameFramework.Core.StableId"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(guidString))))))))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            var newTypeDecl = typeDecl.AddAttributeLists(parsedAttributeList);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document;

            var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
