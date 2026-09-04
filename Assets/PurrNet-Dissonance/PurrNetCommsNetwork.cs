using System;
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

        [Tooltip("When ON, a server-capable instance always brings voice up as Host (local voice + relay), " +
                 "so a client can connect and be heard later without restarting voice (server -> host " +
                 "promotion). When OFF (default) the role follows the NetworkManager's intent: a host runs " +
                 "as Host, a server-only instance is headless. Note: with PurrNet started manually (start " +
                 "flags None), a manual voice start before the instance is actually a host counts as " +
                 "headless — for delayed promotion enable this beforehand.")]
        [SerializeField] private bool startServerAsHost;

        public DissonanceComms comms { get; private set; }

        public PurrNetServer server { get; private set; }
        public PurrNetClient client { get; private set; }

        /// <summary>
        /// The PlayerID used by the host's Dissonance client. On PurrNet hosts localPlayer is
        /// transiently 'Server' before it becomes a numeric ID, so it is captured only once the
        /// local player is ready, and the host-client never sends before then.
        /// </summary>
        internal PlayerID hostClientPlayerId { get; set; }
        internal bool hasHostClientId { get; set; }

        private bool _autoStarted;
        private bool _subscribed;
        private bool _startAllowed;

        private void Awake()
        {
            comms = GetComponent<DissonanceComms>();
            InstanceHandler.RegisterInstance(this);
        }

        private void Start()
        {
            // Auto-start path. Subscribe in Start rather than Awake for one reason: NM assigns its static
            // main in its own Awake and runs at execution order -999, so by any other component's Start
            // main is guaranteed to exist (EnsureSubscribed fails fast if it somehow isn't). Subscribing in
            // Start does NOT guarantee we subscribe before the network comes up: NM auto-starts the network
            // in its own Start and Start order between objects isn't defined, so Connected / player connects
            // may already have happened. EnsureSubscribed handles that with a catch-up on the current state
            // — early events are recovered, not assumed away.
            // With startFlags == None nothing subscribes here; the manual TryRunManually() path does it.
            if (startFlags != StartFlags.None)
                EnsureSubscribed();
        }

        /// <summary>
        /// Idempotently subscribe to the NetworkManager lifecycle events and catch up on any state that
        /// was already reached before we subscribed (e.g. the NetworkManager auto-started the network in
        /// its own Start, before ours ran). Safe to call from the auto-start path (Start) and the manual
        /// path (TryRunManually); the second call is a no-op. The _autoStarted guard makes a caught-up
        /// event and a later real event collapse into a single start.
        /// </summary>
        private void EnsureSubscribed()
        {
            if (_subscribed)
                return;

            var nm = NetworkManager.main;
            if (nm == null)
                throw new InvalidOperationException(
                    "PurrNetCommsNetwork: NetworkManager.main is null at subscribe time. A NetworkManager " +
                    "must exist before this component starts (it runs at execution order -999). Creating the " +
                    "NetworkManager dynamically after this component is not supported.");

            _subscribed = true;

            nm.onServerConnectionState += OnServerConnectionState;
            nm.onClientConnectionState += OnClientConnectionState;
            nm.onLocalPlayerReceivedID += OnLocalPlayerReceivedID;
            nm.onPlayerLeft += OnPlayerLeft;

            if (nm.isServer)
                OnServerConnectionState(ConnectionState.Connected);
            else if (nm.isLocalPlayerReady)
                OnLocalPlayerReceivedID(nm.localPlayer);
        }

        private void OnDestroy()
        {
            InstanceHandler.UnregisterInstance<PurrNetCommsNetwork>();

            if (!_subscribed)
                return;

            var nm = NetworkManager.main;
            if (nm != null)
            {
                nm.onServerConnectionState -= OnServerConnectionState;
                nm.onClientConnectionState -= OnClientConnectionState;
                nm.onLocalPlayerReceivedID -= OnLocalPlayerReceivedID;
                nm.onPlayerLeft -= OnPlayerLeft;
            }
        }

        private void OnServerConnectionState(ConnectionState state)
        {
            var nm = InstanceHandler.NetworkManager;
            if (nm == null)
                return;

            if (state == ConnectionState.Connected)
                TryStart(ResolveServerMode(nm));
            else if (state == ConnectionState.Disconnected)
                StopSession();
        }

        private void OnLocalPlayerReceivedID(PlayerID player)
        {
            var nm = InstanceHandler.NetworkManager;
            if (nm != null && nm.isServer)
            {
                hostClientPlayerId = player;
                hasHostClientId = true;
            }
            else
            {
                TryStart(NetworkMode.Client);
            }
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
                return;

            // On a host the server is independent of the client: if only the client dropped, keep the
            // server (it still relays for the other clients) and drop just the Dissonance client.
            var nm = InstanceHandler.NetworkManager;
            if (nm != null && nm.isServer)
            {
                client?.Disconnect();
                return;
            }

            StopSession();
        }

        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer)
                return;

            var srv = server;
            if (srv != null)
                srv.NotifyPlayerDisconnected(player);
        }

        /// <summary>
        /// Resolve the Dissonance network mode for a server-capable instance. Host when this is a planned
        /// or actual host, or when <see cref="startServerAsHost"/> forces it; otherwise headless.
        /// <para>
        /// <see cref="NetworkManager.isPlannedHost"/> is flag-derived, so it stays stable while a host's
        /// runtime role is transiently server-only during init (the server connects a few frames before
        /// the client) — that is why we never key off the runtime <c>isServerOnly</c>. <c>isHost</c>
        /// additionally covers a manual host whose planned flags are None. Anything else server-capable is
        /// a DedicatedServer; server -> host promotion is the opt-in <see cref="startServerAsHost"/>.
        /// </para>
        /// </summary>
        private NetworkMode ResolveServerMode(NetworkManager nm) =>
            startServerAsHost || nm.isPlannedHost || nm.isHost
                ? NetworkMode.Host
                : NetworkMode.DedicatedServer;

        private void TryStart(NetworkMode mode)
        {
            // The start-flag gate applies only to the FIRST start. Once voice has started (auto or
            // manual), reconnect starts must not re-check the flags: with startFlags == None the gate is
            // always false, which would otherwise stop a client from rejoining voice after a reconnect
            // (the manual first start bypasses the gate via TryRunManually, but reconnect goes through here).
            if (!_startAllowed && !PassesStartFlags())
                return;
            StartSession(mode);
        }

        private void StartSession(NetworkMode mode)
        {
            if (_autoStarted)
                return;

            _startAllowed = true;
            _autoStarted = true;
            hasHostClientId = false;
            PurrNetServer.ClearReceiveQueues();
            PurrNetClient.ClearReceiveQueues();

            switch (mode)
            {
                case NetworkMode.Host:
                    RunAsHost(null, null);
                    break;
                case NetworkMode.DedicatedServer:
                    RunAsDedicatedServer(null);
                    break;
                case NetworkMode.Client:
                    RunAsClient(null);
                    break;
            }
        }

        private void StopSession()
        {
            if (!_autoStarted)
                return;

            _autoStarted = false;
            Stop();

            hasHostClientId = false;
            PurrNetServer.ClearReceiveQueues();
            PurrNetClient.ClearReceiveQueues();
        }

        // Defer to PurrNet's own auto-start check: it evaluates the same StartFlags PurrNet uses to
        // auto-start the network (main editor / clone / client build / server build) and honours the
        // global DisableFlags() switch. Role (host/server/client) is resolved separately in
        // ResolveServerMode / OnLocalPlayerReceivedID, so the gate only answers "should voice start in
        // this environment", exactly what ShouldStart does — no editor/build branching of our own.
        private bool PassesStartFlags()
        {
            return NetworkManager.ShouldStart(startFlags);
        }

        /// <summary>
        /// Start the voice session manually. Use this when startFlags is set to None (built-in auto-start
        /// disabled) to control the moment of the first start yourself. Subscribes to the lifecycle events
        /// on the first call, so reconnect/stop are then handled automatically.
        /// <para>
        /// Call this once the PurrNet role is settled (the NetworkManager is already server or client-only);
        /// calling it before the role is known does nothing, and with startFlags == None there is no
        /// auto-start to pick it up later.
        /// </para>
        /// </summary>
        public void TryRunManually()
        {
            EnsureSubscribed();

            var nm = InstanceHandler.NetworkManager;
            if (nm == null)
                return;

            if (nm.isServer)
                StartSession(ResolveServerMode(nm));
            else if (nm.isClientOnly)
                StartSession(NetworkMode.Client);
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
