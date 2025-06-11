using System.Windows;
using System.Windows.Controls;
using System.Data.SQLite;
using System.Diagnostics;

namespace GameApp.Pages
{
    public partial class SpiritDashboard : Page
    {
        public class DashboardData
        {
            public int ConversationCount { get; set; } = 0;  // 初始对话次数
            public int GamePlayCount { get; set; } = 0;      // 初始游戏次数
            public int HighScore { get; set; } = 0;          // 初始最高分
            public int DiaryCount { get; set; } = 0;         // 初始日记数量
            public string FavoriteTopic { get; set; } = "还没有记录哦~"; // 初始话题
        }

        public SpiritDashboard()
        {
            InitializeComponent();
           LoadDataFromDatabase();
        }
        private void LoadDataFromDatabase()
        {
            var data = new DashboardData();
            string connectionString = App.ConnectionString;

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    // 设置连接超时（10秒）
                    connection.DefaultTimeout = 10;
                    connection.Open();

                    // 使用事务一次性获取所有数据
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. 查询心情笔记数量
                            data.DiaryCount = ExecuteScalarQuery<int>(
                                connection,
                                "SELECT COUNT(*) FROM HeartMemos",
                                0);

                            // 2. 查询对话次数
                            data.ConversationCount = ExecuteScalarQuery<int>(
                                connection,
                                "SELECT COUNT(*) FROM ChatMessages",
                                0);

                            // 3. 查询游戏最高分
                            data.HighScore = ExecuteScalarQuery<int>(
                                connection,
                                "SELECT MAX(Score) FROM GameScores",
                                0);

                            // 4. 查询游戏次数
                            data.GamePlayCount = ExecuteScalarQuery<int>(
                                connection,
                                "SELECT COUNT(*) FROM GameScores",
                                0);

                            // 5. 设置默认话题
                            data.FavoriteTopic = "美食";

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Debug.WriteLine($"加载数据失败: {ex.Message}");

                // 设置默认值
                data = new DashboardData
                {
                    DiaryCount = 0,
                    ConversationCount = 0,
                    HighScore = 0,
                    GamePlayCount = 0,
                    FavoriteTopic = "美食"
                };
            }

            this.DataContext = data;
        }

        // 辅助方法：执行标量查询并处理异常
        private T ExecuteScalarQuery<T>(SQLiteConnection connection, string sql, T defaultValue)
        {
            try
            {
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.CommandTimeout = 5; // 每个查询单独设置超时
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ?
                        defaultValue :
                        (T)Convert.ChangeType(result, typeof(T));
                }
            }
            catch
            {
                return defaultValue;
            }
        }


        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
            else
            {
                MessageBox.Show("没有上一页了！");
            }
        }
    }
}