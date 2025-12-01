namespace Common
{
    public class ViewModelMessage
    {
        /// <summary> Тип сообщения  </summary>
        public string TypeMessage { get; set; }

        /// <summary> Данные сообщения  </summary>
        public string Message { get; set; }

        public ViewModelMessage()
        {
            TypeMessage = "";
            Message = "";
        }

        public ViewModelMessage(string typeMessage, string message)
        {
            TypeMessage = typeMessage;
            Message = message;
        }
    }
}
