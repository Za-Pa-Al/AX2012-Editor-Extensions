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
        private bool   methodGlobalEnabled;
        private Color  methodGlobalColor;

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
         Description("Color instance/static method calls (x.foo(), Type::foo()).")]
        public bool MethodEnabled { get { return methodEnabled; } set { methodEnabled = value; } }

        [DisplayName("Method - Color"), Category("Methods"),
         Description("Foreground color for instance/static method calls.")]
        public Color MethodColor  { get { return methodColor; }   set { methodColor = value; } }

        [DisplayName("Global function - Enabled"), Category("Methods"), DefaultValue(true),
         Description("Color global/free function calls (info(), strFmt(), error()).")]
        public bool MethodGlobalEnabled { get { return methodGlobalEnabled; } set { methodGlobalEnabled = value; } }

        [DisplayName("Global function - Color"), Category("Methods"),
         Description("Foreground color for global/free function calls.")]
        public Color MethodGlobalColor  { get { return methodGlobalColor; }   set { methodGlobalColor = value; } }

        private SyntaxHighlighterProperties() { }

        public SyntaxHighlighterProperties(JAEESyntaxHighlighterSettings settings)
        {
            typeEnabled         = settings.TypeEnabled;
            typeColor           = settings.TypeColor;
            macroEnabled        = settings.MacroEnabled;
            macroColor          = settings.MacroColor;
            methodEnabled       = settings.MethodEnabled;
            methodColor         = settings.MethodColor;
            methodGlobalEnabled = settings.MethodGlobalEnabled;
            methodGlobalColor   = settings.MethodGlobalColor;
        }
    }
}
