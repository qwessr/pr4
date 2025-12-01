using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common;
using System.Xml.Linq;

namespace WpfClient
{
    public partial class MainWindow : Window
    {
        private string serverIP = "127.0.0.1";
        private int serverPort = 8888;
        private int userId = -1;

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- СЕТЕВАЯ ЧАСТЬ ---
        private ViewModelMessage SendRequest(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);

                    // Отправка
                    var request = new ViewModelSend(message, userId);
                    byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                    socket.Send(data);

                    // Получение
                    byte[] buffer = new byte[10485760];
                    int received = socket.Receive(buffer);
                    string responseStr = Encoding.UTF8.GetString(buffer, 0, received);

                    return JsonConvert.DeserializeObject<ViewModelMessage>(responseStr);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сети: {ex.Message}");
                return null;
            }
        }

        // --- ЛОГИКА ИНТЕРФЕЙСА ---

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            serverIP = TxtServer.Text;
            serverPort = int.Parse(TxtPort.Text);
            string cmd = $"connect {TxtLogin.Text} {TxtPassword.Password}";

            var response = SendRequest(cmd);
            if (response != null && response.TypeMessage == "authorization")
            {
                userId = int.Parse(response.Message);
                TxtStatus.Text = $"ID: {userId}. Авторизован.";

                // Включаем кнопки
                BtnRefresh.IsEnabled = true;
                BtnUpload.IsEnabled = true;
                BtnHistory.IsEnabled = true;
                PanelAuth.IsEnabled = false; // Блокируем поля ввода

                LoadFiles();
            }
            else
            {
                MessageBox.Show(response?.Message ?? "Ошибка подключения");
            }
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно регистрации
            RegisterWindow rw = new RegisterWindow(TxtServer.Text, int.Parse(TxtPort.Text));
            rw.Owner = this;
            rw.ShowDialog();
        }

        private void LoadFiles()
        {
            var response = SendRequest("cd");
            if (response != null && response.TypeMessage == "cd")
            {
                try
                {
                    // Парсим сложный JSON ответ от сервера (путь + список)
                    JObject obj = JObject.Parse(response.Message);
                    string path = obj["currentPath"].ToString();
                    List<string> items = obj["items"].ToObject<List<string>>();

                    TxtCurrentPath.Text = $"Путь: {path}";
                    ListViewFiles.Items.Clear();

                    foreach (var item in items)
                    {
                        bool isDir = item.EndsWith("/");
                        ListViewFiles.Items.Add(new FileItem
                        {
                            Name = item,
                            DisplayName = item,
                            Type = isDir ? "📁 Папка" : "📄 Файл",
                            DownloadVisible = isDir ? Visibility.Collapsed : Visibility.Visible
                        });
                    }
                }
                catch { MessageBox.Show("Ошибка чтения списка файлов"); }
            }
        }

        private void ListViewFiles_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListViewFiles.SelectedItem is FileItem item && item.Name.EndsWith("/"))
            {
                string folder = item.Name.TrimEnd('/'); // Убираем слеш
                SendRequest($"cd {folder}");
                LoadFiles();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadFiles();

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string fileName = ((Button)sender).Tag.ToString();
            SaveFileDialog sfd = new SaveFileDialog { FileName = fileName };

            if (sfd.ShowDialog() == true)
            {
                var response = SendRequest($"get {fileName}");
                if (response.TypeMessage == "file")
                {
                    byte[] data = JsonConvert.DeserializeObject<byte[]>(response.Message);
                    File.WriteAllBytes(sfd.FileName, data);
                    MessageBox.Show("Файл скачан!");
                }
                else MessageBox.Show("Ошибка скачивания");
            }
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                byte[] data = File.ReadAllBytes(ofd.FileName);
                var fileInfo = new FileInfoFTP(data, Path.GetFileName(ofd.FileName));

                SendRequest(JsonConvert.SerializeObject(fileInfo)); // Отправка JSON файла
                LoadFiles();
                MessageBox.Show("Файл загружен!");
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var response = SendRequest("history");
            if (response != null && response.TypeMessage == "history")
            {
                List<string> history = JsonConvert.DeserializeObject<List<string>>(response.Message);
                new HistoryWindow(history).ShowDialog();
            }
        }
    }

    // Вспомогательный класс для WPF
    public class FileItem
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
        public Visibility DownloadVisible { get; set; }
    }
}
