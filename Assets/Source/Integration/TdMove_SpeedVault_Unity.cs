namespace MEdge.TdGame
{
    using Engine;

    public partial class TdMove_SpeedVault
    {
        bool TryUnityVaultJump()
        {
            if (bVaultOnto || VaultState != 3 ||
                (PawnOwner.MovementState != TdPawn.EMovement.MOVE_VaultOver &&
                 PawnOwner.MovementState != TdPawn.EMovement.MOVE_SpeedVaulting))
                return false;
            var type = VaultTypes[ActiveVaultType];
            if (!type.bLeftHandIK || type.bRightHandIK ||
                PawnOwner.Controller is not TdPlayerController controller ||
                controller.PlayerInput is not TdPlayerInput { UnityJumpHeld: true })
                return false;

            // The obstacle is now behind the pawn. Stop precise vault alignment
            // before the impulse: falling physics owns the entire flight.
            var velocity = SavedVelocity;
            var jump = (TdMove_Jump)PawnOwner.Moves[(int)TdPawn.EMovement.MOVE_Jump];
            velocity.Z = jump.BaseJumpZ * PawnOwner.GetMobilityMultiplier();
            PawnOwner.SetMove(TdPawn.EMovement.MOVE_Falling);
            PawnOwner.StopCustomAnim(TdPawn.CustomNodeType.CNT_FullBody, .15f);
            PawnOwner.Moves[(int)TdPawn.EMovement.MOVE_Falling].PlayMoveAnim(
                TdPawn.CustomNodeType.CNT_FullBody_Dir, "JumpSlow", 1f, .15f, .2f);
            PawnOwner.Velocity = velocity;
            PawnOwner.Acceleration = default;
            PawnOwner.LastJumpLocation = PawnOwner.Location;
            PawnOwner.NotifyJump();
            controller.AccelerationTime = .2f;
            return true;
        }
    }
}
