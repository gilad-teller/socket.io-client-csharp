﻿using SocketIOClient.Packgers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using WebSocketSharp;
using SocketIOClient.WebSocketClient;

namespace SocketIOClient.NetFx.WebSocketClient
{
    public class WebSocketSharpClient : IWebSocketClient
    {
        public WebSocketSharpClient(SocketIO io, PackgeManager parser)
        {
            _parser = parser;
            _io = io;
        }

        readonly PackgeManager _parser;
        readonly SocketIO _io;
        WebSocket _ws;
        CancellationTokenSource _connectionToken;
        readonly static Task _completedTask = Task.FromResult(false);
        public Action<WebSocket> WebSocketConfiger { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="System.Net.WebSockets.WebSocketException"></exception>
        public async Task ConnectAsync(Uri uri, WebSocketConnectionOptions options)
        {
            _ws = new WebSocket(uri.ToString());
            WebSocketConfiger?.Invoke(_ws);
            _ws.OnMessage += OnMessage;
            _ws.OnError += OnError;
            _ws.OnClose += OnClose;
            // set enabled client Ssl protocols as defined via options
            _ws.SslConfiguration.EnabledSslProtocols = _io.Options.EnabledSslProtocols;
            _ws.Connect();
            if (_ws.ReadyState == WebSocketState.Closed || _ws.ReadyState == WebSocketState.Closing)
            {
                throw new System.Net.WebSockets.WebSocketException("Unable to connect to the remote server.");
            }
            _ws.OnClose += OnClose;
            _connectionToken = new CancellationTokenSource();
            await Task.Factory.StartNew(ListenStateAsync, _connectionToken.Token);
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            Close("io server disconnect");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _io.InvokeError(e.Message);
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsText)
            {
#if DEBUG
                Trace.WriteLine($"⬇ {DateTime.Now} {e.Data}");
#endif
                _parser.Unpack(e.Data);
            }
            else if (e.IsBinary)
            {
#if DEBUG
                Trace.WriteLine($"⬇ {DateTime.Now} Binary message");
#endif
                _io.InvokeBytesReceived(e.RawData.Skip(1).ToArray());
            }
        }

        public Task SendMessageAsync(string text)
        {
            if (_ws == null)
            {
                throw new InvalidOperationException("Faild to emit, websocket is not connected yet.");
            }
            if (_ws.ReadyState != WebSocketState.Open)
            {
                throw new Exception("Connection is not open.");
            }
            _ws.Send(text);
#if DEBUG
            Trace.WriteLine($"⬆ {DateTime.Now} {text}");
#endif
            return _completedTask;
        }

        public Task SendMessageAsync(byte[] bytes)
        {
            if (_ws == null)
            {
                throw new InvalidOperationException("Faild to emit, websocket is not connected yet.");
            }
            if (_ws.ReadyState != WebSocketState.Open)
            {
                throw new Exception("Connection is not open.");
            }
            _ws.Send(bytes);
#if DEBUG
            Trace.WriteLine($"⬆ {DateTime.Now} Binary message");
#endif
            return _completedTask;
        }

        public Task DisconnectAsync()
        {
            _ws.Close();
            Close(null);
            return _completedTask;
        }

        private void Close(string reason)
        {
            if (reason != null)
            {
                _io.InvokeDisconnect(reason);
            }
            _connectionToken?.Cancel();
            var ws = _ws as IDisposable;
            ws.Dispose();
        }

        private async Task ListenStateAsync()
        {
            while (true)
            {
                await Task.Delay(200);
                if (_ws.ReadyState == WebSocketState.Closed)
                {
                    Close("io server disconnect");
                    return;
                }
            }
        }
    }
}

