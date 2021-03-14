using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class ProcessCMD
{
    private static Process process;
    private static StreamReader outStream;
    private static StreamWriter inputStream;
    private static StreamReader errorStream;
    private static string curDirectory;
    private static string command;

    public static bool exec { get; set; }
    public static bool dirsync { get; set; }

    static ProcessCMD()
    {
        process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.CreateNoWindow = false;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        inputStream = process.StandardInput;
        outStream = process.StandardOutput;
        errorStream = process.StandardError;
        command = "";
        exec = false;
    }

    public static void Start()
    {
        outStream.ReadLine();
        outStream.ReadLine();
        outStream.ReadLine();
        inputStream.Write("\n");
        curDirectory = outStream.ReadLine();
        curDirectory = "PC-Telnet>";
        Brodcaster.outstr = outStream;
        Brodcaster.errorstr = errorStream;
        Brodcaster.StartBroadcast();
    }

    public static void Write(string str)
    {
        command += str;
        int rindex = command.IndexOf('\b');
        while (rindex >= 0)
        {
            if (rindex == 0)
                command = command.Remove(rindex, 1);
            else
                command = command.Remove(rindex - 1, 2);
            rindex = command.IndexOf('\b');
        }
        if (command.Contains('\n'))
        {
            if (command != "\n" && command != "\r\n")
            {
                inputStream.Write(command);
                exec = true;
            }
            command = "";
        }
    }

    public static string GetCurrentDirectory()
    {
        dirsync = true;
        inputStream.Write("\n");
        while (dirsync) { }
        return curDirectory;
    }

    public static void SetCurrentDirectory(string dir)
    {
        curDirectory = dir;
    }

    public static string GetCommand()
    {
        return command;
    }
}

public static class Brodcaster
{
    private static List<Socket> sockets;
    public static StreamReader outstr { get; set; }
    public static StreamReader errorstr { get; set; }
    private static CancellationTokenSource cancelTokenSource;

    static Brodcaster()
    {
        sockets = new List<Socket>();
        cancelTokenSource = new CancellationTokenSource();
    }

    public static void AddSocket(Socket socket)
    {
        sockets.Add(socket);
    }

    public static void StartBroadcast()
    {
        CancellationToken token = cancelTokenSource.Token;
        Task task = new Task(() =>
        {
            Task<string> task1 = outstr.ReadLineAsync();
            Task<string> task2 = errorstr.ReadLineAsync();
            while (!token.IsCancellationRequested)
            {
                if (task1.IsCompleted)
                {
                    string temp = task1.Result;
                    
                    if (ProcessCMD.dirsync)
                    {
                        ProcessCMD.SetCurrentDirectory(temp);
                        ProcessCMD.dirsync = false;
                    }
                    else if (ProcessCMD.exec)
                    {
                        outstr.ReadLine();
                        ProcessCMD.exec = false;
                    }
                    else
                    {
                        SendToAll(Encoding.ASCII.GetBytes(task1.Result + "\r\n"));
                    }
                    task1 = outstr.ReadLineAsync();
                }
                else if (task2.IsCompleted)
                {
                    SendToAll(Encoding.ASCII.GetBytes(task2.Result + "\r\n"));
                    task2 = errorstr.ReadLineAsync();
                }
            }
        });
        task.Start();
    }

    public static void StopBroadcast()
    {
        cancelTokenSource.Cancel();
    }

    public static void RemoveSocket(Socket socket)
    {
        sockets.Remove(socket);
    }

    public static void NotifyAllExept(Socket sender, byte[] message)
    {
        foreach (Socket socket in sockets)
        {
            try
            {
                if (socket != sender)
                {
                    socket.Send(message);
                }
            }
            catch
            {

            }
        }
    }

    public static void SendToAll(byte[] message)
    {
        foreach (Socket socket in sockets)
        {
            try
            {
                socket.Send(message);
            }
            catch
            {

            }
        }
    }
}

// State object for reading client data asynchronously  
public class StateObject
{
    // Size of receive buffer.  
    public const int BufferSize = 1024;

    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];

    // Client socket.
    public Socket workSocket = null;
}

public class AsynchronousSocketListener
{
    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    public static void StartListening()
    {
        // Establish the local endpoint for the socket.   
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 23);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        allDone.Set();
        Console.WriteLine("Соединение установлено");

        // Get the socket that handles the client request.  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        Brodcaster.AddSocket(handler);
        handler.Send(Encoding.ASCII.GetBytes('\r' + ProcessCMD.GetCurrentDirectory() + ProcessCMD.GetCommand()));

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;

        // Retrieve the state object and the handler socket  
        // from the asynchronous state object.  
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket.
        int bytesRead;
        try
        {
            bytesRead = handler.EndReceive(ar);
        }
        catch
        {
            Brodcaster.RemoveSocket(handler);
            return;
        }

        if (bytesRead > 0)
        {
            for (int i = bytesRead; i < state.buffer.Length; i++)
            {
                state.buffer[i] = 0;
            }
            // There  might be more data, so store the data received so far.  
            //state.sb.Append(Encoding.ASCII.GetString(
            //   state.buffer, 0, bytesRead));

            // Check for end-of-file tag. If it is not there, read
            // more data.  
            // content = state.sb.ToString();

            
            //Brodcaster.NotifyAllExept(handler, state.buffer);
            string income = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
            if (income.Contains('\n'))
            {
                Brodcaster.NotifyAllExept(handler, Encoding.ASCII.GetBytes("\r\n"));
            }
            ProcessCMD.Write(income);
            if (!income.Contains('\n'))
            {
                Brodcaster.SendToAll(Encoding.ASCII.GetBytes('\r' + ProcessCMD.GetCurrentDirectory() + ProcessCMD.GetCommand()));
            }


            if (income.IndexOf('\n') > -1)
            {
                
                //Brodcaster.NotifyAllExept(null, Encoding.ASCII.GetBytes(ProcessCMD.GetCurrentDirectory()));
            }


            // Not all data received. Get more. 
            try
            {
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }
            catch
            {
                Brodcaster.RemoveSocket(handler);
            }
            
        }
        else
        {
            Brodcaster.RemoveSocket(handler);
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
    }

    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main(String[] args)
    {
        ProcessCMD.Start();
        StartListening();
        return 0;
    }
}
