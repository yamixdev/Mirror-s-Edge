namespace MEdge.TdGame
{
    using Core;
    using Engine;

    public partial class TdMove_GrabPullUp
    {
        Object.Vector _unityPullUpDestination;
        float _unityPullUpForwardSpeed;
        int _unityPullUpPhase;
        bool _unityPullUpAnimationFinished;

        void BeginUnityPullUp()
        {
            // Imported heave clips contain a backwards root trajectory. Keep their
            // root locked for the visual pose, but align physics to the checked ledge.
            PawnOwner.UseRootMotion(false);
            _unityPullUpAnimationFinished = false;
            _unityPullUpPhase = 1;
            float height = GrabPullUpType == EGrabPullUpType.GPUT_IntoCrouch
                ? PawnOwner.CrouchHeight : PawnOwner.CylinderComponent.CollisionHeight;
            _unityPullUpDestination = FloorOverLedgeLocation;
            _unityPullUpDestination.Z += height + 2f;
            var clearance = PawnOwner.Location;
            clearance.Z = FMax(FloorOverLedgeLocation.Z, PawnOwner.MoveLedgeLocation.Z) + height + 2f;
            float duration = CurrentCustomAnimNode?.AnimSeq?.SequenceLength ?? 1.5f;
            duration = FMax(duration, .3f);
            _unityPullUpForwardSpeed = FMax(VSize(_unityPullUpDestination - clearance) / (duration * .2f), 1f);
            SetPreciseLocation(clearance, EPreciseLocationMode.PLM_Fly,
                FMax(VSize(clearance - PawnOwner.Location) / (duration * .6f), 1f));
        }

        public override void ReachedPreciseLocation()
        {
            if (_unityPullUpPhase == 1)
            {
                _unityPullUpPhase = 2;
                SetPreciseLocation(_unityPullUpDestination, EPreciseLocationMode.PLM_Fly, _unityPullUpForwardSpeed);
            }
            else if (_unityPullUpPhase == 2)
            {
                _unityPullUpPhase = 3;
                PawnOwner.Velocity = default;
                TryFinishUnityPullUp();
            }
        }

        public override void FailedToReachPreciseLocation()
        {
            // If precise alignment fails, leave the move through ordinary falling.
            EndUnityPullUp();
            PawnOwner.SetMove(TdPawn.EMovement.MOVE_Falling);
        }

        void UnityPullUpAnimationFinished()
        {
            _unityPullUpAnimationFinished = true;
            TryFinishUnityPullUp();
        }

        void TryFinishUnityPullUp()
        {
            if (_unityPullUpPhase != 3 || !_unityPullUpAnimationFinished)
                return;
            PawnOwner.UseRootMotion(false);
            PawnOwner.Acceleration = default;
            PawnOwner.SetPhysics(Actor.EPhysics.PHYS_Walking);
            PawnOwner.SetMove(GrabPullUpType == EGrabPullUpType.GPUT_IntoCrouch
                ? TdPawn.EMovement.MOVE_Crouch : TdPawn.EMovement.MOVE_Walking);
        }

        void EndUnityPullUp()
        {
            _unityPullUpPhase = 0;
            _unityPullUpAnimationFinished = false;
            bUsePreciseLocation = false;
            PawnOwner.UseRootMotion(false);
        }
    }
}
