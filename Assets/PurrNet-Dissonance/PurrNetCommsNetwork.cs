using Dissonance.Networking;
using PurrNet;
using PurrNet.Logging;
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

        /// <summary>
        /// The PlayerID used by the host's Dissonance client during its initial handshake.
        /// On PurrNet hosts, localPlayer can change from 'Server' to a numeric ID after init,
        /// but Dissonance's server stores the original ID, so we must remember it for loopback.
        /// </summary>
        internal PlayerID hostClientPlayerId { get; set; }
        internal bool hasHostClientId { get; set; }

        private void Awake()
        {
            comms = GetComponent<DissonanceComms>();
            InstanceHandler.RegisterInstance(this);

            if (startFlags == StartFlags.None)
            {
                return;
            }
            
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

        /// <summary>
        /// Try starting PurrNetCommsNetwork manually. Use this if your startFlags is set to None.
        /// </summary>
        public void TryRunManually()
        {
            if (InstanceHandler.NetworkManager.isHost)
                RunAsHost(null, null);
            else if (InstanceHandler.NetworkManager.isServerOnly)
                RunAsDedicatedServer(null);
            else if (InstanceHandler.NetworkManager.isClientOnly)
                RunAsClient(null);
        }

        private void OnConnectionState(ConnectionState connectionState)
        {
            //PurrLogger.Log($"[Dissonance-Comms] OnConnectionState: {connectionState}, isHost={InstanceHandler.NetworkManager.isHost}, isServer={InstanceHandler.NetworkManager.isServer}, isClient={InstanceHandler.NetworkManager.isClient}, localPlayer={NetworkManager.main?.localPlayer}");
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