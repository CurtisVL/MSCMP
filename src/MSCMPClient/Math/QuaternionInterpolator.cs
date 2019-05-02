using UnityEngine;

namespace MSCMP.Math
{
	internal class QuaternionInterpolator
	{
		private Quaternion _current;
		private Quaternion _source;
		private Quaternion _target;

		public Quaternion Current => _current;

		public void SetTarget(Quaternion quat)
		{
			_source = _current;
			_target = quat;
			Evaluate(0.0f);
		}

		public void Teleport(Quaternion quat)
		{
			_current = _source = _target = quat;
		}

		public Quaternion Evaluate(float alpha)
		{
			_current = Quaternion.Slerp(_source, _target, alpha);
			return _current;
		}
	}
}
