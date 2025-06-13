using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Media;

namespace GameApp.Services.HeartMemo
{
    class MemoDbService
    {
        private static string ConnectionString => App.ConnectionString;
        // 保存备忘录（新建或更新）
        public static int SaveMemo(Memo memo)
        {
            if (memo.Id == 0)
            {
                return CreateMemo(memo);
            }
            else
            {
                UpdateMemo(memo);
                return memo.Id;
            }
        }

        private static int CreateMemo(Memo memo)
        {
            const string sql = @"
                INSERT INTO HeartMemos (Date, Title, Content, EmotionColor) 
                VALUES (@date, @title, @content, @color);
                SELECT last_insert_rowid();";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@date", memo.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@title", memo.Title ?? "未命名");
                    cmd.Parameters.AddWithValue("@content", memo.Content ?? "");
                    cmd.Parameters.AddWithValue("@color", memo.ColorString);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public static void UpdateMemo(Memo memo)
        {
            const string sql = @"
                UPDATE HeartMemos 
                SET Date = @date, 
                    Title = @title, 
                    Content = @content, 
                    EmotionColor = @color 
                WHERE Id = @id";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@date", memo.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@title", memo.Title ?? "未命名");
                    cmd.Parameters.AddWithValue("@content", memo.Content ?? "");
                    cmd.Parameters.AddWithValue("@color", memo.ColorString);
                    cmd.Parameters.AddWithValue("@id", memo.Id);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 获取单个备忘录（含语音）
        public static Memo GetMemoWithVoices(int memoId)
        {
            var memo = GetMemoById(memoId);
            if (memo != null)
            {
                memo.LoadVoices();
            }
            return memo;
        }

        // 获取所有备忘录（不含语音）
        public static List<Memo> GetAllMemos()
        {
            var list = new List<Memo>();
            const string sql = "SELECT * FROM HeartMemos ORDER BY Date DESC";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Memo
                        {
                            Id = reader.GetInt32(0),
                            Date = reader.GetDateTime(1),
                            Title = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Content = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            ColorString = reader.IsDBNull(4) ? "#FF0000FF" : reader.GetString(4)
                        });
                    }
                }
            }
            return list;
        }

        // 删除备忘录及其语音
        public static void DeleteMemoWithVoices(int memoId)
        {
            // 先删除语音文件
            var voices = MemoVoiceDbService.GetVoicesByMemoId(memoId);
            foreach (var voice in voices)
            {
                MemoVoiceDbService.DeleteVoiceWithFile(voice.Id);
            }

            // 级联删除数据库记录
            DeleteMemo(memoId);
        }

        private static Memo GetMemoById(int memoId)
        {
            const string sql = "SELECT * FROM HeartMemos WHERE Id = @id";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", memoId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Memo
                            {
                                Id = reader.GetInt32(0),
                                Date = reader.GetDateTime(1),
                                Title = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Content = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                ColorString = reader.IsDBNull(4) ? "#FF0000FF" : reader.GetString(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        private static void DeleteMemo(int memoId)
        {
            const string sql = "DELETE FROM HeartMemos WHERE Id = @id";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", memoId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
