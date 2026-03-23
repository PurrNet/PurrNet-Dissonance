using Dissonance.Editor;
using Dissonance.Integrations.PurrNet;
using PurrNet;
using UnityEditor;

namespace DissonanceVoip.PurrNet.Editor
{
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
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawDefaultInspector();
        }
    }
}