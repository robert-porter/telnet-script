using System;
using System.Net.Sockets;
using System.Text;

namespace CaptiveAudio
{

    enum Verbs {
        WILL = 251,
        WONT = 252,
        DO = 253,
        DONT = 254,
        IAC = 255
    }

    enum Options
    {
        SGA = 3
    }

    class TelnetConnection
    {
        TcpClient tcpSocket;

        int TimeOutMs = 100;

        public TelnetConnection(string Hostname, int Port)
        {
            tcpSocket = new TcpClient(Hostname, Port);

        }

        public string Login(int LoginTimeOutMs)
        {
            int oldTimeOutMs = TimeOutMs;
            TimeOutMs = LoginTimeOutMs;
            string s = Read();
            if (s.TrimEnd().EndsWith(":"))
                throw new Exception("Failed to connect : no login provided but there appears to be a login prompt");
            TimeOutMs = oldTimeOutMs;
            return s;
        }
        public string Login(string Username,string Password,int LoginTimeOutMs)
        {
            int oldTimeOutMs = TimeOutMs;
            TimeOutMs = LoginTimeOutMs;
            string s = Read();
            if (!s.TrimEnd().EndsWith(":"))
                throw new Exception("Failed to connect : no login prompt");
                
            WriteLine(Username);

            s += Read();
            
            if (!s.TrimEnd().EndsWith(":"))
                    throw new Exception("Failed to connect : no password prompt");
            WriteLine(Password);

            s += Read();
            
            TimeOutMs = oldTimeOutMs;
            return s;
        }

        public void WriteLine(string cmd)
        {
            Write(cmd + "\r");
        }

        public void Write(string cmd)
        {
            if (!tcpSocket.Connected) return;
            byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(cmd.Replace("\0xFF","\0xFF\0xFF"));
            tcpSocket.GetStream().Write(buf, 0, buf.Length);
        }

        public string Read()
        {
            if (!tcpSocket.Connected) return null;
            StringBuilder sb=new StringBuilder();
            do
            {
                ParseTelnet(sb);
                System.Threading.Thread.Sleep(TimeOutMs);
            } while (tcpSocket.Available > 0);
            return sb.ToString();
        }

        public bool IsConnected
        {
            get { return tcpSocket.Connected; }
        }

        void ParseTelnet(StringBuilder sb)
        {
            while (tcpSocket.Available > 0)
            {
                int input = tcpSocket.GetStream().ReadByte();
                switch (input)
                {
                    case -1 :
                        break;
                    case (int)Verbs.IAC:
                        // interpret as command
                        int inputverb = tcpSocket.GetStream().ReadByte();
                        if (inputverb == -1) break;
                        switch (inputverb)
                        {
                            case (int)Verbs.IAC: 
                                //literal IAC = 255 escaped, so append char 255 to string
                                sb.Append(inputverb);
                                break;
                            case (int)Verbs.DO: 
                            case (int)Verbs.DONT:
                            case (int)Verbs.WILL:
                            case (int)Verbs.WONT:
                                // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                                int inputoption = tcpSocket.GetStream().ReadByte();
                                if (inputoption == -1) break;
                                tcpSocket.GetStream().WriteByte((byte)Verbs.IAC);
                                if (inputoption == (int)Options.SGA )
                                    tcpSocket.GetStream().WriteByte(inputverb == (int)Verbs.DO ? (byte)Verbs.WILL:(byte)Verbs.DO); 
                                else
                                    tcpSocket.GetStream().WriteByte(inputverb == (int)Verbs.DO ? (byte)Verbs.WONT : (byte)Verbs.DONT); 
                                tcpSocket.GetStream().WriteByte((byte)inputoption);
                                break;
                            default:
                                break;
                        }
                        break;
                    default:
                        sb.Append( (char)input );
                        break;
                }
            }
        }
    }

    struct Processor
    {
        public String Hostname;
        public int Port;
        public bool HasLogin;
        public String UserName;
        public String Password;
    }

    class Program
    {

        static Processor ParseCommandLine(string[] args)
        {
            
            Processor processor = new Processor();
            processor.UserName = "";
            processor.Port = 23;
            bool hasHostname = false;

            processor.HasLogin = false;

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "-hostname":
                        {
                            processor.Hostname = args[i + 1];
                            hasHostname = true;
                            break;
                        }
                    case "-port":
                        {
                            processor.Port = int.Parse(args[i + 1]);
                            break;
                        }
                    case "-username":
                        {
                            processor.UserName = args[i + 1];
                            processor.HasLogin = true;
                            break;
                        }
                    case "-password":
                        {
                            processor.Password = args[i + 1];
                            processor.HasLogin = true;
                            break;
                        }
                }
            }

            if (!hasHostname)
            {
                throw new Exception("You must specify an argument for -hostname.");
            }
            return processor;
        }

        static void Main(string[] args)
        {
            try
            {
                Processor processor = ParseCommandLine(args);

                TelnetConnection tc = new TelnetConnection(processor.Hostname, processor.Port);

                String response;

                if (processor.HasLogin)
                    response = tc.Login(processor.UserName, processor.Password, 100);
                else
                    response = tc.Login(100);


                Console.Write(response);

                // server output should end with ">", otherwise the connection failed
                response = response.TrimEnd();
                if (response.Length == 0)
                    throw new Exception("Connection failed.");
                response = response.Substring(response.Length - 1, 1);
                if (response != ">")
                    throw new Exception("Connection failed");

                String input = "";

                // while connected
                while (tc.IsConnected && input != null && input.Trim() != "exit")
                {
                    // display server output
                    Console.Write(tc.Read());

                    // send client input to server
                    input = Console.ReadLine();

                    if (input != null && input != "exit")
                    {
                        tc.WriteLine(input);

                        // display server output
                        Console.Write(tc.Read());
                    }
                }
                Console.WriteLine("***telnet-script: session ended");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
    
}
