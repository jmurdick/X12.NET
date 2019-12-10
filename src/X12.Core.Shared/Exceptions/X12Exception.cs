using System;
using System.Collections.Generic;
using System.Text;

namespace X12.Core.Shared.Exceptions
{
    public class X12Exception : Exception
    {
        public X12Exception() { }

        public X12Exception(string message) : base(message) { }

        public X12Exception(string message, Exception innerException) : base(message, innerException) { }
    }
}
