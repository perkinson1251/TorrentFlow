using System.Net;
using System.Net.Sockets;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol
{
    public sealed class AsyncConnectData
    {
        public AsyncConnectData(IPEndPoint endpoint, TcpClient tcp)
        {
            endpoint.CannotBeNull();
            tcp.CannotBeNull();

            this.Endpoint = endpoint;
            this.Tcp = tcp;
        }
        private AsyncConnectData()
        {
        }
        public IPEndPoint Endpoint
        {
            get;
            private set;
        }
        public TcpClient Tcp
        {
            get;
            private set;
        }
    }
}
