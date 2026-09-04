# 🎙️ PurrNet Dissonance Integration

A Dissonance networking adapter for PurrNet. It lets Dissonance use PurrNet for
voice-session signalling, peer discovery, voice relaying, and reconnect handling.

The adapter supports PurrNet hosts, dedicated servers, client-only instances, and
an explicit manual startup path.

## 📋 Requirements

- Unity 6
- [PurrNet](https://purrnet.dev/)
- [Dissonance Voice Chat](https://placeholder-software.co.uk/dissonance/)

This repository contains a Unity project and the adapter package at
`Assets/PurrNet-Dissonance`.

## 📦 Install the Package

Add the package through Unity Package Manager with a Git URL:

```text
https://github.com/PurrNet/PurrNet-Dissonance.git?path=/Assets/PurrNet-Dissonance
```

This revision contains the lifecycle fixes described below. PurrNet and Dissonance
must already be installed in the project.

## ⚙️ Basic Setup

1. Add `DissonanceComms` to a scene GameObject.
2. Add `PurrNetCommsNetwork` to that same GameObject.
3. Configure Dissonance as usual: player name, microphone, playback prefab, and
   the required `VoiceBroadcastTrigger` and `VoiceReceiptTrigger` components.
4. Add `PurrNetDissonancePlayer` to the networked player prefab when using
   positional voice. Assign its tracking transform when it differs from the root.

The example scenes in `Assets/Scenes` show a working basic configuration.

## 🚀 Automatic Startup

Automatic startup is enabled by default. `PurrNetCommsNetwork` uses the same
`StartFlags` predicate as PurrNet (`NetworkManager.ShouldStart`), so its behavior
matches the active editor, clone, client-build, and server-build context.

On a server-capable instance, the adapter chooses its Dissonance mode as follows:

- A planned or active PurrNet host starts as `Host`.
- A server-only instance starts as `DedicatedServer`.
- A client-only instance starts as `Client` after it receives its local player ID.

This planned-role check is important: during host startup PurrNet briefly reports a
runtime server-only role before its local client is ready. The adapter must not use
that transient state to start Dissonance as dedicated.

## 🛠️ Manual Startup

To control the first voice-session start yourself, set the adapter's `startFlags`
to `None` and call `TryRunManually()` after PurrNet has reached its intended role:

```csharp
using Dissonance.Integrations.PurrNet;
using UnityEngine;

public sealed class StartVoiceWhenReady : MonoBehaviour
{
    [SerializeField] private PurrNetCommsNetwork voice;

    public void StartVoice()
    {
        voice.TryRunManually();
    }
}
```

`TryRunManually()` also subscribes the adapter to PurrNet lifecycle events. Once
the first session has started, reconnects, shutdown, and departed-peer cleanup are
handled automatically. Calling it while PurrNet is still offline is intentionally a
no-op.

## 🔄 Delayed Server-to-Host Promotion

`startServerAsHost` is off by default. Enable it only when a PurrNet
server-capable instance must bring up a Dissonance host session before its local
PurrNet client exists, then attach that client later without restarting voice.

With the option disabled, normal PurrNet role intent controls the mode. With it
enabled, the server starts Dissonance as `Host` and keeps the relay alive while the
local client arrives or reconnects. This is primarily useful for delayed promotion
flows; ordinary hosts and dedicated servers should leave it disabled.

## 🔌 Lifecycle Behavior

- A host client never sends Dissonance traffic until PurrNet has assigned its final
  local player ID.
- When a player leaves, the Dissonance server removes the corresponding peer.
- Static receive queues are cleared between sessions, preventing old-session
  packets from breaking a reconnect.
- If only the local client disconnects on a host, the Dissonance server continues
  relaying for remaining clients.

## 🧪 Status and Limitations

Version 1.1.0 was tested in Unity 6000.4 with Multiplayer Play Mode 2.0.2,
PurrNet v1.20.0-beta.249, and Dissonance 9.0.9. The exercised topology was one
host and one to three clients, covering connection, disconnect, reconnect, and
server-to-host promotion.

Standalone builds, real headless dedicated-server deployments, and multiple
independent `DissonanceComms` instances have not been tested. In particular,
multi-scene setups using separate Dissonance sessions need explicit validation.

## 💬 Help

- [PurrNet documentation](https://purrnet.dev/docs/integrations/dissonance)
- [PurrNet Discord](https://discord.gg/NP9tP9QxR)
