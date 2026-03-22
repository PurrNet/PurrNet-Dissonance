using PurrNet;
using PurrNet.Logging;
using UnityEngine;
using System;
#if UNITY_EDITOR
using PurrNet.Utils;
#endif

namespace Dissonance.Integrations.PurrNet
{
    public class PurrNetDissonancePlayer : NetworkIdentity, IDissonancePlayer
    {
        [SerializeField] private Transform trackingTransform;

#if UNITY_EDITOR
        [SerializeField, PurrReadOnly] private string dissonanceId_Debug;
        [SerializeField, PurrReadOnly] private bool isTracking_Debug;
#endif

        private readonly SyncVar<string> _playerId = new("", ownerAuth: false);
        private Transform _transform;
        private DissonanceComms _dissonanceComms;
        public DissonanceComms dissonanceComms => _dissonanceComms ??= FindObjectOfType<DissonanceComms>();

        // IDissonancePlayer implementation
        public string PlayerId => _playerId.value;
        public Vector3 Position => _transform.position;
        public Quaternion Rotation => _transform.rotation;
        public NetworkPlayerType Type => isOwner ? NetworkPlayerType.Local : NetworkPlayerType.Remote;
        public bool IsTracking { get; private set; }

        private void Awake()
        {
            _transform = trackingTransform ?? transform;
            _playerId.onChanged += OnPlayerIdChanged;
        }

        private void Start()
        {
            if (isOwner && NetworkManager.main.isServer)
            {
                // We're the host (working for now)
                SetPlayerIdFromDissonance();
            }
            else if (isOwner)
            {
                // We're a client (WIP 28/03/2025)
                ServerRpcSetPlayerId(GetDissonanceLocalPlayerName());
            }
        }
        public void SetTrackingTransform(Transform newTransform)
        {
            if (newTransform == null)
            {
                PurrLogger.LogError("Tracking transform cannot be null.");
                return;
            }
            trackingTransform = newTransform;
            _transform = newTransform;
            PurrLogger.Log($"Tracking transform set to: {_transform.name}");
        }
        // "name" is the player ID in Dissonance, which is set by the DissonanceComms component 
        private string GetDissonanceLocalPlayerName()
        {
            var comms = dissonanceComms;
            if (comms == null)
                return owner.Value.id.ToString();

            if (string.IsNullOrEmpty(comms.LocalPlayerName))
                comms.LocalPlayerName = owner.Value.id.ToString();

            return comms.LocalPlayerName;
        }

        private void SetPlayerIdFromDissonance()
        {
            if (!isOwner)
                return;

            var comms = dissonanceComms;
            if (comms == null)
                return;

            if (string.IsNullOrEmpty(comms.LocalPlayerName))
                comms.LocalPlayerName = owner.Value.id.ToString();

            _playerId.value = comms.LocalPlayerName;
        }

        [ServerRpc(requireOwnership: true)]
        private void ServerRpcSetPlayerId(string id)
        {
            _playerId.value = id;
            PurrLogger.Log($"Server set player ID: {id}");
        }

        private void OnEnable()
        {
            ManageTracking(true);
        }

        private void OnDisable()
        {
            ManageTracking(false);
        }

        private void OnPlayerIdChanged(string newId)
        {
            if (IsTracking)
                ManageTracking(false);

#if UNITY_EDITOR
            dissonanceId_Debug = newId;
#endif

            if (!string.IsNullOrEmpty(newId))
                ManageTracking(true);
        }

        private void ManageTracking(bool track)
        {
            if (IsTracking == track)
                return;

            if (track && string.IsNullOrEmpty(_playerId.value))
                return;

            var comms = dissonanceComms;
            if (comms == null)
                return;

            try
            {
                if (track)
                {
                    comms.TrackPlayerPosition(this);
                    PurrLogger.Log($"Started tracking player: {_playerId.value}");
                }
                else
                {
                    comms.StopTracking(this);
                }

                IsTracking = track;

#if UNITY_EDITOR
                isTracking_Debug = IsTracking;
#endif
            }
            catch (Exception ex)
            {
                PurrLogger.LogError($"Error in positional tracking: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            ManageTracking(false);
            _playerId.onChanged -= OnPlayerIdChanged;
            base.OnDestroy();
        }
    }
}