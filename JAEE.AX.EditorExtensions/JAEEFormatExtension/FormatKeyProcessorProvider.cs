using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace JAEE.AX.EditorExtensions.Format
{
    [Export(typeof(IKeyProcessorProvider))]
    [Name("JAEEFormatKeyProcessor")]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Order(Before = "DefaultKeyProcessor")]
    internal sealed class FormatKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return new FormatKeyProcessor(wpfTextView);
        }
    }
}
