using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class ViewModelMessage
    {
        /// <summary> Команда </summary>
        public string Command { get; set; }

        /// <summary> Текст ответа </summary>
        public string Data { get; set; }

        /// <summary> Конструктор для заполнения данных </summary>
        public ViewModelMessage(string Command, string Data)
        {
            this.Command = Command;
            this.Data = Data;
        }
    }
}
