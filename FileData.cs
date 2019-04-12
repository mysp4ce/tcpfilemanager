using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientServerClassLibrary
{
    [Serializable]
    public class FileData
    {
        public List<string> Files { get; set; }
    }
}
