using System;
using System.Data.SQLite;
using System.Collections.Generic;
using GameApp.Models.AIChat;
using System.Collections.ObjectModel;

namespace GameApp.Services.AIChat
{
    public class ChatDbService : IDisposable
    {
        private readonly SQLiteConnection _connection;
        public SQLiteConnection Connection => _connection;

        // 构造函数：初始化数据库连接
        public ChatDbService()
        {
            _connection = new SQLiteConnection(App.ConnectionString);
            _connection.Open();
            InitializeDatabase(); // 建表（如果不存在）
        }

        // 初始化数据库表结构
        private void InitializeDatabase()
        {
            using (var cmd = _connection.CreateCommand())
            {
                ExecuteNonQuery("PRAGMA foreign_keys = ON;");
                cmd.CommandText = @"
                   CREATE TABLE IF NOT EXISTS ChatSessions (
                   Id TEXT PRIMARY KEY,
                   Name TEXT NOT NULL,
                   CreatedAt TEXT NOT NULL,
                   LastUpdated TEXT NOT NULL,
                   IsSpecialElfSession INTEGER NOT NULL DEFAULT 0,
                   IsFavorite INTEGER NOT NULL DEFAULT 0
                   );

                    CREATE TABLE IF NOT EXISTS ChatMessages (
                        Id TEXT PRIMARY KEY,
                        SessionId TEXT NOT NULL,
                        Role TEXT NOT NULL,
                        Content TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        FOREIGN KEY(SessionId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        // 保存会话（含事务）
        public void SaveSession(ChatSession session)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // 1. 保存会话信息
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO ChatSessions 
                            (Id, Name, CreatedAt, LastUpdated, IsSpecialElfSession, IsFavorite) 
                            VALUES (@id, @name, @createdAt, @lastUpdated, @isSpecialElfSession, @isFavorite)";

                        cmd.Parameters.AddWithValue("@id", session.Id);
                        cmd.Parameters.AddWithValue("@name", session.Name);
                        cmd.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("o"));
                        cmd.Parameters.AddWithValue("@lastUpdated", session.LastUpdated.ToString("o"));
                        cmd.Parameters.AddWithValue("@isSpecialElfSession", session.IsSpecialElfSession);
                        cmd.Parameters.AddWithValue("@isFavorite", session.IsFavorite ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. 保存所有消息（批量）
                    foreach (var message in session.Messages)
                    {
                        SaveMessage(session.Id, message);
                    }

                    transaction.Commit(); // 提交事务
                }
                catch
                {
                    transaction.Rollback(); // 出错回滚
                    throw;
                }
            }
        }

        // 保存单条消息
        public void SaveMessage(string sessionId, ChatMessage message)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO ChatMessages 
                    (Id, SessionId, Role, Content, Timestamp) 
                    VALUES (@id, @sessionId, @role, @content, @timestamp)";

                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@sessionId", sessionId);
                cmd.Parameters.AddWithValue("@role", message.Role);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        // 加载所有会话（含消息）
        public List<ChatSession> LoadAllSessions()
        {
            var sessions = new List<ChatSession>();

            // 1. 加载会话列表
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM ChatSessions ORDER BY LastUpdated DESC";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sessions.Add(new ChatSession
                        {
                            Id = reader["Id"].ToString(),
                            Name = reader["Name"].ToString(),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                            LastUpdated = DateTime.Parse(reader["LastUpdated"].ToString()),
                            Messages = new ObservableCollection<ChatMessage>(),
                            IsSpecialElfSession = Convert.ToInt32(reader["IsSpecialElfSession"]),
                            IsFavorite = reader["IsFavorite"] != DBNull.Value && Convert.ToInt32(reader["IsFavorite"]) == 1
                        });
                    }
                }
            }

            // 2. 为每个会话加载消息
            foreach (var session in sessions)
            {
                session.Messages = new ObservableCollection<ChatMessage>(LoadMessages(session.Id));
            }

            return sessions;
        }

        // 加载会话的消息
        private List<ChatMessage> LoadMessages(string sessionId)
        {
            var messages = new List<ChatMessage>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM ChatMessages WHERE SessionId = @sessionId ORDER BY Timestamp ASC";
                cmd.Parameters.AddWithValue("@sessionId", sessionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        messages.Add(new ChatMessage(
                            reader["Role"].ToString(),
                            reader["Content"].ToString()
                        ));
                    }
                }
            }
            return messages;
        }

        // 删除会话（自动级联删除消息）
        public void DeleteSession(string sessionId)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ChatSessions WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", sessionId);
                cmd.ExecuteNonQuery();
            }
        }
        // 执行不返回结果的SQL命令（用于UPDATE/DELETE等操作）
        public void ExecuteNonQuery(string sql, object parameters = null)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = sql;

                // 添加参数（如果存在）
                if (parameters != null)
                {
                    foreach (var prop in parameters.GetType().GetProperties())
                    {
                        cmd.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(parameters));
                    }
                }

                cmd.ExecuteNonQuery();
            }
        }

        //返回被收藏的
        public List<ChatSession> LoadFavoriteSessions()
        {
            var sessions = new List<ChatSession>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM ChatSessions WHERE IsFavorite = 1 ORDER BY LastUpdated DESC";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sessions.Add(new ChatSession
                        {
                            Id = reader["Id"].ToString(),
                            Name = reader["Name"].ToString(),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                            LastUpdated = DateTime.Parse(reader["LastUpdated"].ToString()),
                            Messages = new ObservableCollection<ChatMessage>(),
                            IsSpecialElfSession = Convert.ToInt32(reader["IsSpecialElfSession"]),
                            IsFavorite = reader["IsFavorite"] != DBNull.Value && Convert.ToInt32(reader["IsFavorite"]) == 1
                        });
                    }
                }
            }
            foreach (var session in sessions)
            {
                session.Messages = new ObservableCollection<ChatMessage>(LoadMessages(session.Id));
            }
            return sessions;
        }

        // 释放资源
        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}