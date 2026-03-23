using Dissonance.Editor;
using Dissonance.Integrations.PurrNet;
using PurrNet;
using UnityEditor;

[CustomEditor(typeof(PurrNetCommsNetwork))]
public class CustomCommsNetworkEditor
    : BaseDissonnanceCommsNetworkEditor<
        PurrNetCommsNetwork,
        PurrNetServer,
        PurrNetClient,
        PlayerID,
        object,
        object
    >
{
}
