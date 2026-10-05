namespace MEdge.Source
{
	using TdGame;
	using UnityEngine;



	public class UnityTdSwingVolume : VolumeProxy<TdSwingVolume>
	{
		public bool bSnapToCenter;
		public bool bThickGrip;

		void OnEnable()
		{
			if(Application.isPlaying)
				EnsureGripReach();
		}

		void EnsureGripReach()
		{
			// SwingJump aims about 1.2m behind the bar. Existing generated objects
			// must also expose that approach zone, without requiring recreation.
			if(GetComponent<ParkourObjectGeometry>() && TryGetComponent<BoxCollider>(out var trigger) && trigger.isTrigger)
			{
				var size = trigger.size;
				size.z = Mathf.Max(size.z, 2.6f / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.z)));
				trigger.size = size;
			}
		}



		protected override void SyncVolume(TdSwingVolume volume)
		{
			EnsureGripReach();
			// Of course I have to do this kind of bullshit as unity doesn't handle property in the editor
			// Otherwise I would just sync when property changes ...
			volume.bThickGrip = bThickGrip;
			volume.bSnapToCenter = bSnapToCenter;
			var geometry = GetComponent<ParkourObjectGeometry>();
			var trigger = GetComponent<BoxCollider>();
			volume.UnityGripHalfLength = (geometry ? geometry.BarLength : trigger ? trigger.size.x : 0f) * Mathf.Abs(transform.lossyScale.x) * 50f;
		}



		void OnDrawGizmos()
		{
			Gizmos.color = Color.magenta;
			
			var subdiv = 16;
			
			var thisPos = transform.position;
			var p = transform.forward * 0.5f;
			var prevP = p;
			Quaternion rotOffset = Quaternion.AngleAxis( 180f / subdiv, transform.right );
			for( int i = 0; i < subdiv; i++ )
			{
				p = rotOffset * prevP;
				Gizmos.DrawLine( thisPos+p, thisPos+prevP );
				Gizmos.DrawLine( thisPos+p*0.8f, thisPos+prevP*0.8f );
				prevP = p;
			}
			
			Gizmos.DrawLine( thisPos+transform.right*0.2f, thisPos-transform.right*0.2f );
			Gizmos.DrawLine( thisPos+transform.forward*0.2f, thisPos-transform.forward*0.2f );
		}
	}
}
