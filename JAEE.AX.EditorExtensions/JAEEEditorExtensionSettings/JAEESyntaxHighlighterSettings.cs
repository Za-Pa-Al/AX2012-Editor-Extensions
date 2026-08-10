using System;
using System.Drawing;

namespace JAEE.AX.EditorExtensions
{
    public enum HighlightCategory { Type, Macro, Method, MethodGlobal, Parameter, GlobalVar }

    /// <summary>
    /// Colors for the semantic syntax highlighter (types, macros, method calls).
    /// Each category can be turned off independently.
    /// "Method" is an instance/static call (x.foo(), Type::foo()); "MethodGlobal"
    /// is a free/global function call (info(), strFmt()).
    /// </summary>
    [Serializable()]
    public class JAEESyntaxHighlighterSettings
    {
        public bool TypeEnabled { get; set; }
        public Color TypeColor { get; set; }

        public bool MacroEnabled { get; set; }
        public Color MacroColor { get; set; }

        public bool MethodEnabled { get; set; }
        public Color MethodColor { get; set; }

        public bool MethodGlobalEnabled { get; set; }
        public Color MethodGlobalColor { get; set; }

        // Font-style categories (no color): parameters -> italic, non-local vars -> bold.
        public bool ParameterEnabled { get; set; }
        public bool GlobalVarEnabled { get; set; }

        public JAEESyntaxHighlighterSettings()
        {
            TypeEnabled = true;
            TypeColor = Color.FromArgb(0x1E, 0x82, 0x69);   // green (VS-style user type)

            MacroEnabled = true;
            MacroColor = Color.FromArgb(0xC0, 0x32, 0x2C);  // red

            MethodEnabled = true;
            MethodColor = Color.FromArgb(0x3C, 0x7F, 0xB1); // steel-blue (instance/static)

            MethodGlobalEnabled = true;
            MethodGlobalColor = Color.FromArgb(0x1F, 0x6F, 0xC0); // blue (global functions)

            ParameterEnabled = true;  // italic
            GlobalVarEnabled = true;  // bold
        }
    }
}
