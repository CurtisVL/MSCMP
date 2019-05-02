using UnityEngine;

namespace MSCMP.Math
{
	internal class Vector3Interpolator
	{
		private Vector3 _current;
		private Vector3 _source;
		private Vector3 _target;

		public Vector3 Current => _current;

		public void SetTarget(Vector3 vec)
		{
			_source = _current;
			_target = vec;
			Evaluate(0.0f);
		}

		public void Teleport(Vector3 vec)
		{
			_current = _source = _target = vec;
		}

		public Vector3 Evaluate(float alpha)
		{
			_current = Vector3.Lerp(_source, _target, alpha);
			return _current;
		}
	}
}
