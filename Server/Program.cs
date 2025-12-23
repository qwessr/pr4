using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using Common;
using Newtonsoft.Json;
using MySqlConnector;

namespace Server
{
    class Program
    {
        private static string connStr = "Server=127.0.0.1;Database=FTPStorage;User=root;Password=;SslMode=None;Charset=utf8;";
        private static Dictionary<int, string> userPaths = new Dictionary<int, string>();

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;

            TcpListener listener = new TcpListener(IPAddress.Any, 8888);
            listener.Start();
            Console.WriteLine(">>> FTP Сервер (Финальная версия) запущен...");

            while (true)
            {
                try
                {
                    using Socket client = listener.AcceptSocket();
                    byte[] buffer = new byte[10485760]; // 10MB
                    int rec = client.Receive(buffer);
                    if (rec == 0) continue;

                    string json = Encoding.UTF8.GetString(buffer, 0, rec);
                    var request = JsonConvert.DeserializeObject<ViewModelSend>(json);
                    if (request == null) continue;

                    if (request.Id != -1) SaveHistory(request.Id, request.Message);

                    Console.WriteLine($"[Команда]: {request.Message} (User ID: {request.Id})");

                    ViewModelMessage response = ProcessRequest(request);

                    byte[] respBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                    client.Send(respBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Ошибка]: {ex.Message}");
                }
            }
        }

        static ViewModelMessage ProcessRequest(ViewModelSend req)
        {
            if (string.IsNullOrWhiteSpace(req.Message)) return new ViewModelMessage("message", "Пустой запрос");

            string[] parts = req.Message.Split(' ');
            string cmd = parts[0].ToLower();

            try
            {
                switch (cmd)
                {
                    case "history": return HandleHistory(req);
                    case "register": return HandleRegister(parts);
                    case "connect": return HandleConnect(parts);
                    case "cd": return HandleCD(req);
                    case "get": return HandleGet(req, parts);
                    default: return HandleFileUpload(req);    
                }
            }
            catch (Exception ex)
            {
                return new ViewModelMessage("message", "Ошибка сервера: " + ex.Message);
            }
        }

        static ViewModelMessage HandleHistory(ViewModelSend req)
        {
            List<string> history = new List<string>();
            using var conn = new MySqlConnection(connStr);
            conn.Open();
            var cmd = new MySqlCommand("SELECT command FROM History WHERE user_id = @id ORDER BY id DESC LIMIT 20", conn);
            cmd.Parameters.AddWithValue("@id", req.Id);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                history.Add(reader.GetString(0));
            }

            return new ViewModelMessage("history", JsonConvert.SerializeObject(history));
        }

        static ViewModelMessage HandleRegister(string[] p)
        {
            if (p.Length < 4) return new ViewModelMessage("message", "Нужно: register логин пароль путь");

            string rawPath = string.Join(" ", p.Skip(3)).Replace("\"", "").Trim();

            using var conn = new MySqlConnection(connStr);
            conn.Open();
            var cmd = new MySqlCommand("INSERT INTO Users (login, password, src) VALUES (@l, @p, @s)", conn);
            cmd.Parameters.AddWithValue("@l", p[1]);
            cmd.Parameters.AddWithValue("@p", p[2]);
            cmd.Parameters.AddWithValue("@s", rawPath);
            cmd.ExecuteNonQuery();

            if (!Directory.Exists(rawPath)) Directory.CreateDirectory(rawPath);

            return new ViewModelMessage("message", "Регистрация успешна. Директория готова.");
        }

        static ViewModelMessage HandleConnect(string[] p)
        {
            if (p.Length < 3) return new ViewModelMessage("message", "Нужно: connect логин пароль");
            using var conn = new MySqlConnection(connStr);
            conn.Open();
            var cmd = new MySqlCommand("SELECT id, src FROM Users WHERE login=@l AND password=@p", conn);
            cmd.Parameters.AddWithValue("@l", p[1]);
            cmd.Parameters.AddWithValue("@p", p[2]);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                int id = r.GetInt32(0);
                userPaths[id] = r.GetString(1);
                return new ViewModelMessage("authorization", id.ToString());
            }
            return new ViewModelMessage("message", "Ошибка авторизации.");
        }


        static void SaveHistory(int userId, string command)
        {
            try
            {
                using var conn = new MySqlConnection(connStr);
                conn.Open();
                var cmd = new MySqlCommand("INSERT INTO History (user_id, command) VALUES (@u, @c)", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                cmd.Parameters.AddWithValue("@c", command);
                cmd.ExecuteNonQuery();
            }
            catch { /* Игнорируем ошибки записи истории */ }
        }

        static ViewModelMessage HandleCD(ViewModelSend req)
        {
            if (!userPaths.ContainsKey(req.Id)) return new ViewModelMessage("message", "Сначала connect");
            string path = userPaths[req.Id];

            if (!Directory.Exists(path)) return new ViewModelMessage("message", "Путь не найден");

            var entries = Directory.GetFileSystemEntries(path)
                                   .Select(x => Directory.Exists(x) ? Path.GetFileName(x) + "/" : Path.GetFileName(x))
                                   .ToList();

            var result = new { currentPath = path, items = entries };
            return new ViewModelMessage("cd", JsonConvert.SerializeObject(result));
        }


        static ViewModelMessage HandleGet(ViewModelSend req, string[] p)
        {
            if (p.Length < 2) return new ViewModelMessage("message", "Укажите имя файла");
            if (!userPaths.ContainsKey(req.Id)) return new ViewModelMessage("message", "Нужна авторизация");

            string fullPath = Path.Combine(userPaths[req.Id], p[1]);
            if (File.Exists(fullPath))
            {
                byte[] data = File.ReadAllBytes(fullPath);
                FileInfoFTP file = new FileInfoFTP(data, p[1]);

                return new ViewModelMessage("file", JsonConvert.SerializeObject(file));
            }
            return new ViewModelMessage("message", "Файл не найден.");
        }

        static ViewModelMessage HandleFileUpload(ViewModelSend req)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<FileInfoFTP>(req.Message);
                if (file != null && userPaths.ContainsKey(req.Id))
                {
                    string savePath = Path.Combine(userPaths[req.Id], file.Name);
                    File.WriteAllBytes(savePath, file.Data);
                    return new ViewModelMessage("message", $"Файл '{file.Name}' загружен на сервер.");
                }
            }
            catch { }
            return new ViewModelMessage("message", "Неизвестная команда.");
        }
    }
}