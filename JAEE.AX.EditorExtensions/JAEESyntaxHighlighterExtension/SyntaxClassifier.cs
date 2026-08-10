using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace JAEE.AX.EditorExtensions
{
    internal sealed class SyntaxClassifier : IClassifier
    {
        private readonly IClassificationTypeRegistryService _registry;

#pragma warning disable 67
        public event System.EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67

        internal SyntaxClassifier(IClassificationTypeRegistryService registry)
        {
            _registry = registry;
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var result = new List<ClassificationSpan>();
            if (span.IsEmpty) return result;

            string text = span.GetText();
            var settings = EditorSettings.getInstance().SyntaxHighlighter;

            foreach (var detected in XppSyntaxDetector.Classify(text))
            {
                if (detected.Category == HighlightCategory.Type         && !settings.TypeEnabled)         continue;
                if (detected.Category == HighlightCategory.Macro        && !settings.MacroEnabled)        continue;
                if (detected.Category == HighlightCategory.Method       && !settings.MethodEnabled)       continue;
                if (detected.Category == HighlightCategory.MethodGlobal && !settings.MethodGlobalEnabled) continue;

                string typeName = CategoryToTypeName(detected.Category);
                var classType = _registry.GetClassificationType(typeName);
                if (classType == null) continue;

                var tokenSpan = new SnapshotSpan(span.Snapshot, span.Start + detected.Start, detected.Length);
                result.Add(new ClassificationSpan(tokenSpan, classType));
            }

            return result;
        }

        private static string CategoryToTypeName(HighlightCategory cat)
        {
            switch (cat)
            {
                case HighlightCategory.Type:         return XppClassificationNames.Type;
                case HighlightCategory.Macro:        return XppClassificationNames.Macro;
                case HighlightCategory.Method:       return XppClassificationNames.Method;
                case HighlightCategory.MethodGlobal: return XppClassificationNames.MethodGlobal;
                default: return string.Empty;
            }
        }
    }
}
