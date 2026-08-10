using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace JAEE.AX.EditorExtensions
{
    internal sealed class SyntaxTagger : ITagger<TextMarkerTag>
    {
#pragma warning disable 67
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var settings = EditorSettings.getInstance().SyntaxHighlighter;

            foreach (var span in spans)
            {
                string text = span.GetText();
                int spanStart = span.Start.Position;

                foreach (var detected in XppSyntaxDetector.Classify(text))
                {
                    if (detected.Category == HighlightCategory.Type   && !settings.TypeEnabled)   continue;
                    if (detected.Category == HighlightCategory.Macro  && !settings.MacroEnabled)  continue;
                    if (detected.Category == HighlightCategory.Method && !settings.MethodEnabled) continue;

                    string formatName = CategoryToFormatName(detected.Category);
                    if (string.IsNullOrEmpty(formatName)) continue;

                    var tokenSpan = new SnapshotSpan(span.Snapshot, spanStart + detected.Start, detected.Length);
                    yield return new TagSpan<TextMarkerTag>(tokenSpan, new TextMarkerTag(formatName));
                }
            }
        }

        private static string CategoryToFormatName(HighlightCategory cat)
        {
            switch (cat)
            {
                case HighlightCategory.Type:   return MarkerFormatNames.XppType;
                case HighlightCategory.Macro:  return MarkerFormatNames.XppMacro;
                case HighlightCategory.Method: return MarkerFormatNames.XppMethod;
                default: return string.Empty;
            }
        }
    }
}
