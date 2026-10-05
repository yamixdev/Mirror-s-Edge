namespace MEdge.TdGame
{
    public partial class TdPlayerInput
    {
        // aUp is deliberately cleared while a move ignores input. Keep the raw
        // held state so a completed vault can decide whether to chain one jump.
        public bool UnityJumpHeld { get; set; }
    }
}
