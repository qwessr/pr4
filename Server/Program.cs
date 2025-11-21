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
    }
}
