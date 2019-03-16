using System;

namespace EngineLightRelit
{
    //Not a MonoBehaviour!
    internal static class Utils
    {
        //As the name says, checks if the user is using the IVA camera
        //Added the OR just in case that other camera mode is also IVA!
        public static bool isIVA()
        {
             return CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA || CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal;
        }

		public static float clampValue(float value, float min, float max, string descriptor="")
		{
			if (min > max)
				throw new ArgumentException("maximum must exceed minimum");

			float orig = value;
			if (value < min)
				value = min;
			else if (value > max)
				value = max;

			if (descriptor.Length > 0 && value != orig)
				log("clamping " + descriptor + " to " + value);	

			return value;
		}

		public static void log(string text)
		{
			UnityEngine.Debug.Log("[EngineLight] " + text);
		}
    }
}
