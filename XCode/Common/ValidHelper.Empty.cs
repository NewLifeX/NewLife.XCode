using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XCode;

public partial class ValidHelper
{
#if !NET461_OR_GREATER
    private static class EmptyArray<T>
    {
        public static readonly T[] Value = new T[0];
    }
#endif

    private static T[] Empty<T>()
    {
#if NET461_OR_GREATER

        return Array.Empty<T>();
#else
        return EmptyArray<T>.Value;
#endif
    }
}
