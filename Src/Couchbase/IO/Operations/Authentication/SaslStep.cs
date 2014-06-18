﻿namespace Couchbase.IO.Operations.Authentication
{
    /// <summary>
    /// Peforms the next step in the in SASL authentication process when required by a AuthenticationContinue message from a <see cref="SaslStart"/> operation.
    /// </summary>
    internal class SaslStep : SaslStart
    {
         public SaslStep(string key, string value, IByteConverter converter) 
            : base(key, value, converter)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStep; }
        }
    }
}
