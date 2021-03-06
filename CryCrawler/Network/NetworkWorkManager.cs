﻿using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using CryCrawler.Security;
using System.Threading.Tasks;

namespace CryCrawler.Network
{
    public class NetworkWorkManager
    {
        public IPAddress Address { get; private set; }
        public string Hostname { get; }
        public int Port { get; }
        public bool IsActive { get; private set; }
        public bool IsConnected { get; private set; }
        public string ClientId { get; private set; }
        public string PasswordHash { get; }
        public NetworkMessageHandler<NetworkMessage> MessageHandler { get; private set; }

        public delegate void ConnectedHandler(string clientid);
        public delegate void MessageReceivedHandler(NetworkMessage w,
            NetworkMessageHandler<NetworkMessage> msgHandler);

        public event ConnectedHandler Connected;
        public event ConnectedHandler Disconnected;
        public event MessageReceivedHandler MessageReceived;

        private TcpClient client;
        private CancellationTokenSource csrc;

        public NetworkWorkManager(string hostname, int port, string password = null, string clientId = null)
        {
            Port = port;
            Hostname = hostname;
            ClientId = clientId;
            PasswordHash = SecurityUtils.GetHash(password);
        }

        public void Start()
        {
            if (IsActive) throw new InvalidOperationException("NetworkWorkManager already running!");

            try
            {
                // validate hostname
                if (IPAddress.TryParse(Hostname, out IPAddress addr) == false)
                {
                    // attempt to resolve hostname
                    var hostEntry = Dns.GetHostEntry(Hostname);
                    if (hostEntry.AddressList.Length > 0)
                    {
                        Address = hostEntry.AddressList.Last();
                    }
                    else throw new InvalidOperationException("Failed to resolve hostname!");
                }
                else Address = addr;

                // start connection loop
                csrc = new CancellationTokenSource();
                ConnectionLoop(csrc.Token);
            }
            finally
            {
                IsActive = true;
            }
        }

        public void Stop()
        {
            if (!IsActive) return;

            try
            {
                csrc.Cancel();
            }
            finally
            {
                IsActive = false;
                IsConnected = false;
            }
        }

        void ConnectionLoop(CancellationToken token)
        {
            new Task(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    ManualResetEvent reset = new ManualResetEvent(false);

                    try
                    {
                        client = new TcpClient();
                        client.Connect(new IPEndPoint(Address, Port));

                        Logger.Log($"Connecting to host...", Logger.LogSeverity.Debug);

                        var stream = client.GetStream();

                        Logger.Log($"Establishing secure connection...", Logger.LogSeverity.Debug);
                        // setup SSL here
                        var sslstream = SecurityUtils.ClientEstablishSSL(stream);

                        // message handler here
                        MessageHandler = new NetworkMessageHandler<NetworkMessage>(sslstream,
                            w =>
                            {
                                if (w.MessageType == NetworkMessageType.ConfigUpdate ||
                                    w.MessageType == NetworkMessageType.WorkLimitUpdate ||
                                    (!token.IsCancellationRequested && IsConnected))
                                {
                                    // do not log status checks because they happen too frequently
                                    /*
                                    if (w.MessageType != NetworkMessageType.StatusCheck)
                                        Logger.Log("Received message -> " + w.MessageType.ToString(), Logger.LogSeverity.Debug);
                                    */

                                    if (w.MessageType == NetworkMessageType.Disconnect) client.ProperlyClose();

                                    MessageReceived?.Invoke(w, MessageHandler);
                                }
                            });

                        // if message handler throws an exception, dispose it
                        MessageHandler.ExceptionThrown += (a, b) =>
                        {
                            Logger.Log("[MessageHandler] " + b.GetDetailedMessage(), Logger.LogSeverity.Debug);

                            MessageHandler.Dispose();
                            reset.Set();
                        };

                        Logger.Log($"Validating host...", Logger.LogSeverity.Debug);

                        // handshake
                        ClientId = SecurityUtils.DoHandshake(MessageHandler, PasswordHash, true, ClientId);

                        // wait a bit (to make sure message handler callbacks don't get early messages)
                        Task.Delay(100).Wait();

                        IsConnected = true;

                        Logger.Log($"Connected to host. (Id: {ClientId})");
                        Connected?.Invoke(ClientId);

                        // wait here until exception is thrown on message handler
                        reset.WaitOne();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Host connection error. " + ex.Message, Logger.LogSeverity.Debug);
                    }
                    finally
                    {
                        if (IsConnected)
                        {
                            Logger.Log("Disconnected from host");
                            Disconnected?.Invoke(ClientId);
                            IsConnected = false;
                        }

                        client.ProperlyClose();
                    }

                    Task.Delay(300).Wait();
                }

            }, token, TaskCreationOptions.LongRunning).Start();
        }
    }
}
