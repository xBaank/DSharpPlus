using System;
using System.Threading.Tasks;

namespace DSharpPlus.VoiceNext
{
    public sealed partial class VoiceNextConnection
    {
        public Task ConnectAsync()
        {
            var gatewayUri = new UriBuilder
            {
                Scheme = "wss",
                Host = this._webSocketEndpoint.Hostname,
                Query = "encoding=json&v=4"
            };

            return this._voiceWebsocket.ConnectAsync(gatewayUri.Uri);
        }

        public Task ReconnectAsync() => throw new NotImplementedException();

        public Task DisconnectAsync() => throw new NotImplementedException();
    }
}
