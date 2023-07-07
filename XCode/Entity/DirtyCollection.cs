using System.Collections;

namespace XCode;

/// <summary>脏属性集合</summary>
/// <remarks>
/// 脏数据需要并行高性能，要节省内存，允许重复。
/// 普通集合加锁成本太高，并发集合内存消耗太大，并发字典只有一两项的时候也要占用7.9k内存。
/// </remarks>
[Serializable]
public class DirtyCollection : IEnumerable<String>
{
    private String[] _keys = new String[8];
    private Object[] _values = new Object[8];

    /// <summary>数据长度</summary>
    /// <remarks>
    /// 添加时，先抢位置，再赋值。
    /// 即使删除，也不会减少长度，仅仅是把数据置空。
    /// 该设计浪费了一些空间，但是避免了并发冲突，简化了代码设计，并且极少用到删除。
    /// </remarks>
    private Int32 _length;

    private Int32 _count;
    /// <summary>个数</summary>
    public Int32 Count => _count;

    /// <summary>获取或设置与指定的属性是否有脏数据。</summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public Boolean this[String item]
    {
        get => Contains(item);
        set
        {
            if (value)
                Add(item, null);
            else
                Remove(item);
        }
    }

    /// <summary>添加脏数据，并记录旧值</summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public Boolean Add(String key, Object value)
    {
        if (Contains(key)) return false;

        // 抢位置
        var n = Interlocked.Increment(ref _length);

        var ms = _keys;
        while (ms.Length < _length)
        {
            // 扩容
            var arr = new String[ms.Length * 2];
            Array.Copy(ms, arr, ms.Length);
            if (Interlocked.CompareExchange(ref _keys, arr, ms) == ms)
            {
                var arr2 = new Object[arr.Length];
                Array.Copy(_values, arr2, _values.Length);
                _values = arr2;
                break;
            }

            ms = _keys;
        }

        _keys[n - 1] = key;
        _values[n - 1] = value;

        Interlocked.Increment(ref _count);

        return true;
    }

    private void Remove(String item)
    {
        var len = _length;
        var ms = _keys;
        if (len > ms.Length) len = ms.Length;
        for (var i = 0; i < len; i++)
        {
            if (ms[i] == item)
            {
                ms[i] = null;

                Interlocked.Decrement(ref _count);
            }
        }
    }

    private Boolean Contains(String item)
    {
        var len = _length;
        var ms = _keys;
        if (len > ms.Length) len = ms.Length;
        for (var i = 0; i < len; i++)
        {
            if (ms[i] == item) return true;
        }

        return false;
    }

    /// <summary>清空</summary>
    public void Clear()
    {
        _length = 0;
        _count = 0;
        Array.Clear(_keys, 0, _keys.Length);
    }

    /// <summary>枚举迭代</summary>
    /// <returns></returns>
    public IEnumerator<String> GetEnumerator()
    {
        var len = _length;
        var ms = _keys;
        if (len > ms.Length) len = ms.Length;
        for (var i = 0; i < len; i++)
        {
            if (ms[i] != null) yield return ms[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>获取字典枚举</summary>
    /// <returns></returns>
    public IEnumerator<KeyValuePair<String, Object>> GetDictionary()
    {
        var len = _length;
        var ms = _keys;
        if (len > ms.Length) len = ms.Length;
        for (var i = 0; i < len; i++)
        {
            if (ms[i] != null) yield return new KeyValuePair<String, Object>(ms[i], _values[i]);
        }
    }
}