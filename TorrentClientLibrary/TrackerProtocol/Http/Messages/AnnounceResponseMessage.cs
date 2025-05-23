using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.BEncoding;
using TorrentFlow.TorrentClientLibrary.Extensions;
using TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages;

namespace TorrentFlow.TorrentClientLibrary.TrackerProtocol.Http.Messages
{
    public class AnnounceResponseMessage
    {
        private AnnounceResponseMessage()
        {
        }
        private AnnounceResponseMessage(string failureReason, TimeSpan updateInterval, int seedersCount, int leecherCount, IEnumerable<IPEndPoint> peers)
        {
            updateInterval.CannotBeEqualTo(TimeSpan.Zero);
            seedersCount.MustBeGreaterThanOrEqualTo(0);
            leecherCount.MustBeGreaterThanOrEqualTo(0);
            peers.CannotBeNull();

            this.FailureReason = failureReason;
            this.UpdateInterval = updateInterval;
            this.SeederCount = seedersCount;
            this.LeecherCount = leecherCount;
            this.Peers = peers;
        }
        public string FailureReason
        {
            get;
            private set;
        }
        public int LeecherCount
        {
            get;
            private set;
        }
        public IEnumerable<IPEndPoint> Peers
        {
            get;
            private set;
        }
        public int SeederCount
        {
            get;
            private set;
        }
        public TimeSpan UpdateInterval
        {
            get;
            private set;
        }
        public string WarningMessage
        {
            get;
            private set;
        }
        public static bool TryDecode(byte[] data, out AnnounceResponseMessage message)
        {
            BEncodedValue value;
            string faliureReason = null;
            TimeSpan interval = TimeSpan.Zero;
            int complete = -1;
            int incomplete = -1;
            string peerId = null;
            string peerIp = null;
            int peerPort = -1;
            IDictionary<string, IPEndPoint> peers = new Dictionary<string, IPEndPoint>();
            IPAddress tmpIpAddress;
            IPEndPoint endpoint;
            BEncodedString failureReasonKey = new BEncodedString("failure reason");
            BEncodedString intervalKey = new BEncodedString("interval");
            BEncodedString completeKey = new BEncodedString("complete");
            BEncodedString incompleteKey = new BEncodedString("incomplete");
            BEncodedString peersKey = new BEncodedString("peers");
            BEncodedString peerIdKey = new BEncodedString("peer id");
            BEncodedString ipaddressKey = new BEncodedString("ip");
            BEncodedString portKey = new BEncodedString("port");

            message = null;

            if (data.IsNotNullOrEmpty())
            {
                value = BEncodedValue.Decode(data);

                if (value is BEncodedDictionary)
                {
                    if (value.As<BEncodedDictionary>().ContainsKey(failureReasonKey) &&
                        value.As<BEncodedDictionary>()[failureReasonKey] is BEncodedString)
                    {
                        faliureReason = value.As<BEncodedDictionary>()[failureReasonKey].As<BEncodedString>().Text;
                    }

                    if (value.As<BEncodedDictionary>().ContainsKey(intervalKey) &&
                        value.As<BEncodedDictionary>()[intervalKey] is BEncodedNumber)
                    {
                        interval = TimeSpan.FromSeconds(value.As<BEncodedDictionary>()[intervalKey].As<BEncodedNumber>().Number);
                    }
                    else
                    {
                        return false;
                    }

                    if (value.As<BEncodedDictionary>().ContainsKey(completeKey) &&
                        value.As<BEncodedDictionary>()[completeKey] is BEncodedNumber)
                    {
                        complete = (int)value.As<BEncodedDictionary>()[completeKey].As<BEncodedNumber>().Number;
                    }
                    else
                    {
                        return false;
                    }

                    if (value.As<BEncodedDictionary>().ContainsKey(incompleteKey) &&
                        value.As<BEncodedDictionary>()[incompleteKey] is BEncodedNumber)
                    {
                        incomplete = (int)value.As<BEncodedDictionary>()[incompleteKey].As<BEncodedNumber>().Number;
                    }
                    else
                    {
                        return false;
                    }

                    if (value.As<BEncodedDictionary>().ContainsKey(peersKey) &&
                        value.As<BEncodedDictionary>()[peersKey] is BEncodedList)
                    {
                        foreach (var item in value.As<BEncodedDictionary>()[peersKey].As<BEncodedList>())
                        {
                            if (item is BEncodedDictionary)
                            {
                                if (item.As<BEncodedDictionary>().ContainsKey(peerIdKey) &&
                                    item.As<BEncodedDictionary>()[peerIdKey] is BEncodedString &&
                                    item.As<BEncodedDictionary>().ContainsKey(ipaddressKey) &&
                                    item.As<BEncodedDictionary>()[ipaddressKey] is BEncodedString &&
                                    item.As<BEncodedDictionary>().ContainsKey(portKey) &&
                                    item.As<BEncodedDictionary>()[portKey] is BEncodedNumber)
                                {
                                    peerId = Message.ToPeerId(Encoding.ASCII.GetBytes(item.As<BEncodedDictionary>()[peerIdKey].As<BEncodedString>().Text));
                                    peerIp = item.As<BEncodedDictionary>()[ipaddressKey].As<BEncodedString>().Text;
                                    peerPort = (int)item.As<BEncodedDictionary>()[portKey].As<BEncodedNumber>().Number;

                                    if (IPAddress.TryParse(peerIp, out tmpIpAddress) &&
                                        peerPort >= IPEndPoint.MinPort &&
                                        peerPort <= IPEndPoint.MaxPort)
                                    {
                                        endpoint = new IPEndPoint(tmpIpAddress, (ushort)peerPort);

                                        if (!peers.ContainsKey(endpoint.ToString()))
                                        {
                                            peers.Add(endpoint.ToString(), endpoint);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var data2 = item.Encode();

                                for (int i = 0; i < data2.Length; i += 6)
                                {
                                    endpoint = new IPEndPoint(new IPAddress(IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data2, i))), IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data2, i + 4)));

                                    if (!peers.ContainsKey(endpoint.ToString()))
                                    {
                                        peers.Add(endpoint.ToString(), endpoint);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                message = new AnnounceResponseMessage(faliureReason, interval, complete, incomplete, peers.Values);

                return true;
            }

            return false;
        }
        public override string ToString()
        {
            return "HttpTrackerAnnounceResponseMessage";
        }
    }
}
