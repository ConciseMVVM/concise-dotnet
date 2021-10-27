using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#nullable enable

namespace Concise.Generators
{
    public class DebugIndent : IDisposable
    {
        public DebugIndent()
        {
            Debug.Indent();
        }

        public void Dispose()
        {
            Debug.Unindent();
        }
    }

    public class ObservableObjectSourceGenerator
    {
        readonly struct Indention
        {
            const int IndentSize = 4;

            public readonly int Level;

            public Indention(int level = 0) =>
                Level = level;

            public override string ToString() =>
                new(' ', Level * IndentSize);

            public static Indention operator +(Indention a, int b) =>
                new(a.Level + b);

            public static Indention operator -(Indention a, int b) =>
                new(a.Level - b);

            public static Indention operator ++(Indention a) =>
                new(a.Level + 1);

            public static Indention operator --(Indention a) =>
                new(a.Level - 1);
        }

        class Property
        {
            public string Name { get; }
            public string Type { get; }
            public bool IsExpr { get; }
            public string GetAccess { get; }
            public string SetAccess { get; }
            public string DefaultValue { get; }

            public static string DetermineDefaultValue(IPropertySymbol propertySymbol)
            {
                var defaultAttribute = propertySymbol.GetAttributes().FirstOrDefault((a) => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DefaultValueAttribute");

                if (defaultAttribute != null)
                    return defaultAttribute.ConstructorArguments.Last().ToCSharpString();

                if (propertySymbol.Type.ToDisplayString() == "string")
                    return "\"\""; // non-nullable strings are special, we use "" as rhe default

                return "default";
            }

            public Property(IPropertySymbol propertySymbol)
            {
                Name = propertySymbol.Name;
                Type = propertySymbol.Type.ToDisplayString();
                IsExpr = propertySymbol.SetMethod == null;

                GetAccess = (propertySymbol.DeclaredAccessibility == Accessibility.Public) ? "public" : "protected";
                SetAccess =  (IsExpr) ? "" : (propertySymbol.SetMethod?.DeclaredAccessibility == Accessibility.Public) ? "public" : "protected";

                DefaultValue = (IsExpr) ? "" : DetermineDefaultValue(propertySymbol);
            }

            public string AddVariableLine()
            {
                if (IsExpr)
                    return $"AddExpression<{Type}>(nameof({Name}), () => base.{Name});";
                else
                    return $"AddVariable<{Type}>(nameof({Name}), {DefaultValue});";
            }

            public string PropertyLine()
            {
                var s = new StringBuilder();

                s.Append($"{GetAccess} override {Type} {Name} ");

                if (IsExpr)
                {
                    s.Append($"=> GetValue<{Type}>();");
                }

                else
                {
                    s.Append($"{{ get => GetValue<{Type}>(); ");

                    if (GetAccess != SetAccess)
                        s.Append($"{SetAccess} ");

                    s.Append($"set => SetValue(value); }}");
                }

                return s.ToString();
            }
        }

        class Constructor
        {
            public string Text { get; }

            private static string? GetDefaultValue(IParameterSymbol p)
            {
                if (p.HasExplicitDefaultValue == false)
                    return null;

                if (p.ExplicitDefaultValue == null)
                    return "null";

                if (p.ExplicitDefaultValue is string s)
                    return $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

                if (p.ExplicitDefaultValue is bool b)
                    return b ? "true" : "false";

                return p.ExplicitDefaultValue.ToString();
            }

            public Constructor(string className, IMethodSymbol c)
            {
                string DefaultValue(IParameterSymbol p) =>
                    (p.HasExplicitDefaultValue) ? $" = {GetDefaultValue(p)}" : "";
    
                var parameters = string.Join(", ", c.Parameters.Select((p) => $"{p.Type.ToDisplayString()} {p.Name}{DefaultValue(p)}"));
                var args = string.Join(", ", c.Parameters.Select((p) => p.Name));

                Text = $"public {className}({parameters}) : base({args}) {{}}";
            }
        }

        class Scope
        {
            public string Type { get; }
            public string Name { get; }
            public string Text => $"{Type} {Name}";

            public Scope(string type, string name) =>
                (Type, Name) = (type, name);
        }

        private static IEnumerable<Scope> GetScopes(ITypeSymbol type)
        {
            while (type.ContainingType != null && type.ContainingType.IsType)
            {
                type = (ITypeSymbol)type.ContainingType;
                yield return new Scope("partial class", type.Name.ToString());
            }

            if (type.ContainingNamespace != null)
            {
                yield return new Scope("namespace", type.ContainingNamespace.ToDisplayString());
            }
        }

        private static bool HasBaseType(ITypeSymbol type, string baseClassName)
        {
            var baseType = type.BaseType;

            if (baseType == null)
                return false;

            if (baseType.ToDisplayString() == baseClassName)
                return true;

            return HasBaseType(baseType, baseClassName);
        }

        public string BaseClassName { get; }
        public string ClassName { get; }
        public string FullClassName { get; }

        Property[] Properties { get; }
        Constructor[] Constructors { get; }
        Scope[] Scopes { get; }

        ObservableObjectSourceGenerator(ClassDeclarationSyntax decl, ITypeSymbol type)
        {
            var baseType = type.BaseType!;

            BaseClassName = baseType.Name;
            ClassName = type.Name;

            Properties = baseType.GetMembers()
                .Where((m) => (m.IsAbstract || m.IsVirtual) && (m.DeclaredAccessibility == Accessibility.Public || m.DeclaredAccessibility == Accessibility.Protected) && m is IPropertySymbol)
                .Select((m) => new Property((IPropertySymbol)m))
                .ToArray();

            Constructors = baseType.GetMembers()
                .Where((m) => m.DeclaredAccessibility == Accessibility.Public && m is IMethodSymbol)
                .Select((m) => (IMethodSymbol)m)
                .Where((m) => m.MethodKind == MethodKind.Constructor)
                .Select((m) => new Constructor(ClassName, m))
                .ToArray();

            Scopes = GetScopes(baseType)
                .Reverse()
                .ToArray();

            FullClassName = string.Join(".", Scopes.Select((s) => s.Name).Append(ClassName));
        }

        public static ObservableObjectSourceGenerator? Inspect(GeneratorSyntaxContext context)
        {
            // Attempt to prequalify as much as we can before we access the SemanticModel, which
            // is expensive...

            // Look for a class declaration...

            if (!context.Node.IsKind(SyntaxKind.ClassDeclaration))
                return null;

            // Make sure it looks like our attribute is associated with this class...

            var decl = (ClassDeclarationSyntax)context.Node;

            bool attributeFound = decl.AttributeLists.FirstOrDefault((lst) => lst.Attributes.FirstOrDefault((a) => a.Name.ToString().Contains("GenerateConcreteObservableObject")) != null) != null;

            if (!attributeFound)
                return null;

            // Ok, we are pretty sure this is a match, let's dig into the SemanticModel...

            var type = context.SemanticModel.GetDeclaredSymbol(context.Node) as ITypeSymbol;

            if (type == null)
                return null;

            Debug.WriteLine($"Inspecting {type}...");

            using var _ = new DebugIndent();

            // Make sure our attribute has been attached to this type...

            var attribute = type.GetAttributes().FirstOrDefault((attribute) => attribute.AttributeClass?.ToDisplayString() == "Concise.GenerateConcreteObservableObjectAttribute");

            if (attribute == null)
            {            
                Debug.WriteLine("*** GenerateConcreteObservableObject attribute not found");
                return null;
            }

            // todo: Create ObservableObjectSourceGenerator here and let
            // it do the remaining processing. Any errors will be saved
            // and output during the execution phase.
       
            //if (type.IsAbstract)
            //{
            //    Debug.WriteLine("*** is abstract");
            //    return null;
            //}

            if (type.IsSealed)
            {
                Debug.WriteLine("*** is sealed");
                return null;
            }
           
            var baseType = type.BaseType;

            if (baseType == null)
            {
                Debug.WriteLine("*** no base type");
                return null;
            }

            // Immediate base class must be abstract...

            if (!baseType.IsAbstract)
            {
                Debug.WriteLine("*** Base class is not abstract");
                return null;
            }

            // Type needs to have ObservableObject as a base class...

            if (!HasBaseType(baseType, "Concise.Observables.ObservableObject"))
            {
                Debug.WriteLine("*** Concise.Observables.ObservableObject is not a base class");
                return null;
            }

            // Make sure it's declared partial...

            if (!decl.Modifiers.Any((m) => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                Debug.WriteLine("*** class must be declared partial");
                return null;
            }

            // We got one!!

            Debug.WriteLine(">> Adding Source Generator");

            return new ObservableObjectSourceGenerator(decl, type);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            Debug.WriteLine($"Building {FullClassName}...");
            try
            {
                var i = new Indention();
                var txt = new StringBuilder();
                var firstSection = true;

                void Append(string s) => txt!.AppendLine($"{i}{s}");
                void OpenCurly() => txt!.AppendLine($"{i++}{{");
                void CloseCurly() => txt!.AppendLine($"{--i}}}");
                void NewSection()
                {
                    if (firstSection)
                    {
                        firstSection = false;
                        return;
                    }

                    Append("");
                }

                foreach (var scope in Scopes)
                {
                    Append($"{scope.Text}");
                    OpenCurly();
                }

                Append($"partial class {ClassName}");
                OpenCurly();

                if (Properties.Any())
                {
                    NewSection();
                    Append($"protected override void AddObservableValues()");
                    OpenCurly();
                    Append($"base.AddObservableValues();");

                    foreach (var property in Properties)
                        Append(property.AddVariableLine());

                    CloseCurly();

                    NewSection();
                    foreach (var property in Properties)
                        Append(property.PropertyLine());
                }

                if (Constructors.Any())
                {
                    NewSection();
                    foreach (var constructor in Constructors)
                        Append(constructor.Text);
                }

                CloseCurly();

                foreach (var _ in Scopes)
                    CloseCurly();

                context.AddSource($"{FullClassName}-Generated.cs", txt.ToString());
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception Executing - {ex.GetType().Name} - {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }
    }


    [Generator]
    public class ObservableObjectGenerator : ISourceGenerator
    {
        private List<ObservableObjectSourceGenerator> _generators = new();
        private TextWriterTraceListener _traceListener = new(new StringWriter());

        public void AddSourceGenerator(ObservableObjectSourceGenerator sourceGenerator) =>
            _generators.Add(sourceGenerator);

        public ObservableObjectGenerator()
        {
            Trace.Listeners.Add(_traceListener);
            
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            Debug.WriteLine($"{DateTimeOffset.Now} Initializing...");

            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver(this));

            Debug.WriteLine($"{DateTimeOffset.Now} Initialization Complete");
        }

        public void Execute(GeneratorExecutionContext context)
        {
            Debug.WriteLine($"{DateTimeOffset.Now} Executing...");

            foreach (var sourceGenerator in _generators)
                sourceGenerator.Execute(context);

            // Add our debug log output...

            Debug.WriteLine($"{DateTimeOffset.Now} Execution Complete");

            _traceListener.Flush();
            var log = ((StringWriter)_traceListener.Writer).ToString();

            context.AddSource("Concise.Generators.ObservableObjectGenerator.log", $"/*\n\n{log}\n\n*/\n");
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            private ObservableObjectGenerator _generator;

            public SyntaxReceiver(ObservableObjectGenerator generator) =>
                (_generator) = (generator);
                 
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                var sourceGenerator = ObservableObjectSourceGenerator.Inspect(context);

                if (sourceGenerator != null)
                    _generator.AddSourceGenerator(sourceGenerator);
                    
            }
        }

    }
}
