using Anaglyph.Netcode;
using Unity.Netcode;
using UnityEngine;

namespace Anaglyph.Lasertag
{
	public class MainMenu : MonoBehaviour
	{
		[SerializeField] private GameObject[] onlyVisibleIfConnected = null;
		[SerializeField] private GameObject[] menusOnlyVisibleIfConnected = null;
		[SerializeField] private GameObject fallbackMenuOnDisconnect = null;

		private NetworkManager manager;

		private void Start()
		{
			manager = NetworkManager.Singleton;

			if (manager == null)
				return;

			manager.OnConnectionEvent += OnConnectionEvent;

			UpdateVisibilityOfNetworkOnlyObjects(manager.IsConnectedClient || manager.IsHost);
		}

		private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
		{
			if (NetcodeHelpers.ThisClientConnected(data))
			{
				UpdateVisibilityOfNetworkOnlyObjects(true);
				
			}
			else if (NetcodeHelpers.ThisClientDisconnected(data))
			{
				UpdateVisibilityOfNetworkOnlyObjects(false);

				foreach (GameObject menu in menusOnlyVisibleIfConnected)
				{
					if (menu.activeSelf)
					{
						fallbackMenuOnDisconnect.SetActive(true);
						break;
					}
				}
			}
		}

		private void UpdateVisibilityOfNetworkOnlyObjects(bool visible)
		{
			foreach(var obj in onlyVisibleIfConnected)
				obj.SetActive(visible);
		}
	}
}
