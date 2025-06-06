using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using GameApp.Models.AIChat;

namespace GameApp.Services.AIChat
{
    /// <summary>
    /// Manages chat sessions including creation, deletion, switching, and organization
    /// Provides centralized session management for the AI chat application
    /// </summary>
    public class SessionManager
    {
        #region Singleton Pattern

        private static SessionManager _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance of SessionManager
        /// </summary>
        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new SessionManager();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Collection of all chat sessions (observable for UI binding)
        /// </summary>
        public ObservableCollection<ChatSession> Sessions { get; private set; }

        /// <summary>
        /// Currently active/selected session
        /// </summary>
        public ChatSession CurrentSession { get; private set; }

        /// <summary>
        /// Total number of sessions
        /// </summary>
        public int SessionCount => Sessions.Count;

        /// <summary>
        /// Check if there are any sessions
        /// </summary>
        public bool HasSessions => Sessions.Count > 0;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when the current session changes
        /// Useful for UI updates and notification
        /// </summary>
        public event Action<ChatSession> SessionChanged;

        /// <summary>
        /// Event fired when a new session is created
        /// </summary>
        public event Action<ChatSession> SessionCreated;

        /// <summary>
        /// Event fired when a session is deleted
        /// </summary>
        public event Action<ChatSession> SessionDeleted;

        /// <summary>
        /// Event fired when a session is renamed
        /// </summary>
        public event Action<ChatSession, string> SessionRenamed; // session, oldName

        #endregion

        #region Constructor

        private SessionManager()
        {
            Sessions = new ObservableCollection<ChatSession>();
            InitializeWithDefaultSession();
        }

        #endregion

        #region Core Session Management

        /// <summary>
        /// Create a new chat session
        /// </summary>
        /// <param name="name">Optional session name. If null, generates default name</param>
        /// <returns>The newly created session</returns>
        public ChatSession CreateNewSession(string name = null)
        {
            var session = new ChatSession(name);

            // Add to the beginning of the collection (most recent first)
            Sessions.Insert(0, session);

            // Automatically switch to the new session
            SwitchToSession(session);

            // Notify listeners
            SessionCreated?.Invoke(session);

            return session;
        }

        /// <summary>
        /// Switch to a different session
        /// </summary>
        /// <param name="session">Session to switch to</param>
        /// <returns>True if switch was successful</returns>
        public bool SwitchToSession(ChatSession session)
        {
            if (session == null || !Sessions.Contains(session))
                return false;

            // Update last updated time for previous session
            if (CurrentSession != null)
            {
                CurrentSession.LastUpdated = DateTime.Now;
            }

            // Set new current session
            var previousSession = CurrentSession;
            CurrentSession = session;
            session.LastUpdated = DateTime.Now;

            // Notify listeners about the change
            SessionChanged?.Invoke(session);

            return true;
        }

        /// <summary>
        /// Delete a session
        /// </summary>
        /// <param name="session">Session to delete</param>
        /// <returns>True if deletion was successful</returns>
        public bool DeleteSession(ChatSession session)
        {
            if (session == null || !Sessions.Contains(session))
                return false;

            // Prevent deleting the last session - always keep at least one
            if (Sessions.Count <= 1)
            {
                // Clear the session instead of deleting it
                ClearSession(session);
                return false;
            }

            // Store reference for event notification
            var deletedSession = session;

            // Remove from collection
            Sessions.Remove(session);

            // If we're deleting the current session, switch to another one
            if (CurrentSession == session)
            {
                var newCurrentSession = Sessions.FirstOrDefault();
                if (newCurrentSession != null)
                {
                    SwitchToSession(newCurrentSession);
                }
                else
                {
                    CurrentSession = null;
                }
            }

            // Notify listeners
            SessionDeleted?.Invoke(deletedSession);

            return true;
        }

        /// <summary>
        /// Rename a session
        /// </summary>
        /// <param name="session">Session to rename</param>
        /// <param name="newName">New name for the session</param>
        /// <returns>True if rename was successful</returns>
        public bool RenameSession(ChatSession session, string newName)
        {
            if (session == null || string.IsNullOrWhiteSpace(newName))
                return false;

            var oldName = session.Name;
            session.Name = newName.Trim();

            // Notify listeners
            SessionRenamed?.Invoke(session, oldName);

            return true;
        }

        /// <summary>
        /// Clear all messages from a session
        /// </summary>
        /// <param name="session">Session to clear</param>
        public void ClearSession(ChatSession session)
        {
            if (session != null)
            {
                session.Messages.Clear();
                session.Name = "New Chat";
                session.LastUpdated = DateTime.Now;
            }
        }

        /// <summary>
        /// Duplicate an existing session
        /// </summary>
        /// <param name="session">Session to duplicate</param>
        /// <returns>The duplicated session</returns>
        public ChatSession DuplicateSession(ChatSession session)
        {
            if (session == null)
                return null;

            var duplicatedSession = new ChatSession($"{session.Name} (Copy)");

            // Copy all messages
            foreach (var message in session.Messages)
            {
                duplicatedSession.Messages.Add(new ChatMessage(message.Role, message.Content));
            }

            // Add to sessions and switch to it
            Sessions.Insert(0, duplicatedSession);
            SwitchToSession(duplicatedSession);

            SessionCreated?.Invoke(duplicatedSession);

            return duplicatedSession;
        }

        #endregion

        #region Message Management for Current Session

        /// <summary>
        /// Add a message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        /// <param name="role">Message role (user, assistant, system)</param>
        public void AddMessageToCurrentSession(string content, string role)
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(content))
                return;

            var message = new ChatMessage(role, content);
            CurrentSession.Messages.Add(message);
            CurrentSession.LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Add a user message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        public void AddUserMessage(string content)
        {
            AddMessageToCurrentSession(content, ChatRole.User);
        }

        /// <summary>
        /// Add an assistant message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        public void AddAssistantMessage(string content)
        {
            AddMessageToCurrentSession(content, ChatRole.Assistant);
        }

        /// <summary>
        /// Add a system message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        public void AddSystemMessage(string content)
        {
            AddMessageToCurrentSession(content, ChatRole.System);
        }

        /// <summary>
        /// Clear all messages in the current session
        /// </summary>
        public void ClearCurrentSession()
        {
            if (CurrentSession != null)
            {
                ClearSession(CurrentSession);
            }
        }

        #endregion

        #region Query and Search Methods

        /// <summary>
        /// Get a session by its ID
        /// </summary>
        /// <param name="sessionId">Session ID to search for</param>
        /// <returns>The session if found, null otherwise</returns>
        public ChatSession GetSessionById(string sessionId)
        {
            return Sessions.FirstOrDefault(s => s.Id == sessionId);
        }

        /// <summary>
        /// Get recent sessions (sorted by last updated)
        /// </summary>
        /// <param name="count">Number of recent sessions to get</param>
        /// <returns>List of recent sessions</returns>
        public List<ChatSession> GetRecentSessions(int count = 10)
        {
            return Sessions
                .OrderByDescending(s => s.LastUpdated)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Search sessions by name or message content
        /// </summary>
        /// <param name="searchTerm">Term to search for</param>
        /// <returns>List of matching sessions</returns>
        public List<ChatSession> SearchSessions(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Sessions.ToList();

            var term = searchTerm.ToLower();
            return Sessions
                .Where(s => s.Name.ToLower().Contains(term) ||
                           s.Messages.Any(m => m.Content.ToLower().Contains(term)))
                .ToList();
        }

        /// <summary>
        /// Get sessions with messages from a specific date range
        /// </summary>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <returns>List of sessions within date range</returns>
        public List<ChatSession> GetSessionsByDateRange(DateTime startDate, DateTime endDate)
        {
            return Sessions
                .Where(s => s.LastUpdated >= startDate && s.LastUpdated <= endDate)
                .OrderByDescending(s => s.LastUpdated)
                .ToList();
        }

        #endregion

        #region Persistence Placeholder Methods

        /// <summary>
        /// Save all sessions to persistent storage
        /// TODO: Implement actual persistence logic
        /// </summary>
        public void SaveSessions()
        {
            // TODO: Implement file/database persistence
            // Placeholder for future implementation
        }

        /// <summary>
        /// Load sessions from persistent storage
        /// TODO: Implement actual loading logic
        /// </summary>
        public void LoadSessions()
        {
            // TODO: Implement file/database loading
            // Placeholder for future implementation
        }

        /// <summary>
        /// Auto-save sessions (called after modifications)
        /// TODO: Implement auto-save logic
        /// </summary>
        private void TriggerAutoSave()
        {
            // TODO: Implement auto-save mechanism
            // Could use timer-based or immediate saving
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Initialize the session manager with a default session
        /// </summary>
        private void InitializeWithDefaultSession()
        {
            if (Sessions.Count == 0)
            {
                var defaultSession = new ChatSession("Welcome Chat");
                defaultSession.AddAssistantMessage("Hello! I'm your AI assistant. How can I help you today?");

                Sessions.Add(defaultSession);
                CurrentSession = defaultSession;
            }
        }

        /// <summary>
        /// Get statistics about all sessions
        /// </summary>
        /// <returns>Session statistics object</returns>
        public SessionStatistics GetSessionStatistics()
        {
            return new SessionStatistics
            {
                TotalSessions = Sessions.Count,
                TotalMessages = Sessions.Sum(s => s.Messages.Count),
                AverageMessagesPerSession = Sessions.Count > 0 ? Sessions.Average(s => s.Messages.Count) : 0,
                OldestSession = Sessions.OrderBy(s => s.CreatedAt).FirstOrDefault(),
                NewestSession = Sessions.OrderByDescending(s => s.CreatedAt).FirstOrDefault(),
                MostActiveSession = Sessions.OrderByDescending(s => s.Messages.Count).FirstOrDefault()
            };
        }

        #endregion
    }

    /// <summary>
    /// Statistics about session usage
    /// </summary>
    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public double AverageMessagesPerSession { get; set; }
        public ChatSession OldestSession { get; set; }
        public ChatSession NewestSession { get; set; }
        public ChatSession MostActiveSession { get; set; }
    }
}
