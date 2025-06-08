using System;
using System.Text.RegularExpressions;

namespace GameApp.Services.Voice
{
    /// <summary>
    /// Utility class for cleaning Markdown text to make it suitable for text-to-speech
    /// </summary>
    public static class MarkdownCleaner
    {
        /// <summary>
        /// Convert Markdown text to speech-friendly plain text
        /// </summary>
        /// <param name="markdownText">Input Markdown text</param>
        /// <returns>Clean text suitable for TTS</returns>
        public static string ConvertToSpeechText(string markdownText)
        {
            if (string.IsNullOrWhiteSpace(markdownText))
                return string.Empty;

            string text = markdownText;

            // Remove code blocks (```code```)
            text = Regex.Replace(text, @"```[\s\S]*?```", " [code block] ", RegexOptions.Multiline);
            text = Regex.Replace(text, @"`([^`]+)`", " $1 "); // Inline code

            // Handle headers (# ## ###)
            text = Regex.Replace(text, @"^#{1,6}\s*(.+)$", "$1.", RegexOptions.Multiline);

            // Remove bold and italic markers (**bold** *italic*)
            text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1"); // Bold
            text = Regex.Replace(text, @"\*([^*]+)\*", "$1");     // Italic
            text = Regex.Replace(text, @"__([^_]+)__", "$1");     // Bold underscore
            text = Regex.Replace(text, @"_([^_]+)_", "$1");       // Italic underscore

            // Handle links [text](url)
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");

            // Handle images ![alt](url)
            text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]+\)", "[image: $1]");

            // Handle list items
            text = Regex.Replace(text, @"^\s*[-*+]\s+", "", RegexOptions.Multiline); // Unordered lists
            text = Regex.Replace(text, @"^\s*\d+\.\s+", "", RegexOptions.Multiline); // Ordered lists

            // Handle blockquotes (>)
            text = Regex.Replace(text, @"^\s*>\s*", "", RegexOptions.Multiline);

            // Handle horizontal rules
            text = Regex.Replace(text, @"^[-*_]{3,}$", "[horizontal rule]", RegexOptions.Multiline);

            // Handle tables (simplified)
            text = Regex.Replace(text, @"\|", " ", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^[-\s|:]+$", "", RegexOptions.Multiline);

            // Clean up excess whitespace
            text = Regex.Replace(text, @"\n{3,}", "\n\n");        // Reduce consecutive newlines
            text = Regex.Replace(text, @"\n", " ");               // Convert newlines to spaces
            text = Regex.Replace(text, @"\s{2,}", " ");           // Multiple spaces to single
            text = text.Trim();

            // Handle special cases
            text = text.Replace("*(stopped)*", "stopped");

            return text;
        }

        /// <summary>
        /// Optimize text for speech playback by adding appropriate pauses
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Text optimized for TTS</returns>
        public static string OptimizeForSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Add pauses after sentence endings
            text = Regex.Replace(text, @"([.!?])", "$1 ");

            // Add short pauses after commas
            text = Regex.Replace(text, @"([,;:])", "$1 ");

            // Clean up excess spaces
            text = Regex.Replace(text, @"\s{2,}", " ");

            return text.Trim();
        }

        /// <summary>
        /// Get a preview of text for display purposes (first N characters)
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="maxLength">Maximum length for preview</param>
        /// <returns>Preview text</returns>
        public static string GetPreviewText(string text, int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string cleanText = ConvertToSpeechText(text);

            if (cleanText.Length <= maxLength)
                return cleanText;

            return cleanText.Substring(0, maxLength) + "...";
        }
    }
}
