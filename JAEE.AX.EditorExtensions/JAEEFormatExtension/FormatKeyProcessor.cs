using System;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace JAEE.AX.EditorExtensions.Format
{
    internal sealed class FormatKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;

        internal FormatKeyProcessor(IWpfTextView view)
        {
            _view = view;
        }

        public override void KeyDown(KeyEventArgs args)
        {
            if (args.Key == Key.F &&
                Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                // Mark handled immediately so AX never sees the key, then defer the work
                // so the key event fully completes before the buffer edit — same pipeline
                // safeguard used by the Rename extension.
                args.Handled = true;
                _view.VisualElement.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(TryFormat));
            }
        }

        private void TryFormat()
        {
            try
            {
                ITextSnapshot snapshot = _view.TextBuffer.CurrentSnapshot;
                int caretOffset = _view.Caret.Position.BufferPosition.Position;

                string original = snapshot.GetText();
                string formatted = XppFormatter.Format(original);

                // No change (already formatted, or the safety guard refused) — do nothing,
                // so no empty undo entry is created.
                if (string.Equals(formatted, original, StringComparison.Ordinal))
                {
                    _view.VisualElement.Focus();
                    return;
                }

                // One atomic edit == a single Ctrl+Z undo.
                using (ITextEdit edit = _view.TextBuffer.CreateEdit())
                {
                    edit.Replace(0, snapshot.Length, formatted);
                    edit.Apply();
                }

                // Restore the caret near its previous offset (whole-buffer replace loses the
                // exact position; clamp into the new snapshot).
                ITextSnapshot newSnapshot = _view.TextBuffer.CurrentSnapshot;
                int target = Math.Min(caretOffset, newSnapshot.Length);
                _view.Caret.MoveTo(new SnapshotPoint(newSnapshot, target));
                _view.Caret.EnsureVisible();
                _view.VisualElement.Focus();
            }
            catch (Exception)
            {
                // An editor extension must never crash the AX client.
            }
        }
    }
}
