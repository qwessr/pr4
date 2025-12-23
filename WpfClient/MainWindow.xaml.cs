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
using System.Linq;

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

        private ViewModelMessage SendRequest(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);
                    var request = new ViewModelSend(message, userId);
                    byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                    socket.Send(data);

                    byte[] buffer = new byte[10485760]; // 10MB
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

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtLogin.Text) || string.IsNullOrEmpty(TxtPassword.Password)) return;

            serverIP = TxtServer.Text;
            serverPort = int.Parse(TxtPort.Text);
            string cmd = $"connect {TxtLogin.Text} {TxtPassword.Password}";

            var response = SendRequest(cmd);
            if (response != null && (response.TypeMessage == "authorization" || response.TypeMessage == "autorization"))
            {
                userId = int.Parse(response.Message);
                TxtStatus.Text = $"Авторизован (ID: {userId})";

                SetUIState(true);
                LoadFiles();
            }
            else
            {
                MessageBox.Show(response?.Message ?? "Неверный логин или пароль");
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            userId = -1;
            SetUIState(false);
            ListViewFiles.Items.Clear();
            TxtCurrentPath.Text = "Путь: не подключен";
            TxtStatus.Text = "Вышли из системы";
            TxtPassword.Clear();
        }

        private void SetUIState(bool loggedIn)
        {
            BtnRefresh.IsEnabled = loggedIn;
            BtnUpload.IsEnabled = loggedIn;
            BtnHistory.IsEnabled = loggedIn;
            BtnLogout.IsEnabled = loggedIn;
            PanelAuth.IsEnabled = !loggedIn;
        }

        private void LoadFiles()
        {
            var response = SendRequest("cd");
            if (response != null && response.TypeMessage == "cd")
            {
                try
                {
                    if (response.Message.Trim().StartsWith("{"))
                    {
                        JObject obj = JObject.Parse(response.Message);
                        TxtCurrentPath.Text = $"Путь: {obj["currentPath"]}";
                        UpdateFileList(obj["items"].ToObject<List<string>>());
                    }
                    else
                    {
                        string filesOnly = response.Message.Replace("Файлы:", "").Trim();
                        var items = filesOnly.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        UpdateFileList(items);
                    }
                }
                catch { TxtStatus.Text = "Ошибка отображения файлов"; }
            }
        }

        private void UpdateFileList(List<string> items)
        {
            ListViewFiles.Items.Clear();
            foreach (var item in items)
            {
                bool isDir = item.EndsWith("/") || !item.Contains(".");
                ListViewFiles.Items.Add(new FileItem
                {
                    Name = item.TrimEnd('/'),
                    DisplayName = item,
                    Type = isDir ? "📁 Папка" : "📄 Файл",
                    DownloadVisible = isDir ? Visibility.Collapsed : Visibility.Visible
                });
            }
        }

        private void ListViewFiles_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListViewFiles.SelectedItem is FileItem item && item.Type.Contains("Папка"))
            {
                SendRequest($"cd {item.Name}");
                LoadFiles();
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string fileName = ((Button)sender).Tag.ToString();
            SaveFileDialog sfd = new SaveFileDialog { FileName = fileName };

            if (sfd.ShowDialog() == true)
            {
                var response = SendRequest($"get {fileName}");
                if (response != null && response.TypeMessage == "file")
                {
                    var fileReceived = JsonConvert.DeserializeObject<FileInfoFTP>(response.Message);
                    File.WriteAllBytes(sfd.FileName, fileReceived.Data);
                    MessageBox.Show("Готово!");
                }
            }
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                byte[] data = File.ReadAllBytes(ofd.FileName);
                var fileInfo = new FileInfoFTP(data, Path.GetFileName(ofd.FileName));
                SendRequest(JsonConvert.SerializeObject(fileInfo));
                LoadFiles();
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var response = SendRequest("history");
            if (response != null && response.TypeMessage == "history")
            {
                try
                {
                    List<string> history = JsonConvert.DeserializeObject<List<string>>(response.Message);
                    MessageBox.Show(string.Join("\n", history), "История команд");
                }
                catch { MessageBox.Show("История пуста или недоступна"); }
            }
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow rw = new RegisterWindow(TxtServer.Text, int.Parse(TxtPort.Text));
            rw.Owner = this;
            rw.ShowDialog();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadFiles();
    }

    public class FileItem
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
        public Visibility DownloadVisible { get; set; }
    }
}