namespace MEdge.TdGame
{
    using Core;
    using Engine;

    public partial class TdMove_Swing
    {
        void UnityUpdateHandGrips()
        {
            Update(PawnOwner.LeftHandWorldIKController, "LeftHandMiddle1", -20f, .62f, 1.06190f);
            Update(PawnOwner.RightHandWorldIKController, "RightHandMiddle1", 20f, .04f, .38023670f);
        }

        void Update(SkelControlLimb controller, name palm, float side, float release, float contact)
        {
            if (controller == null) return;
            controller.UnityPalmBone = palm;
            controller.UnityPreserveAnimatedTwist = true;
            controller.UnityFollowAnimatedGrip = bIsShimmying;
            controller.UnityGripAxis = BarDirection;
            controller.UnityGripHalfLength = Volume.UnityGripHalfLength > 0f ? FMax(0f, Volume.UnityGripHalfLength - 3f) : BIG_NUMBER;
            // During a step the animation supplies lateral hand motion. At rest
            // the two contacts are centred on the physical grip.
            var centre = bIsShimmying ? Volume.Location : SwingLocation + BarDirection * side;
            controller.EffectorLocation = centre + vect(0f, 0f, 2f);
            controller.EffectorLocationSpace = SkelControlBase.EBoneControlSpace.BCS_WorldSpace;
            float strength = 1f;
            if (bIsShimmying && CustomAnimNode != null)
            {
                // These windows belong to the imported SwingStrafe clip; using
                // clip time also keeps the same poses when played in reverse.
                float t = CustomAnimNode.CurrentTime;
                if (t > release && t < contact)
                    strength = 1f - FClamp(FMin(t-release, contact-t) / .07f, 0f, 1f);
            }
            controller.SetSkelControlStrength(strength, .04f);
        }

        void UnityResetHandGrips()
        {
            Reset(PawnOwner.LeftHandWorldIKController, "LeftHand");
            Reset(PawnOwner.RightHandWorldIKController, "RightHand");
        }

        void Reset(SkelControlLimb controller, name hand)
        {
            if (controller == null) return;
            // Preserve the solved wrist while IK fades out. Leaving a palm goal
            // in a wrist controller would move the hand by the palm offset again.
            controller.EffectorLocation = PawnOwner.Mesh1p.GetBoneLocation(hand);
            controller.UnityPalmBone = NAME_None;
            controller.UnityPreserveAnimatedTwist = false;
            controller.UnityFollowAnimatedGrip = false;
        }
    }
}
