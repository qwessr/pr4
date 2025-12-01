using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace Server
{
    class Program
    {
        private static IPAddress ipAddress;
        private static int port;
        // Строка подключения для SSMS
        private static string connectionString = @"Server=localhost;Database=FTPStorage;Trusted_Connection=True;";
        
        // Кэш текущих путей пользователей: <UserId, CurrentPath>
        private static Dictionary<int, string> userPaths = new Dictionary<int, string>();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "FTP Server (SQL Server)";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════╗");
            Console.WriteLine("║      FTP SERVER (SSMS)             ║");
            Console.WriteLine("╚════════════════════════════════════╝\n");

            // Настройка сети
            Console.Write("IP адрес (Enter = 127.0.0.1): ");
            string ip = Console.ReadLine();
            ipAddress = string.IsNullOrWhiteSpace(ip) ? IPAddress.Parse("127.0.0.1") : IPAddress.Parse(ip);

            Console.Write("Порт (Enter = 8888): ");
            string portStr = Console.ReadLine();
            port = string.IsNullOrWhiteSpace(portStr) ? 8888 : int.Parse(portStr);

            StartServer();
        }

        static void StartServer()
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);

            serverSocket.Bind(endPoint);
            serverSocket.Listen(10);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Сервер запущен: {ipAddress}:{port}\n");
            Console.ForegroundColor = ConsoleColor.White;

            while (true)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();
                    string clientIP = ((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString();

                    byte[] buffer = new byte[10485760]; // 10 MB буфер
                    int received = clientSocket.Receive(buffer);
                    string data = Encoding.UTF8.GetString(buffer, 0, received);

                    ViewModelSend request = JsonConvert.DeserializeObject<ViewModelSend>(data);
                    
                    // Замер времени выполнения
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    ViewModelMessage response = ProcessRequest(request, clientIP);
                    stopwatch.Stop();

                    // Логирование в БД (кроме команд connect и register, если нужно)
                    if (request.Id != -1) 
                    {
                        LogCommand(request.Id, request.Message, clientIP, stopwatch.ElapsedMilliseconds, 
                            response.TypeMessage == "message" && response.Message.Contains("Ошибка") ? "error" : "success", 
                            response.Message);
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                    clientSocket.Send(responseBytes);
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        static ViewModelMessage ProcessRequest(ViewModelSend request, string clientIP)
        {
            string[] parts = request.Message.Split(' ');
            string command = parts[0].ToLower();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] IP: {clientIP} | Cmd: {command}");

            switch (command)
            {
                case "register": return HandleRegister(parts);
                case "connect": return HandleConnect(parts);
                case "cd": return HandleCD(request);
                case "get": return HandleGet(request);
                case "history": return HandleHistory(request);
                default: return HandleUpload(request); // Если это JSON файла
            }
        }

        // --- ОБРАБОТЧИКИ КОМАНД ---

        static ViewModelMessage HandleRegister(string[] parts)
        {
            if (parts.Length < 4) return new ViewModelMessage("message", "Формат: register логин пароль путь");

            string login = parts[1];
            string password = parts[2];
            string path = string.Join(" ", parts.Skip(3));

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Проверка на существование
                    SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE login = @login", conn);
                    checkCmd.Parameters.AddWithValue("@login", login);
                    if ((int)checkCmd.ExecuteScalar() > 0)
                        return new ViewModelMessage("message", "Логин занят");

                    // Регистрация
                    SqlCommand cmd = new SqlCommand("INSERT INTO Users (login, password, src) VALUES (@login, @password, @src)", conn);
                    cmd.Parameters.AddWithValue("@login", login);
                    cmd.Parameters.AddWithValue("@password", password);
                    cmd.Parameters.AddWithValue("@src", path);
                    cmd.ExecuteNonQuery();

                    // Создаем папку физически
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    return new ViewModelMessage("message", "Регистрация успешна!");
                }
            }
            catch (Exception ex) { return new ViewModelMessage("message", $"Ошибка БД: {ex.Message}"); }
        }

        static ViewModelMessage HandleConnect(string[] parts)
        {
            if (parts.Length < 3) return new ViewModelMessage("message", "Формат: connect логин пароль");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand("SELECT id, src FROM Users WHERE login = @l AND password = @p", conn);
                    cmd.Parameters.AddWithValue("@l", parts[1]);
                    cmd.Parameters.AddWithValue("@p", parts[2]);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string src = reader.GetString(1);
                            
                            // Инициализируем текущий путь пользователя
                            if (!userPaths.ContainsKey(id)) userPaths[id] = src;

                            return new ViewModelMessage("authorization", id.ToString());
                        }
                    }
                }
                return new ViewModelMessage("message", "Неверный логин или пароль");
            }
            catch (Exception ex) { return new ViewModelMessage("message", $"Ошибка: {ex.Message}"); }
        }

        static ViewModelMessage HandleCD(ViewModelSend request)
        {
            if (request.Id == -1) return new ViewModelMessage("message", "Нет авторизации");

            try
            {
                // Если путь еще не в кэше (сервер перезагружался), восстановим из БД
                if (!userPaths.ContainsKey(request.Id)) RestoreUserPath(request.Id);

                string currentPath = userPaths[request.Id];
                string[] parts = request.Message.Split(new[] { ' ' }, 2);

                if (parts.Length > 1)
                {
                    string folder = parts[1].Trim();
                    if (folder == "..")
                    {
                        DirectoryInfo parent = Directory.GetParent(currentPath);
                        if (parent != null) currentPath = parent.FullName;
                    }
                    else
                    {
                        string newPath = Path.Combine(currentPath, folder);
                        if (Directory.Exists(newPath)) currentPath = newPath;
                        else return new ViewModelMessage("message", "Папка не найдена");
                    }
                    userPaths[request.Id] = currentPath;
                }

                // Собираем список файлов
                var items = new List<string>();
                if (Directory.GetParent(currentPath) != null) items.Add("../");
                
                foreach (var d in Directory.GetDirectories(currentPath)) items.Add(Path.GetFileName(d) + "/");
                foreach (var f in Directory.GetFiles(currentPath)) items.Add(Path.GetFileName(f));

                // Формируем JSON объект как в примере
                var resultObj = new { items = items, currentPath = currentPath };
                return new ViewModelMessage("cd", JsonConvert.SerializeObject(resultObj));
            }
            catch (Exception ex) { return new ViewModelMessage("message", $"Ошибка: {ex.Message}"); }
        }

        static ViewModelMessage HandleGet(ViewModelSend request)
        {
            try
            {
                string fileName = request.Message.Substring(4); // "get filename"
                string fullPath = Path.Combine(userPaths[request.Id], fileName);

                if (File.Exists(fullPath))
                {
                    byte[] data = File.ReadAllBytes(fullPath);
                    return new ViewModelMessage("file", JsonConvert.SerializeObject(data));
                }
                return new ViewModelMessage("message", "Файл не найден");
            }
            catch (Exception ex) { return new ViewModelMessage("message", ex.Message); }
        }

        static ViewModelMessage HandleUpload(ViewModelSend request)
        {
            try
            {
                FileInfoFTP file = JsonConvert.DeserializeObject<FileInfoFTP>(request.Message);
                string fullPath = Path.Combine(userPaths[request.Id], file.Name);
                File.WriteAllBytes(fullPath, file.Data);
                return new ViewModelMessage("message", "Файл загружен");
            }
            catch { return new ViewModelMessage("message", "Ошибка загрузки"); }
        }

        static ViewModelMessage HandleHistory(ViewModelSend request)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Берем последние 20 команд
                    string query = "SELECT TOP 20 command, executed_at, status FROM UserCommands WHERE user_id = @uid ORDER BY executed_at DESC";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@uid", request.Id);

                    List<string> history = new List<string>();
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            history.Add($"[{r.GetDateTime(1):HH:mm:ss}] {r.GetString(0)} ({r.GetString(2)})");
                        }
                    }
                    return new ViewModelMessage("history", JsonConvert.SerializeObject(history));
                }
            }
            catch (Exception ex) { return new ViewModelMessage("message", ex.Message); }
        }

        static void LogCommand(int userId, string cmdText, string ip, long ms, string status, string result)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"INSERT INTO UserCommands (user_id, command, ip_address, execution_time_ms, status, result_message) 
                                     VALUES (@u, @c, @ip, @ms, @s, @r)";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@u", userId);
                    cmd.Parameters.AddWithValue("@c", cmdText.Length > 50 ? cmdText.Substring(0, 50) + "..." : cmdText);
                    cmd.Parameters.AddWithValue("@ip", ip);
                    cmd.Parameters.AddWithValue("@ms", ms);
                    cmd.Parameters.AddWithValue("@s", status);
                    cmd.Parameters.AddWithValue("@r", result.Length > 100 ? result.Substring(0, 100) : result);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* Игнорируем ошибки логгера */ }
        }

        static void RestoreUserPath(int id)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT src FROM Users WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                string src = (string)cmd.ExecuteScalar();
                if (src != null) userPaths[id] = src;
            }
        }
    }
}
