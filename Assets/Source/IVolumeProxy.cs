namespace MEdge.Source
{
	using Core;
	using Engine;
	using UnityEngine;



	public abstract class VolumeProxy : MonoBehaviour
	{
		public abstract MEdge.Engine.Volume UnrealVolume{ get; }
	}



	public abstract class VolumeProxy<T> : VolumeProxy where T : Volume, new()
	{
		T _unrealInstance;
		UWorld _registeredWorld;



		public override Volume UnrealVolume
		{
			get
			{
				if( _unrealInstance != null )
					return _unrealInstance;
				
				_registeredWorld = UWorld.Instance;
				_unrealInstance = new T
				{
					Name = $"{typeof(T).Name}_{gameObject.name}",
				};
				SyncVolumeOuter();
				_registeredWorld.LowFrequencyUpdate.Add( SyncVolumeOuter );
				return _unrealInstance;
			}
		}



		protected void SyncVolumeOuter()
		{
			// Of course I have to do this kind of bullshit as unity doesn't handle property in the editor
			// Otherwise I would just sync when property changes ...
			_unrealInstance.Rotation = this.transform.rotation.ToUnrealRot();
			_unrealInstance.Location = this.transform.position.ToUnrealPos();
			SyncVolume( _unrealInstance );
		}



		protected abstract void SyncVolume( T volume );

		protected virtual void OnDestroy()
		{
			// Use the world that owns the callback; Instance could create a world during shutdown.
			if( _registeredWorld != null )
				_registeredWorld.RemoveLowFrequencyUpdate( SyncVolumeOuter );
			_registeredWorld = null;
		}
	}
}
