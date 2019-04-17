using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Configuration;
using KSIS_LAB_4.HTTP;

namespace KSIS_LAB_4
{
    class Program
    {
        private static int ProxyPort { get => 54422; }

        static void Main(string[] args)
        {
            try
            {
                IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Loopback, ProxyPort);
                Socket listenSocket = new Socket(listenEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                listenSocket.Bind(listenEndPoint);
                listenSocket.Listen((int)SocketOptionName.MaxConnections);
                WriteLog("Прокси-сервер успешно запущен, ожидаение запросов на адрес {0} порт {1}.", IPAddress.Loopback.ToString(), ProxyPort);
                while (true)
                {
                    if (listenSocket.Poll(0, SelectMode.SelectRead))
                    {
                        Thread listenThread = new Thread(new ParameterizedThreadStart(ExecuteRequest));
                        listenThread.IsBackground = true;
                        listenThread.Start(listenSocket.Accept());
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static bool CheckIfHostInBlackList(string host, string path)
        {
            var blackList = ConfigurationManager.AppSettings;
            foreach (var key in blackList.AllKeys)
            {
                if (host.Equals(key) && path.Equals(blackList[key]))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SendErrorPage(byte[] response, Socket clientSocket, int statusCode, Parser httpParser)
        {
            response = GetHTTPError(statusCode, "Forbidden");
            if (clientSocket.Send(response, response.Length, SocketFlags.None) != response.Length)
            {
                Console.WriteLine("Данные хосту {0} не были отправлены.", httpParser.Host);
            }
            else
            {
                WriteLog("Получен ответ, код состояния {0}", statusCode);
            }
            clientSocket.Disconnect(true);
            clientSocket.Dispose();
            clientSocket.Close();
        }

        private static void ExecuteRequest(object arg)
        {
            try
            {
                using (Socket clientSocket = (Socket)arg)
                {
                    if (clientSocket.Connected)
                    {
                        byte[] httpRequest = ReadRequest(clientSocket);
                        Parser http = new Parser(httpRequest);
                        if (http.Method != Parser.MethodsList.CONNECT && http.Method != Parser.MethodsList.POST)
                        {
                            WriteLog("Запрос {0} байт, URL {1}:{2}", httpRequest.Length, http.Path, http.Port);
                            byte[] response = null;
                            if (CheckIfHostInBlackList(http.Host.ToLower(), http.Path.ToLower()))
                            {
                                SendErrorPage(response, clientSocket, 403, http);
                                return;
                            }
                            IPHostEntry serverIp = null;
                            try
                            {
                                serverIp = Dns.GetHostEntry(http.Host);
                            }
                            catch (SocketException)
                            { 
                                SendErrorPage(response, clientSocket, 404, http);
                                return;
                            }
                            IPEndPoint serverEndPoint = new IPEndPoint(serverIp.AddressList[0], http.Port);
                            using (Socket sendingToServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                            {
                                sendingToServerSocket.Connect(serverEndPoint);
                                if (sendingToServerSocket.Send(httpRequest, httpRequest.Length, SocketFlags.None) != httpRequest.Length)
                                {
                                    WriteLog("Данные хосту {0} не были отправлены...", http.Host);
                                }
                                else
                                {
                                    Parser httpResponse = new Parser(ReadRequest(sendingToServerSocket));
                                    if (httpResponse.Source != null && httpResponse.Source.Length > 0)
                                    {
                                        WriteLog("Ответ {0} байт, код состояния {1}", httpResponse.Source.Length, httpResponse.StatusCode);
                                        response = httpResponse.Source;
                                    }
                                }
                                sendingToServerSocket.Disconnect(true);
                                sendingToServerSocket.Dispose();
                                sendingToServerSocket.Close();
                            }
                            if (response != null)
                            {
                                clientSocket.Send(response, response.Length, SocketFlags.None);
                            }
                        }
                        clientSocket.Disconnect(true);
                        clientSocket.Dispose();
                        clientSocket.Close();
                    }
                }
            }
            catch (Exception) { }
        }

        private static byte[] ReadRequest(Socket mySocket)
        {
            byte[] buffer = new byte[mySocket.ReceiveBufferSize];
            int bytesReceived = 0;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                while (mySocket.Poll(1000000, SelectMode.SelectRead) && (bytesReceived = mySocket.Receive(buffer, mySocket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesReceived);
                }
                return memoryStream.ToArray();
            }
        }

        private static byte[] GetHTTPError(int statusCode, string statusMessage)
        {
            try
            {
                FileInfo errorFile = new FileInfo(string.Format(@"D:\\HTTP\HTTP{0}.htm", statusCode));
                byte[] headers = Encoding.ASCII.GetBytes(string.Format("HTTP/1.1 {0} {1}\r\nContent-Type: text/html\r\nContent-Length: {2}\r\n\r\n", statusCode, statusMessage, errorFile.Length));
                byte[] result = null;
                using (FileStream fs = new FileStream(errorFile.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                    {
                        result = new byte[headers.Length + fs.Length];
                        Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
                        Buffer.BlockCopy(br.ReadBytes(Convert.ToInt32(fs.Length)), 0, result, headers.Length, Convert.ToInt32(fs.Length));
                    }
                }
                return result;
            }
            catch (Exception) { return null; }
        }

        private static void WriteLog(string msg, params object[] args)
        {
            Console.WriteLine(DateTime.Now.ToString() + " : " + msg, args);
        }
    }
}