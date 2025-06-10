using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace GameApp.Models.AIChat
{
    /// <summary>
    /// Represents a chat session containing multiple messages
    /// </summary>
    public class ChatSession : INotifyPropertyChanged
    {
        private string _name;
        private DateTime _lastUpdated;

        /// <summary>
        /// Unique identifier for the session
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name of the session
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
                LastUpdated = DateTime.Now;
            }
        }

        /// <summary>
        /// When the session was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time the session was updated
        /// </summary>
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Whether this session is a special elf session (1) or a regular one (0)
        /// </summary>
        public int IsSpecialElfSession { get; set; } = 0;
        /// <summary>
        /// Collection of messages in this session
        /// </summary>
        public ObservableCollection<ChatMessage> Messages { get; set; }

        public ChatSession()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            LastUpdated = DateTime.Now;
            Messages = new ObservableCollection<ChatMessage>();
        }

        public ChatSession(string name) : this()
        {
            Name = name ?? GenerateDefaultName();
        }

        /// <summary>
        /// Add a user message to the session
        /// </summary>
        public void AddUserMessage(string content)
        {
            var message = new ChatMessage(ChatRole.User, content);
            Messages.Add(message);
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Add an assistant message to the session
        /// </summary>
        public void AddAssistantMessage(string content)
        {
            var message = new ChatMessage(ChatRole.Assistant, content);
            Messages.Add(message);
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Add a system message to the session
        /// </summary>
        public void AddSystemMessage(string content)
        {
            var message = new ChatMessage(ChatRole.System, content);
            Messages.Add(message);
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Get messages formatted for OpenAI API
        /// </summary>
        public List<ChatMessage> GetMessagesForAPI()
        {
            return Messages.ToList();
        }

        /// <summary>
        /// Get preview text for session list display
        /// </summary>
        public string PreviewText
        {
            get
            {
                var lastMessage = Messages.LastOrDefault(m => m.Role != ChatRole.System);
                if (lastMessage != null)
                {
                    var preview = lastMessage.Content.Length > 35
                        ? lastMessage.Content.Substring(0, 35) + "..."
                        : lastMessage.Content;
                    return preview;
                }
                return "New conversation";
            }
        }

        /// <summary>
        /// Generate default session name
        /// </summary>
        private string GenerateDefaultName()
        {
            return $"New Chat {CreatedAt:HH:mm}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Add this method to update preview when messages change
        public void NotifyUIUpdate()
        {
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(LastUpdated));
        }
    }
}
