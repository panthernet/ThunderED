using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace THDWebServer.Classes
{
    internal class BackgroundSocketProcessor
    {
        private readonly List<WebSocketClient> _clients = new List<WebSocketClient>();

        internal async Task AddSocket(WebSocket socket, TaskCompletionSource<bool> finish)
        {
            var value = new WebSocketClient
            {
                Socket = socket,
                Finish = finish
            };
            _clients.Add(value);
            await Task.Factory.StartNew(async () =>
            {
                await ProcessSocket(value);
                value.Finish.SetResult(true);
                _clients.Remove(value);

            });
        }

        public async Task AddMessage(SocketMessageEnum type, string json)
        {
            foreach (var client in _clients)
            {
                //todo generate message if(client.)
            }
        }

        private async Task ProcessSocket(WebSocketClient client)
        {
            while (!client.Socket.CloseStatus.HasValue)
            {
                var buffer = new byte[1024 * 4];
                var result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (string.IsNullOrEmpty(client.Code))
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (string.IsNullOrEmpty(text))
                        {
                            await client.Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Unknown code",
                                CancellationToken.None);
                            return;
                        }

                        //todo check code

                        client.Code = text;
                    }
                    else
                    {
                        await client.Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Unexpected query",
                            CancellationToken.None);
                        return;
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    return;
                }
            }
        }
    }
}