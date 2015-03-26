﻿using RandM.RMLib;
using System;
using System.IO;
using System.Text;

namespace RandM.fTelnetProxy
{
    public class fTelnetProxy : IDisposable
    {
        private FileStream _LogStream = null;
        private object _LogStreamLock = new object();
        private bool _Stopping = false;
        private WebSocketServerThread _WebSocketServer = null;

        public void Dispose()
        {
            if (!_Stopping) Stop();
        }

        private void ParseCommandLineArgs()
        {
            string[] Args = Environment.GetCommandLineArgs();
            if (Args.Length > 1)
            {
                RMLog.Info("Overriding with settings from command-line");

                for (int i = 1; i < Args.Length; i++)
                {
                    string Arg = Args[i].TrimStart('/').TrimStart('-');
                    switch (Arg)
                    {
                        case "c":
                        case "cert":
                            i += 1;

                            // If file doesn't exist, and it's relative, convert to absolute
                            if (!File.Exists(Args[i]) && !Path.IsPathRooted(Args[i]))
                            {
                                Args[i] = StringUtils.PathCombine(ProcessUtils.StartupPath, Args[i]);
                            }
                            
                            if (File.Exists(Args[i]))
                            {
                                Config.Default.CertificateFilename = Args[i];
                                RMLog.Info("-Cert file......" + Config.Default.CertificateFilename);
                            }
                            else
                            {
                                RMLog.Error("-Cert file not found: '" + Args[i] + "'");
                            }
                            break;

                        case "?":
                        case "h":
                        case "help":
                            ShowHelp();
                            return;

                        case "l":
                        case "loglevel":
                            i += 1;
                            try
                            {
                                RMLog.Level = (LogLevel)Enum.Parse(typeof(LogLevel), Args[i]);
                                RMLog.Info("-Log level......" + RMLog.Level.ToString());
                            }
                            catch (Exception ex)
                            {
                                RMLog.Exception(ex, "-Invalid log level: '" + Args[i] + "'");
                            }
                            break;

                        case "p":
                        case "port":
                            i += 1;
                            try
                            {
                                Config.Default.ListenPort = Convert.ToInt16(Args[i]);
                                RMLog.Info("-Listen port...." + Config.Default.ListenPort.ToString());
                            }
                            catch (Exception ex)
                            {
                                RMLog.Exception(ex, "-Invalid port: '" + Args[i] + "'");
                            }
                            break;

                        case "pw":
                        case "password":
                            i += 1;
                            Config.Default.CertificatePassword = Args[i];
                            RMLog.Info("-Cert password..yes (hidden)");
                            break;

                        case "r":
                        case "relay":
                            i += 1;

                            // If file doesn't exist, and it's relative, convert to absolute
                            if (!File.Exists(Args[i]) && !Path.IsPathRooted(Args[i]))
                            {
                                Args[i] = StringUtils.PathCombine(ProcessUtils.StartupPath, Args[i]);
                            }

                            if (File.Exists(Args[i]))
                            {
                                Config.Default.RelayFilename = Args[i];
                                RMLog.Info("-Relay file....." + Config.Default.RelayFilename);
                            }
                            else
                            {
                                RMLog.Error("-Relay file not found: '" + Args[i] + "'");
                            }
                            break;

                        case "t":
                        case "target":
                            i += 1;
                            if (Args[i].Contains(":"))
                            {
                                Config.Default.TargetHostname = Args[i].Split(':')[0];
                                try
                                {
                                    Config.Default.TargetPort = Convert.ToInt16(Args[i].Split(':')[1]);
                                    RMLog.Info("-Target server.." + Config.Default.TargetHostname + ":" + Config.Default.TargetPort.ToString());
                                }
                                catch (Exception ex)
                                {
                                    RMLog.Exception(ex, "Invalid target port: '" + Args[i].Split(':')[1] + "'");
                                    RMLog.Info("-Target server.." + Config.Default.TargetHostname + ":" + Config.Default.TargetPort.ToString());
                                }
                            }
                            else
                            {
                                Config.Default.TargetHostname = Args[i];
                                RMLog.Info("-Target server.." + Config.Default.TargetHostname + ":" + Config.Default.TargetPort.ToString());
                            }
                            break;

                        default:
                            RMLog.Error("-Unknown parameter: '" + Args[i] + "'");
                            break;
                    }
                }
            }
        }

        void RMLog_Handler(object sender, RMLogEventArgs e)
        {
            string Message = string.Format("[{0}] [{1}] {2}\r\n",
                DateTime.Now.ToString(),
                e.Level.ToString(),
                e.Message);

            if (_LogStream != null)
            {
                byte[] MessageBytes = Encoding.ASCII.GetBytes(Message);
                _LogStream.Write(MessageBytes, 0, MessageBytes.Length);
                _LogStream.Flush();
            }

            if (Environment.UserInteractive) Console.Write(Message);
        }

        private void ShowHelp()
        {
            if (Environment.UserInteractive)
            {
                //Console.WriteLine("345678901234567890123456789012345678901234567890123456789012345678901234567890");
                Console.WriteLine();                                                                               //
                Console.WriteLine("Usage: " + Path.GetFileName(ProcessUtils.ExecutablePath) + " [parameters]");    //
                Console.WriteLine();                                                                               //
                Console.WriteLine("Service-mode parameters:");                                                     //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  /i, -i, /install, --install       Install the service");                      //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  /u, -u, /uninstall, --uninstall   Uninstall the service");                    //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  Edit the " + Config.Default.FileName + " file to configure");                 //
                Console.WriteLine();                                                                               //
                Console.WriteLine();                                                                               //
                Console.WriteLine("Console-mode parameters:");                                                     //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -p <port>                  Port to listen for connections on");               //
                Console.WriteLine("  --port <port>              Default is 1123");                                 //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -t <host:port>             Telnet server to redirect to");                    //
                Console.WriteLine("  --target <host:port>       Default is localhost:23");                         //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -c <filename>              PKCS12 file containing private key + cert chain"); //
                Console.WriteLine("  --cert <filename>          Needed if your site uses https://");               //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -pw <password>             Password to open the PKCS12 file");                //
                Console.WriteLine("  --password <password>      Needed if your PKCS12 file is password protected");//
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -l <level>                 Log level (Trace, Debug, Info, Warning, Error)");  //
                Console.WriteLine("  --loglevel <level>         Default is Info");                                 //
                Console.WriteLine();                                                                               //
                Console.WriteLine("  -?, -h, --help             Display this screen");                             //
                Console.WriteLine();                                                                               //
                //Console.WriteLine("345678901234567890123456789012345678901234567890123456789012345678901234567890");
                Environment.Exit(1);
            }
        }

        public void Start()
        {
            RMLog.Handler += RMLog_Handler;

            try
            {
                _LogStream = new FileStream(Path.ChangeExtension(ProcessUtils.ExecutablePath, ".log"), FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (Exception ex)
            {
                RMLog.Exception(ex, "Failed to open " + Path.ChangeExtension(ProcessUtils.ExecutablePath, ".log") + " for writing");
                Environment.Exit(1);
            }

            RMLog.Info("fTelnetProxy Starting Up");

            Config.Default.Load();
            ParseCommandLineArgs();

            try
            {
                RMLog.Info("Starting WebSocket proxy thread");
                _WebSocketServer = new WebSocketServerThread("0.0.0.0", Config.Default.ListenPort);
                _WebSocketServer.Start();
            }
            catch (Exception ex)
            {
                RMLog.Exception(ex, "Failed to start WebSocket proxy thread");
                Environment.Exit(1);
            }
        }

        public void Stop()
        {
            _Stopping = true;

            RMLog.Info("fTelnetProxy shutting down");

            if (_WebSocketServer != null)
            {
                RMLog.Info("Stopping WebSocket proxy thread");
                _WebSocketServer.Stop();
                _WebSocketServer.WaitFor();
            }

            RMLog.Info("fTelnetProxy terminated");

            if (_LogStream != null)
            {
                _LogStream.WriteByte(0x0D);
                _LogStream.WriteByte(0x0A);
                _LogStream.WriteByte(0x0D);
                _LogStream.WriteByte(0x0A);
                _LogStream.Close();
                _LogStream.Dispose();
            } 
        }
    }
}
