using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace JAEE.AX.EditorExtensions
{
    /// <summary>Unique classification-type names for the X++ semantic highlighter.</summary>
    internal static class XppClassificationNames
    {
        internal const string Type         = "JAEE.Xpp.Type";
        internal const string Macro        = "JAEE.Xpp.Macro";
        internal const string Method       = "JAEE.Xpp.Method";
        internal const string MethodGlobal = "JAEE.Xpp.MethodGlobal";
        internal const string Parameter    = "JAEE.Xpp.Parameter";
        internal const string GlobalVar    = "JAEE.Xpp.GlobalVar";
    }

    /// <summary>
    /// Classification TYPE definitions. Exporting these is what makes the names
    /// resolvable via IClassificationTypeRegistryService.GetClassificationType(name).
    /// Without them the classifier gets null back and emits nothing — the most
    /// likely reason the earlier IClassifier attempt "did nothing" in AX.
    /// </summary>
    internal static class XppClassificationTypeExports
    {
#pragma warning disable 649 // assigned by the MEF composition engine, not by us
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.Type)]
        internal static ClassificationTypeDefinition TypeDef;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.Macro)]
        internal static ClassificationTypeDefinition MacroDef;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.Method)]
        internal static ClassificationTypeDefinition MethodDef;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.MethodGlobal)]
        internal static ClassificationTypeDefinition MethodGlobalDef;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.Parameter)]
        internal static ClassificationTypeDefinition ParameterDef;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(XppClassificationNames.GlobalVar)]
        internal static ClassificationTypeDefinition GlobalVarDef;
#pragma warning restore 649
    }

    // ---- FORMAT definitions: foreground color per classification type (from settings) ----

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.Type)]
    [Name("JAEE.Xpp.Type.Format")]
    [UserVisible(true)]
    internal sealed class XppTypeFormat : ClassificationFormatDefinition
    {
        internal XppTypeFormat()
        {
            DisplayName = "X++ Type";
            var c = EditorSettings.getInstance().SyntaxHighlighter.TypeColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.Macro)]
    [Name("JAEE.Xpp.Macro.Format")]
    [UserVisible(true)]
    internal sealed class XppMacroFormat : ClassificationFormatDefinition
    {
        internal XppMacroFormat()
        {
            DisplayName = "X++ Macro";
            var c = EditorSettings.getInstance().SyntaxHighlighter.MacroColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.Method)]
    [Name("JAEE.Xpp.Method.Format")]
    [UserVisible(true)]
    internal sealed class XppMethodFormat : ClassificationFormatDefinition
    {
        internal XppMethodFormat()
        {
            DisplayName = "X++ Method (instance/static)";
            var c = EditorSettings.getInstance().SyntaxHighlighter.MethodColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.MethodGlobal)]
    [Name("JAEE.Xpp.MethodGlobal.Format")]
    [UserVisible(true)]
    internal sealed class XppMethodGlobalFormat : ClassificationFormatDefinition
    {
        internal XppMethodGlobalFormat()
        {
            DisplayName = "X++ Method (global function)";
            var c = EditorSettings.getInstance().SyntaxHighlighter.MethodGlobalColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    // Font-style only (no foreground) so the text keeps its default color.
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.Parameter)]
    [Name("JAEE.Xpp.Parameter.Format")]
    [UserVisible(true)]
    internal sealed class XppParameterFormat : ClassificationFormatDefinition
    {
        internal XppParameterFormat()
        {
            DisplayName = "X++ Parameter variable";
            IsItalic = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = XppClassificationNames.GlobalVar)]
    [Name("JAEE.Xpp.GlobalVar.Format")]
    [UserVisible(true)]
    internal sealed class XppGlobalVarFormat : ClassificationFormatDefinition
    {
        internal XppGlobalVarFormat()
        {
            DisplayName = "X++ Non-local variable";
            IsBold = true;
        }
    }
}
