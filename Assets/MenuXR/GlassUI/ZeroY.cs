using UnityEngine;

namespace GlassUI
{
    public class ZeroY : MonoBehaviour
    {
		private void LateUpdate()
		{
			transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
		}
	}
}
