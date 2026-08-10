using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace JAEE.AX.EditorExtensions
{
    internal static class MarkerFormatNames
    {
        internal const string XppType   = "MarkerFormatDefinition/XppType";
        internal const string XppMacro  = "MarkerFormatDefinition/XppMacro";
        internal const string XppMethod = "MarkerFormatDefinition/XppMethod";
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(MarkerFormatNames.XppType)]
    [UserVisible(true)]
    internal sealed class XppTypeFormatDefinition : MarkerFormatDefinition
    {
        internal XppTypeFormatDefinition()
        {
            DisplayName = "X++ Type";
            var c = EditorSettings.getInstance().SyntaxHighlighter.TypeColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
            BackgroundColor = Colors.Transparent; // no marker box — tint text only
            ZOrder = 5;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(MarkerFormatNames.XppMacro)]
    [UserVisible(true)]
    internal sealed class XppMacroFormatDefinition : MarkerFormatDefinition
    {
        internal XppMacroFormatDefinition()
        {
            DisplayName = "X++ Macro";
            var c = EditorSettings.getInstance().SyntaxHighlighter.MacroColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
            BackgroundColor = Colors.Transparent; // no marker box — tint text only
            ZOrder = 5;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(MarkerFormatNames.XppMethod)]
    [UserVisible(true)]
    internal sealed class XppMethodFormatDefinition : MarkerFormatDefinition
    {
        internal XppMethodFormatDefinition()
        {
            DisplayName = "X++ Method";
            var c = EditorSettings.getInstance().SyntaxHighlighter.MethodColor;
            ForegroundColor = Color.FromArgb(c.A, c.R, c.G, c.B);
            BackgroundColor = Colors.Transparent; // no marker box — tint text only
            ZOrder = 5;
        }
    }
}
