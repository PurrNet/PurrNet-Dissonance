using System;
using System.Collections.Generic;
using Dissonance.Networking;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Transports;
using UnityEngine;

namespace Dissonance.Integrations.PurrNet
{
    public class PurrNetServer : BaseServer<PurrNetServer, PurrNetClient, PlayerID>
    {
        private readonly PurrNetCommsNetwork _network;

        public PurrNetServer(PurrNetCommsNetwork network)
        {
            _network = network;
        }

        protected override void SendReliable(PlayerID connection, ArraySegment<byte> packet)
        {
            if (NetworkManager.main != null && NetworkManager.main.sceneModule != null)
                if (NetworkManager.main.serverState == ConnectionState.Connected)
                    if (NetworkManager.main.sceneModule.TryGetSceneID(_network.gameObject.scene, out var scene))
                    {
                        var bytes = ByteArrayPool.Rent(packet.Count);
                        Buffer.BlockCopy(packet.Array, packet.Offset, bytes, 0, packet.Count);
                        PurrNetClient.ClientReceiveDataReliable(connection, scene, bytes);
                    }
        }

        protected override void SendUnreliable(PlayerID connection, ArraySegment<byte> packet)
        {
            if (NetworkManager.main != null && NetworkManager.main.sceneModule != null)
                if (NetworkManager.main.serverState == ConnectionState.Connected)
                    if (NetworkManager.main.sceneModule.TryGetSceneID(_network.gameObject.scene, out var scene))
                    {
                        var bytes = ByteArrayPool.Rent(packet.Count);
                        Buffer.BlockCopy(packet.Array, packet.Offset, bytes, 0, packet.Count);
                        PurrNetClient.ClientReceiveDataUnreliable(connection, scene, bytes);
                    }
        }

        private static Dictionary<SceneID, SceneQueue> _receivedData = new();

        [ServerRpc(requireOwnership: false, channel: Channel.ReliableOrdered)]
        public static void ServerReceiveDataReliable(byte[] data, SceneID scene, RPCInfo info = default)
        {
            ReceiveData(info.sender, scene, data);
        }
        [ServerRpc(requireOwnership: false, channel: Channel.Unreliable)]
        public static void ServerReceiveDataUnreliable(byte[] data, SceneID scene, RPCInfo info = default)
        {
            ReceiveData(info.sender, scene, data);
        }

        private static void ReceiveData(PlayerID player, SceneID scene, byte[] data)
        {
            if (!_receivedData.TryGetValue(scene, out var sceneQueue))
            {
                sceneQueue = new SceneQueue();
                sceneQueue.data = new();
                _receivedData[scene] = sceneQueue;
            }

            if (!sceneQueue.data.ContainsKey(player))
            {
                var queue = QueuePool<byte[]>.Instantiate();
                sceneQueue.data[player] = queue;
            }

            sceneQueue.data[player].Enqueue(data);
        }

        protected override void ReadMessages()
        {
            if (NetworkManager.main.sceneModule.TryGetSceneID(_network.gameObject.scene, out var scene) &&
                _receivedData.TryGetValue(scene, out var dataQueue))
            {
                foreach (var (player, queue) in dataQueue.data)
                {
                    while (queue.Count > 0)
                    {
                        var data = queue.Dequeue();
                        base.NetworkReceivedPacket(player, data);
                        ByteArrayPool.Return(data);
                    }
                }
            }
        }

        private struct SceneQueue
        {
            public Dictionary<PlayerID, Queue<byte[]>> data;
        }
    }
}