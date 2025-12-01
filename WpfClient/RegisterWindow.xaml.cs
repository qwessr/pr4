using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using Common;
using Newtonsoft.Json;

namespace WpfClient
{
    public partial class RegisterWindow : Window
    {
        string ip; int port;
        public RegisterWindow(string ip, int port) { InitializeComponent(); this.ip = ip; this.port = port; }

        private void BtnReg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отправка запроса регистрации без авторизации (id = -1)
                var req = new ViewModelSend($"register {TxtRegLogin.Text} {TxtRegPass.Text} {TxtRegPath.Text}", -1);

                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    s.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                    s.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));

                    byte[] buf = new byte[1024];
                    int rec = s.Receive(buf);
                    var resp = JsonConvert.DeserializeObject<ViewModelMessage>(Encoding.UTF8.GetString(buf, 0, rec));

                    MessageBox.Show(resp.Message);
                    if (resp.Message.Contains("успешна")) Close();
                }
            }
            catch { MessageBox.Show("Ошибка регистрации"); }
        }
    }
}
