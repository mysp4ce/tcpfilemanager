using ClientServerClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Client
{
    class ClientModel
    {
        private IPAddress _ipAddress;
        private int _port;
        private const string
            _authorize = "AUTHORIZE",
            _register = "REGISTER",
            _download = "DOWNLOAD",
            _delete = "DELETE",
            _getdata = "GETDATA",
            _savedata = "SAVEDATA";

        public ClientModel(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public async Task<List<string>> SignIn(string username, string password)
        {
            using (var tcpClient = new TcpClient())
            {
                XmlSerializer xmlFileSerializer = new XmlSerializer(typeof(FileData));
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_authorize);
                    networkStream.Write(bytesWrite, 0, bytesWrite.Length);

                    Serialize(networkStream, username, password);
                    tcpClient.Client.Shutdown(SocketShutdown.Send);

                    byte[] bytesRead = new byte[8];
                    networkStream.Read(bytesRead, 0, bytesRead.Length);

                    if (Encoding.ASCII.GetString(bytesRead).Trim('\0') == "True")
                    {
                        FileData fileData = (FileData)xmlFileSerializer.Deserialize(networkStream);
                        return fileData.Files;
                    }
                }
                
                throw new ArgumentException("Wrong login or password");
            }
        }
       
        public async Task<bool> SignUp(string username, string password)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_register);
                    await networkStream.WriteAsync(bytesWrite, 0, bytesWrite.Length);

                    Serialize(networkStream, username, password);
                    tcpClient.Client.Shutdown(SocketShutdown.Send);

                    byte[] bytesRead = new byte[16];
                    networkStream.Read(bytesRead, 0, bytesRead.Length);

                    if (Encoding.ASCII.GetString(bytesRead).Trim('\0') == "True")
                        return true;
                    else
                        return false;
                }
            }
        }

        public async Task DownloadFile(string filename, string pathToDownload)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_download);
                    await networkStream.WriteAsync(bytesWrite, 0, bytesWrite.Length);

                    if (filename.Length != 0)
                    {
                        byte[] fileName = Encoding.ASCII.GetBytes(filename);
                        await networkStream.WriteAsync(fileName, 0, fileName.Length);
                    }

                    using (FileStream fileStream = new FileStream(pathToDownload, FileMode.Create))
                        await networkStream.CopyToAsync(fileStream);
                }
            }
        }

        public async Task DeleteFile(string filename)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_delete);
                    await networkStream.WriteAsync(bytesWrite, 0, bytesWrite.Length);

                    if (filename.Length != 0)
                    {
                        byte[] fileName = Encoding.ASCII.GetBytes(filename);
                        await networkStream.WriteAsync(fileName, 0, fileName.Length);
                    }
                }
            }
        }

        public async Task<string> GetFileData(string filename)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_getdata);
                    await networkStream.WriteAsync(bytesWrite, 0, bytesWrite.Length);

                    if (filename.Length != 0)
                    {
                        byte[] fileName = Encoding.ASCII.GetBytes(filename);
                        await networkStream.WriteAsync(fileName, 0, fileName.Length);
                    }

                    byte[] fileLength = new byte[16];
                    int lengthBuff = await networkStream.ReadAsync(fileLength, 0, fileLength.Length);

                    string stringLength = Encoding.ASCII.GetString(fileLength).Substring(0, lengthBuff);
                    int length = Convert.ToInt32(stringLength);

                    byte[] bytesRead = new byte[length];
                    await networkStream.ReadAsync(bytesRead, 0, bytesRead.Length);

                    tcpClient.Client.Shutdown(SocketShutdown.Receive);

                    return Encoding.ASCII.GetString(bytesRead);
                }
            }
        }

        public async Task SaveFileData(string filename, string filecontent)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);

                using (var networkStream = tcpClient.GetStream())
                {
                    byte[] bytesWrite = Encoding.ASCII.GetBytes(_savedata);
                    await networkStream.WriteAsync(bytesWrite, 0, bytesWrite.Length);
                    
                    await Task.Run(() => SendLength(networkStream, filecontent)); // В связи с частой передачей избыточных
                                                                          // реализовал вызов через Task.Run
                    byte[] fileData = Encoding.ASCII.GetBytes(filecontent);
                    await networkStream.WriteAsync(fileData, 0, fileData.Length);

                    if (filename.Length != 0)
                    {
                        byte[] fileName = Encoding.ASCII.GetBytes(filename);
                        await networkStream.WriteAsync(fileName, 0, fileName.Length);
                    }
                }
            }
        }

        private async Task SendLength(NetworkStream networkStream, string filecontent)
        {
            byte[] fileLength = Encoding.ASCII.GetBytes(filecontent.Length.ToString());
            await networkStream.WriteAsync(fileLength, 0, fileLength.Length);
        }

        private void Serialize(NetworkStream networkStream, string username, string password)
        {
            var user = new ClientData { Username = username, Password = password };
            var xmlSerializer = new XmlSerializer(typeof(ClientData));
            xmlSerializer.Serialize(networkStream, user);
        }
    }
}