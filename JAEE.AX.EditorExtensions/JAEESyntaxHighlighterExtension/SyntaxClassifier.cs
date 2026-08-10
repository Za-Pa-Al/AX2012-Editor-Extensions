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

        // Cache the whole-buffer classification per snapshot so scope (parameters/locals)
        // is computed from the entire method, not just the requested span, and only once
        // per edit rather than on every GetClassificationSpans call.
        private ITextSnapshot _cachedSnapshot;
        private List<DetectedSpan> _cachedSpans;

        internal SyntaxClassifier(IClassificationTypeRegistryService registry)
        {
            _registry = registry;
        }

        private List<DetectedSpan> GetBufferSpans(ITextSnapshot snapshot)
        {
            if (_cachedSnapshot == snapshot && _cachedSpans != null) return _cachedSpans;
            _cachedSpans = XppSyntaxDetector.Classify(snapshot.GetText());
            _cachedSnapshot = snapshot;
            return _cachedSpans;
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var result = new List<ClassificationSpan>();
            if (span.IsEmpty) return result;

            var settings = EditorSettings.getInstance().SyntaxHighlighter;
            int reqStart = span.Start.Position;
            int reqEnd   = span.End.Position;

            // Detect against the whole buffer (== one method) so parameter/local scope is known.
            foreach (var detected in GetBufferSpans(span.Snapshot))
            {
                int dStart = detected.Start;
                int dEnd   = detected.Start + detected.Length;
                if (dEnd <= reqStart || dStart >= reqEnd) continue; // outside the requested span

                if (detected.Category == HighlightCategory.Type         && !settings.TypeEnabled)         continue;
                if (detected.Category == HighlightCategory.Macro        && !settings.MacroEnabled)        continue;
                if (detected.Category == HighlightCategory.Method       && !settings.MethodEnabled)       continue;
                if (detected.Category == HighlightCategory.MethodGlobal && !settings.MethodGlobalEnabled) continue;
                if (detected.Category == HighlightCategory.Parameter    && !settings.ParameterEnabled)    continue;
                if (detected.Category == HighlightCategory.GlobalVar    && !settings.GlobalVarEnabled)    continue;

                string typeName = CategoryToTypeName(detected.Category);
                var classType = _registry.GetClassificationType(typeName);
                if (classType == null) continue;

                var tokenSpan = new SnapshotSpan(span.Snapshot, dStart, detected.Length);
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
                case HighlightCategory.Parameter:    return XppClassificationNames.Parameter;
                case HighlightCategory.GlobalVar:    return XppClassificationNames.GlobalVar;
                default: return string.Empty;
            }
        }
    }
}
