#if UNITY_5_3_OR_NEWER

using System;
using UnityEngine;
using MikaProtocol;
using Utils;

namespace MikaNetwork
{
    public class NetworkManager : SingletonMonoBehaviour<NetworkManager>
    {
        private readonly MikaClient _client = new();
        private readonly ServerPacketManager _packetManager = new();

        protected override void Awake()
        {
            base.Awake(); // Singleton 등록

            Debug.Log("NetworkManager is Awaken");
        }

        public async void Start()
        {
            _client.PacketReceived += (session, data) =>
            {
                _packetManager.OnRecvPacket(session, data, job =>
                {
                    NetworkMessageQueue.Instance.Push(job);
                });

                return default;
            };

            await _client.ConnectAsync("127.0.0.1", 10050);
        }

        void Update()
        {
            NetworkMessageQueue.Instance.Flush();
        }

        public void Send<T>(T packet) where T : IPacket
        {
            _client.Send(packet);
        }
    }
}

#endif