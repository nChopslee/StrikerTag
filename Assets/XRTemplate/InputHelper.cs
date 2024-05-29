using UnityEngine;
using UnityEngine.InputSystem.Utilities;

namespace XRTemplate
{
    public static class InputHelper
    {
        public static bool IsSameHand(UnityEngine.InputSystem.InputDevice inputSystemDevice, UnityEngine.XR.Interaction.Toolkit.XRController xrController)
        {
			return inputSystemDevice.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand) && 
                xrController.inputDevice.characteristics.HasFlag(UnityEngine.XR.InputDeviceCharacteristics.Left);
        }
    }
}
