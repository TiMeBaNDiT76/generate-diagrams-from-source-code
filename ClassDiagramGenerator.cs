using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

// ClassDiagramGenerator from https://github.com/pierre3/PlantUmlClassDiagramGenerator
namespace Diagrams
{
    /// <summary>
    /// C#のソースコードからPlantUMLのクラス図を生成するクラス
    /// </summary>
    public class ClassDiagramGenerator : CSharpSyntaxWalker
    {
        private TextWriter writer;
        private string indent;
        private int nestingDepth = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="writer">結果を出力するTextWriter</param>
        /// <param name="indent">インデントとして使用する文字列</param>
        public ClassDiagramGenerator(TextWriter writer, string indent)
        {
            this.writer = writer;
            this.indent = indent;
        }

        /// <summary>
        /// インターフェースの定義をPlantUMLの書式で出力する
        /// </summary>
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitInterfaceDeclaration(node));
        }

        /// <summary>
        /// クラスの定義をPlantUMLの書式で出力する
        /// </summary>        
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitClassDeclaration(node));
        }

        /// <summary>
        /// 構造体の定義をPlantUMLの書式で出力する
        /// </summary>
        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var name = node.Identifier.ToString();
            var typeParam = node.TypeParameterList?.ToString() ?? "";

            WriteLine($"class {name}{typeParam} <<struct>> {{");

            nestingDepth++;
            base.VisitStructDeclaration(node);
            nestingDepth--;

            WriteLine("}");
        }

        /// <summary>
        /// 列挙型の定義をPlantUMLの書式で出力する
        /// </summary>
        /// <param name="node"></param>
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            WriteLine($"{node.EnumKeyword} {node.Identifier} {{");

            nestingDepth++;
            base.VisitEnumDeclaration(node);
            nestingDepth--;

            WriteLine("}");
        }

        /// <summary>
        /// 型（クラス、インターフェース、構造体)の定義をPlantUMLの書式で出力する
        /// </summary>

        private void VisitTypeDeclaration(TypeDeclarationSyntax node, Action visitBase)
        {
            var modifiers = GetTypeModifiersText(node.Modifiers);
            var keyword = (node.Modifiers.Any(SyntaxKind.AbstractKeyword) ? "abstract " : "")
                + node.Keyword.ToString();
            var name = node.Identifier.ToString();
            var typeParam = node.TypeParameterList?.ToString() ?? "";

            WriteLine($"{keyword} {name}{typeParam} {modifiers}{{");

            nestingDepth++;
            visitBase();
            nestingDepth--;

            WriteLine("}");
            
            if (node.BaseList != null)
            {             
                foreach (var b in node.BaseList.Types)
                {
                    WriteLine($"{name} <|-- {b.Type.ToFullString()}");
                }
            }
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var args = node.ParameterList.Parameters.Select(p => $"{p.Identifier}:{p.Type}");

            WriteLine($"{modifiers}{name}({string.Join(", ", args)})");
        }
        /// <summary>
        /// フィールドの定義を出力する
        /// </summary>
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var typeName = node.Declaration.Type.ToString();
            var variables = node.Declaration.Variables;
            foreach (var field in variables)
            {
                var useLiteralInit = field.Initializer?.Value?.Kind().ToString().EndsWith("LiteralExpression") ?? false;
                var initValue = useLiteralInit ? (" = " + field.Initializer.Value.ToString()) : "";

                WriteLine($"{modifiers}{field.Identifier} : {typeName}{initValue}");
            }
        }

        /// <summary>
        /// プロパティの定義を出力する
        /// </summary>        
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var typeName = node.Type.ToString();
            var accessor = node.AccessorList.Accessors
                .Where(x => !x.Modifiers.Select(y => y.Kind()).Contains(SyntaxKind.PrivateKeyword))
                .Select(x => $"<<{(x.Modifiers.ToString() == "" ? "" : (x.Modifiers.ToString() + " "))}{x.Keyword}>>");

            var useLiteralInit = node.Initializer?.Value?.Kind().ToString().EndsWith("LiteralExpression") ?? false;
            var initValue = useLiteralInit ? (" = " + node.Initializer.Value.ToString()) : "";

            WriteLine($"{modifiers}{name} : {typeName} {string.Join(" ", accessor)}{initValue}");
        }

        /// <summary>
        /// メソッドの定義を出力する
        /// </summary>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var returnType = node.ReturnType.ToString();
            var args = node.ParameterList.Parameters.Select(p => $"{p.Identifier}:{p.Type}");

            WriteLine($"{modifiers}{name}({string.Join(", ", args)}) : {returnType}");
        }

        /// <summary>
        /// 列挙型のメンバーを出力する
        /// </summary>
        /// <param name="node"></param>
        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            WriteLine($"{node.Identifier}{node.EqualsValue},");
        }

        /// <summary>
        /// 結果出力用TextWriterに、1行書き込む。
        /// </summary>
        private void WriteLine(string line)
        {
            //行の先頭にネストの階層分だけインデントを付加する
            var space = string.Concat(Enumerable.Repeat(indent, nestingDepth));
            writer.WriteLine(space + line);
        }

        /// <summary>
        /// 型(クラス、インターフェース、構造体)の修飾子を文字列に変換する
        /// </summary>
        /// <param name="modifiers">修飾子のTokenList</param>
        /// <returns>変換後の文字列</returns>
        private string GetTypeModifiersText(SyntaxTokenList modifiers)
        {
            var tokens = modifiers.Select(token =>
            {
                switch (token.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                    case SyntaxKind.PrivateKeyword:
                    case SyntaxKind.ProtectedKeyword:
                    case SyntaxKind.InternalKeyword:
                    case SyntaxKind.AbstractKeyword:
                        return "";
                    default:
                        return $"<<{token.ValueText}>>";
                }
            }).Where(token => token != "");

            var result = string.Join(" ", tokens);
            if (result != string.Empty)
            {
                result += " ";
            };
            return result;
        }

        /// <summary>
        /// 型のメンバーの修飾子を文字列に変換する
        /// </summary>
        /// <param name="modifiers">修飾子のTokenList</param>
        /// <returns></returns>
        private string GetMemberModifiersText(SyntaxTokenList modifiers)
        {
            var tokens = modifiers.Select(token =>
            {
                switch (token.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                        return "+";
                    case SyntaxKind.PrivateKeyword:
                        return "-";
                    case SyntaxKind.ProtectedKeyword:
                        return "#";
                    case SyntaxKind.AbstractKeyword:
                    case SyntaxKind.StaticKeyword:
                        return $"{{{token.ValueText}}}";
                    case SyntaxKind.InternalKeyword:
                    default:
                        return $"<<{token.ValueText}>>";
                }
            });

            var result = string.Join(" ", tokens);
            if (result != string.Empty)
            {
                result += " ";
            };
            return result;
        }
    }
}
