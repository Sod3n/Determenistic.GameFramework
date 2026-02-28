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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NetworkIdCodeFixProvider)), Shared]
    public class NetworkIdCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(NetworkIdAnalyzer.DiagnosticId);

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
                    title: "Generate NetworkId Attribute",
                    createChangedDocument: c => AddNetworkIdAsync(context.Document, declaration, c),
                    equivalenceKey: "Generate NetworkId Attribute"),
                diagnostic);
        }

        private async Task<Document> AddNetworkIdAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return document;
            
            var compilation = semanticModel.Compilation;

            string typeName = typeDecl.Identifier.Text;
            int generatedId = System.Math.Abs(typeName.GetHashCode());

            var attributeString = $"[Deterministic.GameFramework.CoreV2.NetworkId({generatedId})]\n";
            var parsedAttributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory.ParseName("Deterministic.GameFramework.CoreV2.NetworkId"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(generatedId))))))))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            var newTypeDecl = typeDecl.AddAttributeLists(parsedAttributeList);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document;

            var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
