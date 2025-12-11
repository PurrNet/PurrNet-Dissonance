using Dissonance.Networking;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

namespace Dissonance.Integrations.PurrNet
{
    public class PurrNetCommsNetwork : BaseCommsNetwork<PurrNetServer, PurrNetClient, PlayerID, object, object>
    {
        [Header("Auto Start Settings")]
        [Tooltip("The flags to determine when the Dissonance Comms should automatically start.")]
        [SerializeField] private StartFlags startFlags = StartFlags.ServerBuild | StartFlags.ClientBuild | StartFlags.Clone | StartFlags.Editor;

        public DissonanceComms comms { get; private set; }

        public PurrNetServer server { get; private set; }
        public PurrNetClient client { get; private set; }

        private void Awake()
        {
            comms = GetComponent<DissonanceComms>();
            InstanceHandler.RegisterInstance(this);
#if UNITY_SERVER
            NetworkManager.main.onServerConnectionState += OnConnectionState;
#else
            NetworkManager.main.onClientConnectionState += OnConnectionState;
#endif
        }

        private void OnDestroy()
        {
            InstanceHandler.UnregisterInstance<PurrNetCommsNetwork>();
            NetworkManager.main.onClientConnectionState -= OnConnectionState;
            NetworkManager.main.onServerConnectionState -= OnConnectionState;
        }

        private void OnConnectionState(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.Connected)
            {
#if UNITY_EDITOR
                if (InstanceHandler.NetworkManager.isHost && startFlags.HasFlag(StartFlags.Editor))
                    RunAsHost(null, null);
                else if (InstanceHandler.NetworkManager.isServerOnly && startFlags.HasFlag(StartFlags.Editor))
                    RunAsDedicatedServer(null);
                else if (InstanceHandler.NetworkManager.isClientOnly && startFlags.HasFlag(StartFlags.Editor))
                    RunAsClient(null);
#else
                if (InstanceHandler.NetworkManager.isHost && startFlags.HasFlag(StartFlags.ClientBuild))
                    RunAsHost(null, null);
                else if (InstanceHandler.NetworkManager.isServerOnly && startFlags.HasFlag(StartFlags.ServerBuild))
                    RunAsDedicatedServer(null);
                else if (InstanceHandler.NetworkManager.isClientOnly && startFlags.HasFlag(StartFlags.ClientBuild))
                    RunAsClient(null);
#endif
            }
            else
            {
                Stop();
            }
        }

        protected override PurrNetServer CreateServer(object connectionParameters)
        {
            server = new PurrNetServer(this);
            return server;
        }

        protected override PurrNetClient CreateClient(object connectionParameters)
        {
            client = new PurrNetClient(this);
            return client;
        }

        protected override void Initialize()
        {
            // Initialization for PurrNet-specific setups, if required
        }
    }
}