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
            //DataBase Operation
            using (var db = new ChatDbService())
            {
                db.SaveSession(session); // 包含会话信息和初始消息
            }
            //Memory Operation
            // Add to the beginning of the collection (most recent first)
            Sessions.Insert(0, session);

            // Automatically switch to the new session
            SwitchToSession(session);

            // Notify listeners
            SessionCreated?.Invoke(session);

            return session;
        }


        /// <summary>
        /// Give the special prompt to the sprite
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public string GetSystemPromptForSession(ChatSession session)
        {
            if (session == null)
                return AIConfigSettings.SystemPrompt;

            // 如果是精灵会话，返回特定的精灵提示词
            if (session.IsSpecialElfSession == 1)
            {
                return @"
你是一个神奇的精灵助手，拥有超凡的智慧和魔法能力。
你的任务是：
1. 用精灵特有的方式回答用户问题
2. 保持可爱而友好的语气
3. 可以适当使用精灵风格的表达方式
4. 必要时可以提供魔法般的建议
5. 要能温暖用户的心灵
记住你是一个精灵，不是普通AI！";
            }

            // 默认系统提示词
            return AIConfigSettings.SystemPrompt;
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
            if (session == null || !Sessions.Contains(session)||session.IsSpecialElfSession==1)
                return false;

            // Prevent deleting the last session - always keep at least one
            if (Sessions.Count <= 1)
            {
                // Clear the session instead of deleting it ,this method has connected to database
                ClearSession(session);
                return false;
            }

            using (var db = new ChatDbService())
            {
                // Begin transaction to ensure atomicity
                using (var transaction = db.Connection.BeginTransaction())
                {
                    try
                    {
                        // 1.Note: This step can be omitted due to ON DELETE CASCADE
                        // db.ExecuteNonQuery("DELETE FROM ChatMessages WHERE SessionId = @id", 
                        //     new { id = session.Id });

                        db.DeleteSession(session.Id);
                        // 2.Delete session record
                      

                        transaction.Commit();

                        // 3.Update in-memory state
                        var deletedSession = session;
                        Sessions.Remove(session);

                        if (CurrentSession == session)
                        {
                            CurrentSession = Sessions.FirstOrDefault();
                        }
                        // 4.Trigger event
                        SessionDeleted?.Invoke(deletedSession);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Failed to delete session: {ex.Message}");
                        return false;
                    }
                }
            }
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
            session.LastUpdated = DateTime.Now; // 更新修改时间

            // 同步更新数据库
            using (var db = new ChatDbService())
            {
                db.ExecuteNonQuery(
                    "UPDATE ChatSessions SET Name = @name, LastUpdated = @lastUpdated WHERE Id = @id",
                    new
                    {
                        name = session.Name,
                        lastUpdated = session.LastUpdated.ToString("o"),
                        id = session.Id
                    });
            }

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
            if (session == null) return;

            using (var db = new ChatDbService())
            {
                // 开启事务确保原子性
                using (var transaction = db.Connection.BeginTransaction())
                {
                    try
                    {
                        // 1. 删除所有关联消息
                        db.ExecuteNonQuery(
                            "DELETE FROM ChatMessages WHERE SessionId = @id",
                            new { id = session.Id });

                        // 2. 更新会话信息
                        db.ExecuteNonQuery(
                            @"UPDATE ChatSessions 
                      SET Name = @name, 
                          LastUpdated = @lastUpdated 
                      WHERE Id = @id",
                            new
                            {
                                name = "New Chat",
                                lastUpdated = DateTime.Now.ToString("o"),
                                id = session.Id
                            });

                        transaction.Commit();

                        // 3. 更新内存状态
                        session.Messages.Clear();
                        session.Name = "New Chat";
                        session.LastUpdated = DateTime.Now;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // 重新抛出异常
                    }
                }
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
            using (var db = new ChatDbService())
            {
                db.SaveMessage(CurrentSession.Id, message);
            }
        }

        /// <summary>
        /// Add a user message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        public void AddUserMessage(string content)
        {
            AddMessageToCurrentSession(content, ChatRole.User);
            CurrentSession?.NotifyUIUpdate(); // Add this line
        }

        /// <summary>
        /// Add an assistant message to the current session
        /// </summary>
        /// <param name="content">Message content</param>
        public void AddAssistantMessage(string content)
        {
            AddMessageToCurrentSession(content, ChatRole.Assistant);
            CurrentSession?.NotifyUIUpdate(); // Add this line
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
                using (var db = new ChatDbService())
                {
                    db.ExecuteNonQuery(
                        "DELETE FROM ChatMessages WHERE SessionId = @id",
                        new { id = CurrentSession.Id });
                }
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
            // 优先从数据库加载
            using (var db = new ChatDbService())
            {
                var savedSessions = db.LoadAllSessions();
                if (savedSessions.Count > 0)
                {
                    foreach (var session in savedSessions)
                    {
                        Sessions.Add(session); // 加载到内存
                    }
                    CurrentSession = Sessions.First(); // 默认选中第一个
                    return;
                }
            }

            // 无数据时创建默认精灵会话（并保存到数据库）
            var spriteSession = new ChatSession("Sprite Chat");
            spriteSession.AddAssistantMessage("Hello! How can I help you?");
            spriteSession.IsSpecialElfSession = 1;
            using (var db = new ChatDbService())
            {
                db.SaveSession(spriteSession); // 持久化默认会话
            }

            Sessions.Add(spriteSession);
            CurrentSession = spriteSession;
           
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
