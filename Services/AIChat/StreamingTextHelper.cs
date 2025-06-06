using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig.Wpf;

namespace GameApp.Services.AIChat
{
    public class StreamingTextHelper
    {
        private MarkdownViewer _markdownViewer;
        private Border _container;
        private ScrollViewer _scrollViewer;

        public StreamingTextHelper(MarkdownViewer markdownViewer, Border container, ScrollViewer scrollViewer)
        {
            _markdownViewer = markdownViewer;
            _container = container;
            _scrollViewer = scrollViewer;
        }

        /// <summary>
        /// Update streaming markdown content
        /// </summary>
        public void UpdateStreamingMarkdown(string markdownText)
        {
            // Update the markdown viewer on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _markdownViewer.Markdown = markdownText;
                    _scrollViewer.ScrollToEnd();
                }
                catch (Exception)
                {
                    // Fallback to plain text if markdown parsing fails
                    _markdownViewer.Markdown = $"```\n{markdownText}\n```";
                }
            });
        }

        /// <summary>
        /// Create streaming message container with markdown support
        /// </summary>
        public static Border CreateStreamingMarkdownContainer(Panel messagesPanel, ScrollViewer scrollViewer, out MarkdownViewer markdownViewer)
        {
            // Create message container with chat bubble styling
            Border messageBorder = new Border
            {
                Background = new SolidColorBrush(Colors.LightGray),
                Padding = new Thickness(12),
                Margin = new Thickness(10, 5, 80, 5), // Chat bubble margins
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(18, 18, 18, 4), // Chat bubble style
                MaxWidth = 1000 // Limit width for better chat appearance
            };

            // Create markdown viewer for streaming content
            markdownViewer = new MarkdownViewer
            {
                Markdown = "*Thinking...*", // Initial placeholder
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 14,
            };

            // Store reference to markdown viewer for lambda expression
            var markdownViewerRef = markdownViewer;

            // Add context menu for copying
            var contextMenu = new ContextMenu();
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(markdownViewerRef.Markdown ?? "");
                }
                catch (Exception)
                {
                    // Ignore clipboard errors
                }
            };
            contextMenu.Items.Add(copyItem);
            markdownViewer.ContextMenu = contextMenu;

            // Add markdown viewer to container
            messageBorder.Child = markdownViewer;

            // Add to UI on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                messagesPanel.Children.Add(messageBorder);
                scrollViewer.ScrollToEnd();
            });

            return messageBorder;
        }

        /// <summary>
        /// Legacy method for backward compatibility with TextBlock
        /// </summary>
        public void UpdateStreamingText(string text)
        {
            UpdateStreamingMarkdown(text);
        }

        /// <summary>
        /// Legacy method for backward compatibility with TextBlock
        /// </summary>
        public static Border CreateStreamingMessageContainer(ScrollViewer scrollViewer, out TextBlock textBlock)
        {
            // This method is kept for backward compatibility but creates a markdown viewer instead
            MarkdownViewer markdownViewer;
            var border = CreateStreamingMarkdownContainer(
                scrollViewer.Content as Panel,
                scrollViewer,
                out markdownViewer);

            // Create a dummy TextBlock for compatibility (not actually used)
            textBlock = new TextBlock();

            return border;
        }
    }
}
