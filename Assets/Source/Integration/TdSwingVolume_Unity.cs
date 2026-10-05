namespace MEdge.TdGame
{
    using Core;

    public partial class TdSwingVolume
    {
        // Supplied by the Unity adapter. Legacy volumes keep their original shape.
        public float UnityGripHalfLength;

        public Object.Vector UnityClosestGrip(Object.Vector location)
        {
            Object.Vector forward = default, along = default, up = default;
            GetAxes(Rotation, ref forward, ref along, ref up);
            float offset = Dot(location - Location, along);
            if (UnityGripHalfLength > 0f)
                offset = FClamp(offset, -FMax(0f, UnityGripHalfLength - 23f), FMax(0f, UnityGripHalfLength - 23f));
            return Location + along * offset;
        }
    }
}
