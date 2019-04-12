using ClientServerClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TcpClientServerSolution
{
    class ServerModel
    {
        private static TcpListener _tcpListener;
        private static Authentication _authentication = new Authentication();
        private const string _filesFolder = @"\\S0004\USERS\dh\Desktop\";
        private static DirectoryInfo _directoryInfo;
        private const string 
            _authorize = "AUTHORIZE",
            _register = "REGISTER",
            _download = "DOWNLOAD",
            _delete = "DELETE",
            _getdata = "GETDATA",
            _savedata = "SAVEDATA";

        public ServerModel(int port)
        {
            _tcpListener = TcpListener.Create(port);
            _directoryInfo = new DirectoryInfo(_filesFolder);
        }

        public async Task StartListener()
        {
            _tcpListener.Start();
            byte[] data = new byte[16];

            while (true)
                using (var tcpClient = await _tcpListener.AcceptTcpClientAsync())
                using (var networkStream = tcpClient.GetStream())
                    if (tcpClient.Connected)
                    {
                        int buffLength = await networkStream.ReadAsync(data, 0, data.Length);

                        string command = Encoding.ASCII.GetString(data).TrimEnd('\0');

                        if (command.Length > buffLength)
                            command = command.Substring(0, buffLength);
                        
                        if (command == _authorize)
                            await Authorize(networkStream);

                        if (command == _register)
                            await Register(networkStream);

                        if (command == _download)
                            await SendFile(networkStream);

                        if (command == _delete)
                            await DeleteFile(networkStream);

                        if (command == _getdata)
                            await FormDataFromFile(networkStream);

                        if (command == _savedata)
                            await SaveFile(networkStream);
                    }
        }

        private async Task Authorize(NetworkStream networkStream)
        {
            var xmlSerializer = new XmlSerializer(typeof(ClientData));
            ClientData user = (ClientData)xmlSerializer.Deserialize(networkStream);
            bool authTry = _authentication.Authorize(user.Username, user.Password);
            byte[] reply = Encoding.ASCII.GetBytes(authTry.ToString());
            await networkStream.WriteAsync(reply, 0, reply.Length);

            SendFilesList(networkStream);

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_authorize} user: '{user.Username}' pass: '{user.Password}' reply: {authTry}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_authorize} user: '{user.Username}' pass: '{user.Password}' reply: {authTry}");
        }

        private async Task Register(NetworkStream networkStream)
        {
            var xmlUserSerializer = new XmlSerializer(typeof(ClientData));
            ClientData user = (ClientData)xmlUserSerializer.Deserialize(networkStream);
            bool regTry = _authentication.Register(user.Username, user.Password);
            byte[] reply = Encoding.ASCII.GetBytes(regTry.ToString());
            await networkStream.WriteAsync(reply, 0, reply.Length);

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_register} user: '{user.Username}' pass: '{user.Password}' reply: {regTry}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_register} user: '{user.Username}' pass: '{user.Password}' reply: {regTry}");
        }

        private void SendFilesList(NetworkStream networkStream)
        {
            var xmlFileSerializer = new XmlSerializer(typeof(FileData));
            var Files = FormFilesArray();
            xmlFileSerializer.Serialize(networkStream, Files);
        }

        private FileData FormFilesArray()
        {
            FileData fileData = new FileData();
            fileData.Files = new List<string>();

            foreach (FileInfo f in _directoryInfo.GetFiles("*.txt"))
                fileData.Files.Add(f.Name);

            return fileData;
        }

        private async Task SendFile(NetworkStream networkStream)
        {
            byte[] fileName = new byte[64];
            await networkStream.ReadAsync(fileName, 0, fileName.Length);
            string fileStringName = Encoding.ASCII.GetString(fileName).Trim('\0');

            foreach (FileInfo file in _directoryInfo.GetFiles(fileStringName))
                using (var fileStream = new FileStream(file.FullName, FileMode.Open))
                    await fileStream.CopyToAsync(networkStream);

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_download} filename: {fileStringName}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_download} filename: {fileStringName}");
        }

        private async Task DeleteFile(NetworkStream networkStream)
        {
            byte[] fileName = new byte[64];
            await networkStream.ReadAsync(fileName, 0, fileName.Length);
            string fileStringName = Encoding.ASCII.GetString(fileName).Trim('0');

            foreach (FileInfo file in _directoryInfo.GetFiles(Encoding.ASCII.GetString(fileName).Trim('\0')))
                file.Delete();

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_delete} filename: {fileStringName}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_delete} filename: {fileStringName}");
        }

        private async Task FormDataFromFile(NetworkStream networkStream)
        {
            byte[] fileName = new byte[64];
            byte[] fileContent = null;
            int buff = await networkStream.ReadAsync(fileName, 0, fileName.Length);
            string fileStringName = Encoding.ASCII.GetString(fileName).Substring(0, buff);
            
            if (_directoryInfo.GetFiles(Encoding.ASCII.GetString(fileName).Trim('\0')) == null)
                return;

            foreach (FileInfo file in _directoryInfo.GetFiles(Encoding.ASCII.GetString(fileName).Trim('\0')))
                fileContent = File.ReadAllBytes(file.FullName);

            if (fileContent == null)
                return;

            await Task.Run(() => SendLength(networkStream, fileContent.Length.ToString()));

            int length = fileContent.Length;
            byte[] leng = Encoding.ASCII.GetBytes(length.ToString());
            await networkStream.WriteAsync(fileContent, 0, fileContent.Length);

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_getdata} filename: {fileStringName} fileLength: {length}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_getdata} filename: {fileStringName} fileLength: {length}");
        }
        private async Task SendLength(NetworkStream networkStream, string length)
        {
            byte[] fileLength = Encoding.ASCII.GetBytes(length.ToString());
            await networkStream.WriteAsync(fileLength, 0, fileLength.Length);
        }

        private async Task SaveFile(NetworkStream networkStream)
        {

            byte[] fileLength = new byte[16];
            int lengthBuff = await networkStream.ReadAsync(fileLength, 0, fileLength.Length);

            string stringLength = Encoding.ASCII.GetString(fileLength).Substring(0, lengthBuff);
            int length = Convert.ToInt32(stringLength);
            byte[] fileContent = new byte[length];
            await networkStream.ReadAsync(fileContent, 0, fileContent.Length);
            
            byte[] fileName = new byte[32];
            int filenamebuff = await networkStream.ReadAsync(fileName, 0, fileName.Length);
            string fileStringName = Encoding.ASCII.GetString(fileName).Substring(0, filenamebuff);

            foreach (FileInfo file in _directoryInfo.GetFiles(Encoding.ASCII.GetString(fileName).Trim('\0')))
                File.WriteAllBytes(file.FullName, fileContent);

            Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_savedata} filename: {fileStringName} fileLength: {length}");
            Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] command: {_savedata} filename: {fileStringName} fileLength: {length}");
        }
        
        ~ServerModel()
        {
            if (_tcpListener != null)
                _tcpListener.Stop();
        }
    }
}
