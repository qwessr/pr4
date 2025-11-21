using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class FileInfoFTP
    {
        /// <summary> Массив байт </summary>
        public byte[] Data { get; set; }

        /// <summary> Имя файла </summary>
        public string Name { get; set; }

        /// <summary> Конструктор для заполнения класса </summary>
        public FileInfoFTP(byte[] Data, string Name)
        {
            this.Data = Data;
            this.Name = Name;
        }
    }

}
