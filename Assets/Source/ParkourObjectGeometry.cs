using UnityEngine;

namespace MEdge.Source
{
    // Geometry is baked by the editor into ordinary children and colliders.
    // No procedural allocations or Unity physics callbacks are needed in play mode.
    [DisallowMultipleComponent]
    public class ParkourObjectGeometry : MonoBehaviour
    {
        public enum ObjectKind { BalanceBeam, SwingBar, Ladder, Zipline }
        [HideInInspector] public ObjectKind Kind;
        [Min(.02f), Tooltip("Толщина балки, перекладины или троса, в метрах.")]
        public float Thickness = .18f;
        [Min(.3f), Tooltip("Длина перекладины для раскачивания, в метрах.")]
        public float BarLength = 3f;
        [Min(.3f), Tooltip("Ширина лестницы, в метрах.")]
        public float LadderWidth = .5f;
        public Material Material;
        [HideInInspector] public Transform GeneratedGeometry;
    }
}
