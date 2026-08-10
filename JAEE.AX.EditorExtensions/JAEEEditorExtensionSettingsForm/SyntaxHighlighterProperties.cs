using System.ComponentModel;
using System.Drawing;

namespace JAEE.AX.EditorExtensions
{
    internal class SyntaxHighlighterProperties
    {
        private bool   typeEnabled;
        private Color  typeColor;
        private bool   macroEnabled;
        private Color  macroColor;
        private bool   methodEnabled;
        private Color  methodColor;

        [DisplayName("Type - Enabled"), Category("Types"), DefaultValue(true),
         Description("Color user-defined and system types (e.g. SalesTable, System.IO.Stream).")]
        public bool TypeEnabled   { get { return typeEnabled; }   set { typeEnabled = value; } }

        [DisplayName("Type - Color"), Category("Types"),
         Description("Foreground color for X++ types.")]
        public Color TypeColor    { get { return typeColor; }     set { typeColor = value; } }

        [DisplayName("Macro - Enabled"), Category("Macros"), DefaultValue(true),
         Description("Color macro directives (#define) and inline references (#MyMacro).")]
        public bool MacroEnabled  { get { return macroEnabled; }  set { macroEnabled = value; } }

        [DisplayName("Macro - Color"), Category("Macros"),
         Description("Foreground color for X++ macros.")]
        public Color MacroColor   { get { return macroColor; }    set { macroColor = value; } }

        [DisplayName("Method - Enabled"), Category("Methods"), DefaultValue(true),
         Description("Color method call identifiers (e.g. info, MakeTransparent).")]
        public bool MethodEnabled { get { return methodEnabled; } set { methodEnabled = value; } }

        [DisplayName("Method - Color"), Category("Methods"),
         Description("Foreground color for X++ method calls.")]
        public Color MethodColor  { get { return methodColor; }   set { methodColor = value; } }

        private SyntaxHighlighterProperties() { }

        public SyntaxHighlighterProperties(JAEESyntaxHighlighterSettings settings)
        {
            typeEnabled   = settings.TypeEnabled;
            typeColor     = settings.TypeColor;
            macroEnabled  = settings.MacroEnabled;
            macroColor    = settings.MacroColor;
            methodEnabled = settings.MethodEnabled;
            methodColor   = settings.MethodColor;
        }
    }
}
