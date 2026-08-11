using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace JAEE.AX.EditorExtensions
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("text")]
    internal sealed class SyntaxClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry = null;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new SyntaxClassifier(ClassificationRegistry));
        }
    }
}
