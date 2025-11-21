namespace Server
{
    public class ViewModelSend
    {
        /// <summary> Сообщение отправляемое сервером </summary>
        public string Message { get; set; }

        /// <summary> Код пользователя </summary>
        public int Id { get; set; }

        /// <summary> Конструктор для заполнения класса </summary>
        public ViewModelSend(string message, int Id)
        {
            this.Message = message;
            this.Id = Id;
        }
    }
}
