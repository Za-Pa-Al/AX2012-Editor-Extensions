using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace JAEE.AX.EditorExtensions.RefactorRename
{
    internal sealed class RefactorRenameKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;
        private readonly ITextSearchService _search;

        internal RefactorRenameKeyProcessor(
            IWpfTextView view,
            ITextSearchService search)
        {
            _view = view;
            _search = search;
        }

        public override void KeyDown(KeyEventArgs args)
        {
            if (args.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Mark handled immediately so AX never sees the key.
                // Defer the actual work so the key event fully completes before the
                // modal dialog opens — opening ShowDialog() mid-event blocks the
                // WPF input pipeline and prevents subsequent key events from firing.
                args.Handled = true;
                _view.VisualElement.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(TryRename));
            }
        }

        private void TryRename()
        {
            try
            {
                string oldName = GetWordUnderCaret();
                if (oldName == null)
                    return;

                string newName = RenameDialog.Prompt(oldName, _view);

                // Restore focus to the editor after the dialog closes.
                _view.VisualElement.Focus();

                if (newName == null)
                    return;

                ApplyRename(oldName, newName);
            }
            catch (Exception)
            {
                // An editor extension must never crash the AX client.
            }
        }

        // Resolve the identifier covering or immediately adjacent to the caret by
        // scanning the buffer directly. Deterministic — avoids the text navigator
        // returning ';'/'(' at a word's trailing edge or splitting camelCase.
        private string GetWordUnderCaret()
        {
            SnapshotPoint caret = _view.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;
            int pos = caret.Position;
            int length = snapshot.Length;

            bool atIdent = pos < length && IsIdentChar(snapshot[pos]);
            bool afterIdent = pos > 0 && IsIdentChar(snapshot[pos - 1]);
            if (!atIdent && !afterIdent)
                return null;

            int start = pos;
            int end = pos;
            while (start > 0 && IsIdentChar(snapshot[start - 1]))
                start--;
            while (end < length && IsIdentChar(snapshot[end]))
                end++;

            string word = snapshot.GetText(start, end - start);
            return IsValidXppIdentifier(word) ? word : null;
        }

        private static bool IsIdentChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool IsValidXppIdentifier(string word)
        {
            return word.Length > 0 && Regex.IsMatch(word, @"^[A-Za-z_][A-Za-z0-9_]*$");
        }

        private void ApplyRename(string oldName, string newName)
        {
            ITextSnapshot snapshot = _view.TextBuffer.CurrentSnapshot;
            FindData findData = new FindData(oldName, snapshot)
            {
                FindOptions = FindOptions.WholeWord | FindOptions.MatchCase
            };

            Collection<SnapshotSpan> matches = _search.FindAll(findData);
            if (matches == null || matches.Count == 0)
                return;

            using (ITextEdit edit = _view.TextBuffer.CreateEdit())
            {
                foreach (SnapshotSpan match in matches)
                    edit.Replace(match.Span, newName);
                edit.Apply();
            }
        }
    }
}
