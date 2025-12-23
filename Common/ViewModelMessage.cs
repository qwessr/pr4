namespace Common
{
    public class ViewModelMessage
    {
        public string TypeMessage { get; set; }
        public string Message { get; set; }
        public ViewModelMessage(string type, string msg) { TypeMessage = type; Message = msg; }
    }
}
    
