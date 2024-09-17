﻿using System;
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
    private static object[] Lockers;
    static KeyedLocker()
    {
        int Length = 8;
        var temp = new object[Length];
        for (int i = 0; i < Length; i++) temp[i] = new object();
        Lockers = temp;
    }

    public static object SharedLock(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        var code = key.GetHashCode();
        return Lockers[Math.Abs(code % Lockers.Length)];
    }
}
