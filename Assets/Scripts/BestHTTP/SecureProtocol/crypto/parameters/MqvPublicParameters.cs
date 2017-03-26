#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)

using System;

namespace Org.BouncyCastle.Crypto.Parameters
{
	public class MqvPublicParameters
		: ICipherParameters
	{
		private readonly ECPublicKeyParameters staticPublicKey;
		private readonly ECPublicKeyParameters ephemeralPublicKey;

		public MqvPublicParameters(
			ECPublicKeyParameters	staticPublicKey,
			ECPublicKeyParameters	ephemeralPublicKey)
		{
			this.staticPublicKey = staticPublicKey;
			this.ephemeralPublicKey = ephemeralPublicKey;
		}

		public ECPublicKeyParameters StaticPublicKey
		{
			get { return staticPublicKey; }
		}

		public ECPublicKeyParameters EphemeralPublicKey
		{
			get { return ephemeralPublicKey; }
		}
	}
}

#endif
