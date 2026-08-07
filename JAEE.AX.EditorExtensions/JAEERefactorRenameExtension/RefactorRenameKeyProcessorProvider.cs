using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace JAEE.AX.EditorExtensions.RefactorRename
{
    [Export(typeof(IKeyProcessorProvider))]
    [Name("JAEERefactorRenameKeyProcessor")]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Order(Before = "DefaultKeyProcessor")]
    internal sealed class RefactorRenameKeyProcessorProvider : IKeyProcessorProvider
    {
        [Import]
        internal ITextSearchService TextSearchService { get; set; }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return new RefactorRenameKeyProcessor(wpfTextView, TextSearchService);
        }
    }
}
