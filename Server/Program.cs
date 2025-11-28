using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common;
using Intercom.Data;
using Newtonsoft.Json;

namespace Server
{
    class Program
    {
        /// <summary> Список пользователей </summary>
        public static List<User> Users = new List<User>();
        /// <summary> IP-адрес сервера </summary>
        public static IPAddress IpAddress;
        /// <summary> Порт сервера </summary>
        public static int Port;

        static void Main(string[] args)
        {
            Users.Add(new User("test", "test123", @"A:\test"));
            Console.Write("Введите IP адрес сервера: ");
            string sIPAdress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIPAdress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер.");
                StartServer();
            }

            Console.Read();
        }


        /// <summary> Авторизация пользователя </summary>
        public static bool AutorizationUser(string login, string password)
        {
            // Создаём объект отвечающий за авторизованного пользователя
            User user = null;
            // Среди всех пользователей ищем пользователя с удовлетворяющим логином и паролем
            user = Users.Find(x => x.login == login && x.password == password);
            // Возвращаем результат что пользователь не равен null
            return user != null;
        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket sListener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            sListener.Bind(endPoint);
            sListener.Listen(10);
            Log("Сервер запущен.", ConsoleColor.Green);
            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    string Data = null;
                    byte[] Bytes = new byte[10485760];
                    int BytesRec = Handler.Receive(Bytes);
                    Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                    Log($"Сообщение от пользователя: (Data) \n", ConsoleColor.White);
                    string Peply = "";
                    ViewModelSend viewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                    if (viewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage;
                        string[] DataCommand = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOption.None);
                        if (DataCommand[0] == "connect")
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            if (AutorizationUser(DataMessage[1], DataMessage[2]))
                            {
                                int IdUser = Users.FindIndex(x => x.login == DataMessage[1] && x.password == DataMessage[2]);
                                viewModelMessage = new ViewModelMessage("autorization", IdUser.ToString());
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Не правильный логин и пароль пользователя.");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "cd")
                        {
                            if (viewModelSend.Id != -1)
                            {
                                string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                List<string> FoldersFiles = new List<string>();
                                if (DataMessage.Length == 1)
                                {
                                    Users[viewModelSend.Id].temp_src = Users[viewModelSend.Id].src;
                                    FoldersFiles = GetDirectory(Users[viewModelSend.Id].src);
                                }
                                else
                                {
                                    string cdfolder = "";
                                    for (int i = 1; i < DataMessage.Length; i++)
                                        if (cdfolder == "")
                                            cdfolder += DataMessage[i];
                                        else
                                            cdfolder += " " + DataMessage[i];
                                    Users[viewModelSend.Id].temp_src = Users[viewModelSend.Id].temp_src + cdfolder;
                                    FoldersFiles = GetDirectory(Users[viewModelSend.Id].temp_src);
                                }
                                if (FoldersFiles.Count == 0)
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует.");
                                else
                                    viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "get")
                        {
                            if (viewModelSend.Id != -1)
                            {
                                string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                string getFile = "";
                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (getFile == "")
                                        getFile += DataMessage[i];
                                    else
                                        getFile += "__" + DataMessage[i];

                                byte[] byteFile = File.ReadAllBytes(Users[viewModelSend.Id].temp_src + getFile);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else
                        {
                            if (viewModelSend.Id != -1)
                            {
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(viewModelSend.Message);
                                File.WriteAllBytes(Users[viewModelSend.Id].temp_src + @"\" + SendFileInfo.Name, SendFileInfo.Data);
                                viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                    }
                } catch (Exception exp)
                {
                    Log($"что то случилось (exp.Message)", ConsoleColor.Red);
                }
            } 

        }

        public static void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }

        /// <summary> Получение директорий </summary>
        public static List<string> GetDirectory(string src)
        {
            // Создаём список директорий
            List<string> FoldersFiles = new List<string>();
            // Проверяем что директория от которой мы отталкиваемся существует
            if (Directory.Exists(src))
            {
                // Получаем список всех директорий
                string[] dirs = Directory.GetDirectories(src);
                // Перебираем через цикл директории
                foreach (string dir in dirs)
                {
                    // Создаём новое имя для директории, заменяя часть пути
                    string NameDirectory = dir.Replace(src, "");
                    // Добавляем директорию в массив
                    FoldersFiles.Add(NameDirectory + "/");
                }
                // Получаем список всех файлов находящиеся в директории
                string[] files = Directory.GetFiles(src);
                // Перебираем через цикл файлы
                foreach (string file in files)
                {
                    // Создаём новое имя для файла, заменяя часть пути
                    string NameFile = file.Replace(src, "");
                    // Добавляем файл в массив
                    FoldersFiles.Add(NameFile);
                }
            }

            return FoldersFiles;
        }
    }
}
