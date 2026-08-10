using System;
using System.Drawing;

namespace JAEE.AX.EditorExtensions
{
    public enum HighlightCategory { Type, Macro, Method }

    /// <summary>
    /// Colors for the semantic syntax highlighter (types, macros, method calls).
    /// Each category can be turned off independently.
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

        public JAEESyntaxHighlighterSettings()
        {
            TypeEnabled = true;
            TypeColor = Color.FromArgb(0x1F, 0x6F, 0xC0);   // blue

            MacroEnabled = true;
            MacroColor = Color.FromArgb(0xC0, 0x32, 0x2C);  // red

            MethodEnabled = true;
            MethodColor = Color.FromArgb(0x3C, 0x7F, 0xB1); // steel-blue
        }
    }
}
