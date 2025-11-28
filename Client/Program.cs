using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common;
using MongoDB.Bson.IO;


namespace Client
{
    class Program
    {
        public static IPAddress IpAdress;
        public static int Port;
        public static int Id = -1;

        static void Main(string[] args)
        {
            Console.Write("Введите IP адрес сервера: ");
            string sIpAdress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAdress, out IpAdress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Подключаюсь к сервер.");
                while (true)
                {
                    Connection();
                }
            }
        }

        public static bool CheckCommand(string message)
        {
            bool BCommand = false;
            string[] Datamessage = message.Split(new string[1] { " " }, StringSplitOptions.None);

            if (Datamessage.Length > 0)
            {
                string Command = Datamessage[0];
                if (Command == "connect")
                {
                    if (Datamessage.Length != 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: connect [login] [password]\nПример: connect User1 P@sswOrd");
                        BCommand = false;
                    }
                    else
                        BCommand = true;
                }
                else if (Command == "cd")
                    BCommand = true;
                else if (Command == "get")
                {
                    if (Datamessage.Length == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: get [NameFile]\nПример: get Test.txt");
                        BCommand = false;
                    }
                    else
                        BCommand = true;
                }
                else if (Command == "set")
                {
                    if (Datamessage.Length == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: set [NameFile]\nПример: set Test.txt");
                        BCommand = false;
                    }
                    else
                        BCommand = true;
                }
            }
            return BCommand;
        }

        public static void Connection()
        {
            try
            {
                IPEndPoint endpoint = new IPEndPoint(IpAdress, Port);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(endpoint);

                if (socket.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    string message = Console.ReadLine();

                    if (CheckCommand(message))
                    {
                        ViewModelSend viewModelSend = new ViewModelSend(message, id);

                        if (message.Split(new string[] { " " }, StringSplitOptions.None)[0] == "set")
                        {
                            string[] DataMessage = message.Split(new string[] { " " }, StringSplitOptions.None);
                            string NameFile = "";

                            for (int i = 1; i < DataMessage.Length; i++)
                            {
                                if (NameFile == "")
                                    NameFile += DataMessage[i];
                                else
                                    NameFile += " " + DataMessage[i];
                            }

                            if (File.Exists(NameFile))
                            {
                                FileInfo fileInfo = new FileInfo(NameFile);
                                FileInfoFTP fileInfoFTP = new FileInfoFTP(File.ReadAllBytes(NameFile), fileInfo.Name);
                                viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), id);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Указанный файл не существует");
                            }
                        }

                        byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                        int ByteSent = socket.Send(messageByte);
                        byte[] bytes = new byte[10485760];
                        int ByteRec = socket.Receive(bytes);
                        string messageServer = Encoding.UTF8.GetString(bytes, 0, ByteRec);
                        ViewModelMessage viewModelMessage = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);

                        if (viewModelMessage.Command == "autorization")
                            id = int.Parse(viewModelMessage.Data);
                        else if (viewModelMessage.Command == "message")
                            Console.WriteLine(viewModelMessage.Data);
                        else if (viewModelMessage.Command == "cd")
                        {
                            List<string> FoldersFiles = new List<string>();
                            FoldersFiles = JsonConvert.DeserializeObject<List<string>>(viewModelMessage.Data);
                            foreach (string name in FoldersFiles)
                                Console.WriteLine(name);
                        }
                        else if (viewModelMessage.Command == "file")
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[] { " " }, StringSplitOptions.None);
                            string getFile = "";

                            for (int i = 1; i < DataMessage.Length; i++)
                            {
                                if (getFile == "")
                                    getFile += DataMessage[i];
                                else
                                    getFile += " " + DataMessage[i];
                            }

                            byte[] byteFile = JsonConvert.DeserializeObject<byte[]>(viewModelMessage.Data);
                            File.WriteAllBytes(getFile, byteFile);
                        }
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Подключение не удалось.");
                }
            }
            catch (Exception exp)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Что-то случилось - " + exp.Message);
            }
        }
    }
}
