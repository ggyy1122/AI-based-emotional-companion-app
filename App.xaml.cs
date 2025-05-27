using System.Configuration;
using System.Data;
using System.IO;
using System.Reflection;
using System.Windows;

namespace GameApp
{
    public partial class App : Application
    {
        /// <summary>
        /// 全局数据库连接字符串
        /// </summary>
        public static readonly string ProjectRoot =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..");

        public static readonly string ConnectionString =
            $"Data Source={Path.Combine(ProjectRoot, "game.db")}";

        public static readonly string VoiceDirectoryName = "Voices";
        public static readonly string VoiceStoragePath = Path.Combine(ProjectRoot, VoiceDirectoryName);

        // 添加应用程序数据目录属性
        public static string DataDirectory
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "GameApp", "Data");
                Directory.CreateDirectory(dir); // 确保目录存在
                return dir;
            }
        }

    }
}