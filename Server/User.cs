namespace Server
{
    public class User
    {
        /// <summary> Логин пользователя </summary>
        public string login { get; set; }
        /// <summary> Пароль пользователя </summary>
        public string password { get; set; }
        /// <summary> Директория пользователя </summary>
        public string src { get; set; }
        /// <summary> Директория пользователя в которой он находится </summary>
        public string temp_src { get; set; }
        /// <summary> Конструктор для заполнения класса </summary>
        public User(string login, string password, string src)
        {
            this.login = login;
            this.password = password;
            this.src = src;

            temp_src = src;
        }
    }
}
