using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XCode.Common;

/// <summary>
/// 基于键的锁定器
/// </summary>
internal class KeyedLocker<TEntity>
{
    private static Object[] Lockers;
    static KeyedLocker()
    {
        var Length = 8;
        var temp = new Object[Length];
        for (var i = 0; i < Length; i++) temp[i] = new Object();
        Lockers = temp;
    }

    public static Object SharedLock(String key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        var code = key.GetHashCode();
        return Lockers[Math.Abs(code % Lockers.Length)];
    }
}
