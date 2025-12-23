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
                    Console.ForegroundColor = ConsoleColor.White;
                    string message = Console.ReadLine();

                    if (CheckCommand(message))
                    {
                        ViewModelSend viewModelSend = new ViewModelSend(message, Id);

                        string[] parts = message.Split(' ');
                        if (parts[0].ToLower() == "set" && parts.Length > 1)
                        {
                            string nameFile = string.Join(" ", parts.Skip(1)).Replace("\"", "");
                            if (File.Exists(nameFile))
                            {
                                FileInfo info = new FileInfo(nameFile);
                                byte[] fileData = File.ReadAllBytes(nameFile);
                                FileInfoFTP fileInfoFTP = new FileInfoFTP(fileData, info.Name);
                                viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(fileInfoFTP), Id);
                            }
                            else
                            {
                                Console.WriteLine("Файл для отправки не найден локально.");
                                return;
                            }
                        }

                        // Отправка запроса
                        byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                        socket.Send(messageByte);

                        // Получение ответа
                        byte[] bytes = new byte[10485760]; // 10 MB
                        int byteRec = socket.Receive(bytes);
                        string messageServer = Encoding.UTF8.GetString(bytes, 0, byteRec);

                        ViewModelMessage response = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);


                        if (response.TypeMessage == "authorization" || response.TypeMessage == "autorization")
                        {
                            Id = int.Parse(response.Message);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"Успешный вход! Ваш ID: {Id}");
                        }
                        else if (response.TypeMessage == "message")
                        {
                            Console.WriteLine(response.Message);
                        }
                        else if (response.TypeMessage == "file")
                        {
                            var fileReceived = JsonConvert.DeserializeObject<FileInfoFTP>(response.Message);

                            File.WriteAllBytes(fileReceived.Name, fileReceived.Data);

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[СИСТЕМА]: Файл '{fileReceived.Name}' успешно получен и сохранен.");
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка: " + exp.Message);
            }

        }
    }
}