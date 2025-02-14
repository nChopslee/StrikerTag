using Anaglyph.SharedSpaces;
using Anaglyph.XRTemplate;
using Anaglyph.XRTemplate.SharedSpaces;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Anaglyph.Lasertag
{
	public class LasertagManager : NetworkBehaviour
	{
		[Serializable]
		public enum ColocationMethod
		{
			Automatic = 0,
			TrackedKeyboard = 1,
		}

		public static LasertagManager Current;
		private NetworkVariable<ColocationMethod> colocationMethodSync = new(0);
		public void SetColocationMethod(ColocationMethod colocationMethod)
			=> colocationMethodSync.Value = colocationMethod;

		private void Start()
		{
			if (Current == null)
				Current = this;
		}

		public override void OnNetworkSpawn()
		{
			switch(colocationMethodSync.Value)
			{
				case ColocationMethod.Automatic:
					Colocation.SetActiveColocator(MetaAnchorColocator.Instance);
					break;

				case ColocationMethod.TrackedKeyboard:
					Colocation.SetActiveColocator(MetaTrackableColocator.Instance);
					break;
			}

			Colocation.ActiveColocator.Colocate();
		}

		public override void OnNetworkDespawn()
		{
			Colocation.ActiveColocator.StopColocation();

			MainXROrigin.Instance.transform.position = Vector3.zero;
			MainXROrigin.Instance.transform.rotation = Quaternion.identity;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
		}
	}
}
