using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Telnet_Client_CS
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.ASCII;
            Console.OutputEncoding = Encoding.ASCII;

            //Console.WriteLine("Введите адрес сервера:");
            //string address = Console.ReadLine();
            IPEndPoint iPEndPoint;
            //if (address == "localhost")
            //{
            iPEndPoint = new IPEndPoint(IPAddress.Loopback, 23);
            //}
            //else
            //{
            //    return;
            //}

            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                server.Connect(iPEndPoint);
            }
            catch
            {
                Console.WriteLine("Failed to connect");
            }

            Task terminalToServer = new Task(() =>
            {

                while (true)
                {
                    string message = "";
                    ConsoleKeyInfo key = Console.ReadKey();
                    message += key.KeyChar;
                    if (key.KeyChar == '\r')
                    {
                        message += '\n'; 
                        Console.WriteLine();
                    }
                    
                    server.Send(Encoding.ASCII.GetBytes(message));
                    message = "";

                }
            });
            terminalToServer.Start();

            Task serverToTerminal = new Task(() =>
            {
                byte[] buffer = new byte[256];
                buffer.Initialize();
                do
                {
                    int bytescount = server.Receive(buffer);
                    byte[] bytestr = new byte[bytescount];
                    for (int i = 0; i < bytescount; i++)
                    {
                        bytestr[i] = buffer[i];
                    }
                    string temp = Encoding.ASCII.GetString(bytestr);
                    bool nl = false;
                    Console.Write(temp);
                    //Console.SetCursorPosition(temp.Length - 1, Console.CursorTop);
                } while (buffer.Length != 0);
            });
            serverToTerminal.Start();
            serverToTerminal.Wait();

            server.Shutdown(SocketShutdown.Both);
            server.Close();
        }
    }
}
