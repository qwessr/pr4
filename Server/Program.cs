using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
