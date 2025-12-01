using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Newtonsoft.Json; 

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
                Console.WriteLine("Данные успешно введены. Подключаюсь к серверу.");
                while (true)
                {
                    Connection();
                }
            }
        }

        public static bool CheckCommand(string message)
        {
            // Простая проверка на наличие текста
            return !string.IsNullOrWhiteSpace(message);
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
                        ViewModelSend viewModelSend = new ViewModelSend(message, Id);

                        // Логика отправки файла (set filename)
                        string[] parts = message.Split(' ');
                        if (parts[0] == "set" && parts.Length > 1)
                        {
                            string nameFile = string.Join(" ", parts, 1, parts.Length - 1);
                            if (File.Exists(nameFile))
                            {
                                FileInfo fileInfo = new FileInfo(nameFile);
                                FileInfoFTP fileInfoFTP = new FileInfoFTP(File.ReadAllBytes(nameFile), fileInfo.Name);
                                viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), Id);
                            }
                            else
                            {
                                Console.WriteLine("Файл не найден");
                                return;
                            }
                        }

                        byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                        socket.Send(messageByte);

                        byte[] bytes = new byte[10485760]; // 10 MB
                        int byteRec = socket.Receive(bytes);
                        string messageServer = Encoding.UTF8.GetString(bytes, 0, byteRec);

                        ViewModelMessage response = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);

                        if (response.TypeMessage == "authorization" || response.TypeMessage == "autorization")
                        {
                            Id = int.Parse(response.Message);
                            Console.WriteLine($"Авторизован. ID: {Id}");
                        }
                        else if (response.TypeMessage == "message")
                        {
                            Console.WriteLine(response.Message);
                        }
                        else if (response.TypeMessage == "cd" || response.TypeMessage == "list")
                        {
                            // Если сервер присылает JSON объект 
                            try
                            {
                                // Пытаемся распарсить как старый список
                                var list = JsonConvert.DeserializeObject<System.Collections.Generic.List<string>>(response.Message);
                                foreach (var item in list) Console.WriteLine(item);
                            }
                            catch
                            {
                                // Или просто выводим данные
                                Console.WriteLine(response.Message);
                            }
                        }
                        else if (response.TypeMessage == "file")
                        {
                            string getFile = "downloaded_file";
                            if (parts[0] == "get" && parts.Length > 1) getFile = parts[1];

                            byte[] byteFile = JsonConvert.DeserializeObject<byte[]>(response.Message);
                            File.WriteAllBytes(getFile, byteFile);
                            Console.WriteLine("Файл скачан.");
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine("Ошибка: " + exp.Message);
            }
        }
    }
}
