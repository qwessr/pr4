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
    }
}
