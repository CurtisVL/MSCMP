using UnityEngine;

namespace MSCMP.Math
{
	internal class TransformInterpolator
	{
		private readonly QuaternionInterpolator _rotation = new QuaternionInterpolator();
		private readonly Vector3Interpolator _position = new Vector3Interpolator();

		public Vector3 CurrentPosition => _position.Current;

		public Quaternion CurrentRotation => _rotation.Current;

		public void Teleport(Vector3 pos, Quaternion rot)
		{
			_position.Teleport(pos);
			_rotation.Teleport(rot);
		}

		public void SetTarget(Vector3 pos, Quaternion rot)
		{
			_position.SetTarget(pos);
			_rotation.SetTarget(rot);
		}

		public void Evaluate(ref Vector3 pos, ref Quaternion rot, float alpha)
		{
			pos = _position.Evaluate(alpha);
			rot = _rotation.Evaluate(alpha);
		}

		public void Evaluate(float alpha)
		{
			_position.Evaluate(alpha);
			_rotation.Evaluate(alpha);
		}
	}
}
