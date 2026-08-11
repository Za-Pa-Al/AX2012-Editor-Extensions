using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace JAEE.AX.EditorExtensions.RefactorRename
{
    internal sealed class RenameDialog : Window
    {
        private readonly TextBox _textBox;
        private readonly TextBlock _errorLabel;

        private RenameDialog(string currentName)
        {
            Title = "Rename";
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(12) };

            panel.Children.Add(new TextBlock
            {
                Text = "New name:",
                Margin = new Thickness(0, 0, 0, 4)
            });

            _textBox = new TextBox { Text = currentName };
            _textBox.KeyDown += OnTextBoxKeyDown;
            panel.Children.Add(_textBox);

            _errorLabel = new TextBlock
            {
                Foreground = Brushes.Red,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };
            panel.Children.Add(_errorLabel);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 72,
                IsDefault = true,
                Margin = new Thickness(0, 0, 6, 0)
            };
            okButton.Click += (_, __) => TryAccept();

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 72,
                IsCancel = true
            };
            cancelButton.Click += (_, __) => DialogResult = false;

            buttonRow.Children.Add(okButton);
            buttonRow.Children.Add(cancelButton);
            panel.Children.Add(buttonRow);

            Content = panel;
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        }

        private void TryAccept()
        {
            string name = _textBox.Text.Trim();
            if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                _errorLabel.Text = "Must be a valid X++ identifier (letters, digits, underscore; cannot start with digit).";
                _errorLabel.Visibility = Visibility.Visible;
                return;
            }
            DialogResult = true;
        }

        /// <summary>
        /// Shows the rename dialog. Returns the new name, or null if cancelled or unchanged.
        /// </summary>
        internal static string Prompt(string currentName, IWpfTextView view)
        {
            var dialog = new RenameDialog(currentName);
            dialog.Owner = Window.GetWindow(view.VisualElement);

            // Select all text so the user can type the new name immediately
            dialog.Loaded += (_, __) =>
            {
                dialog._textBox.Focus();
                dialog._textBox.SelectAll();
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
                return null;

            string newName = dialog._textBox.Text.Trim();
            return (newName == currentName) ? null : newName;
        }
    }
}
