using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace GameApp.Features.HeartMemo
{
    public static class MemoVoiceDbService
    {
        private static string ConnectionString => App.ConnectionString;
        private static string VoiceStoragePath => Path.Combine(App.DataDirectory, "Voices");
        private static string VoiceRelativePath => "Voices"; // 相对路径

        // 添加语音并返回完整对象
        public static MemoVoice AddVoice(int memoId, string tempFilePath)
        {
            // 确保项目语音目录存在
            Directory.CreateDirectory(App.VoiceStoragePath);

            // 生成唯一文件名（保持.wav格式）
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var fileName = $"录音_{timestamp}.wav"; // 示例：录音_20230825143045567.wav
            var relativePath = Path.Combine(App.VoiceDirectoryName, fileName); // 存入数据库的路径
            var fullDestPath = Path.Combine(App.ProjectRoot, relativePath);   // 物理存储路径

            try
            {
                // 将临时文件复制到项目目录
                File.Copy(tempFilePath, fullDestPath);

                // 存入数据库（只存相对路径）
                const string sql = @"
            INSERT INTO MemoVoices (MemoId, VoicePath) 
            VALUES (@memoId, @path);
            SELECT last_insert_rowid();";

                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@memoId", memoId);
                        cmd.Parameters.AddWithValue("@path", relativePath.Replace('\\', '/')); // 统一使用正斜杠

                        var voiceId = Convert.ToInt32(cmd.ExecuteScalar());

                        return new MemoVoice
                        {
                            Id = voiceId,
                            MemoId = memoId,
                            VoicePath = relativePath
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                // 失败时清理文件
                if (File.Exists(fullDestPath))
                {
                    try { File.Delete(fullDestPath); } catch { }
                }
                throw new Exception($"保存录音失败: {ex.Message}");
            }
        }
        // 删除语音（保留文件）
        public static bool DeleteVoice(int voiceId)
        {
            const string sql = "DELETE FROM MemoVoices WHERE Id = @id";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", voiceId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // 删除语音（同时删除文件）
        public static bool DeleteVoiceWithFile(int voiceId)
        {
            var voice = GetVoiceById(voiceId);
            if (voice == null) return false;

            // 获取完整路径
            var fullPath = GetVoiceFullPath(voice.VoicePath);

            // 先删除文件
            try { if (File.Exists(fullPath)) File.Delete(fullPath); }
            catch { return false; }

            // 再删除记录
            return DeleteVoice(voiceId);
        }

        // 获取备忘录的所有语音
        public static List<MemoVoice> GetVoicesByMemoId(int memoId)
        {
            var list = new List<MemoVoice>();
            const string sql = "SELECT * FROM MemoVoices WHERE MemoId = @memoId";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@memoId", memoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new MemoVoice
                            {
                                Id = reader.GetInt32(0),
                                MemoId = reader.GetInt32(1),
                                VoicePath = reader.GetString(2) // 相对路径
                            });
                        }
                    }
                }
            }
            return list;
        }

        private static MemoVoice GetVoiceById(int voiceId)
        {
            const string sql = "SELECT * FROM MemoVoices WHERE Id = @id";

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", voiceId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new MemoVoice
                            {
                                Id = reader.GetInt32(0),
                                MemoId = reader.GetInt32(1),
                                VoicePath = reader.GetString(2) // 相对路径
                            };
                        }
                    }
                }
            }
            return null;
        }
        // 新增方法：获取语音完整路径
        public static string GetVoiceFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("语音路径不能为空");

            // 统一处理路径分隔符问题（确保跨平台兼容）
            var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(App.ProjectRoot, normalizedPath);
        }

        // 新增方法：通过ID获取完整路径（组合功能）
        public static string GetFullPathById(int voiceId)
        {
            var voice = GetVoiceById(voiceId);
            return voice != null ? GetVoiceFullPath(voice.VoicePath) : null;
        }

    }
}