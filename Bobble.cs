using UnityEngine;

namespace MapMogul
{
	internal class Bobble : MonoBehaviour
	{
		public Transform target;
		public Vector3 bounce;
		public Vector3 rotate;
		public Vector3 scale;
		public float bounceTime;
		public float rotateTime;
		public float scaleTime;

		bool initialized;
		Vector3 originPos;
		Vector3 originRot;
		Vector3 originScale;

		void Update ()
		{
			if (target == null) return;
			if (!initialized)
			{
				originPos = target.position;
				originRot = target.localEulerAngles;
				originScale = target.localScale;
				initialized = true;
			}

			target.position = originPos + bounce * Mathf.Sin(Time.realtimeSinceStartup / bounceTime);
			target.localEulerAngles = originRot + rotate * Mathf.Sin(Time.realtimeSinceStartup / rotateTime);
			target.localScale = originScale + scale * Mathf.Sin(Time.realtimeSinceStartup / scaleTime);
		}
	}
}
