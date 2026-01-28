using UnityEngine;

namespace MapMogul
{
	public static class CameraFadeUtil
	{
		private static GameObject overlay;

		public static void FadeToBlack(Camera cam)
		{
			if (overlay != null) return;

			overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Object.Destroy(overlay.GetComponent<Collider>());

			overlay.name = "CameraBlackout";
			overlay.transform.SetParent(cam.transform, false);

			overlay.transform.localPosition = new Vector3(0, 0, 0.5f);
			overlay.transform.localRotation = Quaternion.identity;
			overlay.transform.localScale = new Vector3(100f, 100f, 1f);

			var mat = new Material(Shader.Find("Unlit/Color"));
			mat.color = Color.black;
			overlay.GetComponent<MeshRenderer>().material = mat;
		}

		public static void FadeFromBlack()
		{
			if (overlay != null)
			{
				Object.Destroy(overlay);
				overlay = null;
			}
		}
	}
}