using System.Windows;
using System.Windows.Controls;

namespace GameApp.Pages
{
    /// <summary>
    /// Simple dialog helper for input operations
    /// </summary>
    public static class SimpleDialog
    {
        /// <summary>
        /// Show a simple input dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="prompt">Input prompt text</param>
        /// <param name="defaultValue">Default input value</param>
        /// <returns>User input or null if cancelled</returns>
        public static string ShowInput(string title, string prompt, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Prompt label
            var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(label, 0);

            // Input textbox
            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(5)
            };
            Grid.SetRow(textBox, 1);

            // Buttons panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            string result = null;
            okButton.Click += (s, e) => { result = textBox.Text; dialog.Close(); };
            cancelButton.Click += (s, e) => dialog.Close();
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    result = textBox.Text;
                    dialog.Close();
                }
            };

            textBox.Focus();
            textBox.SelectAll();
            dialog.ShowDialog();

            return result;
        }
    }
}
