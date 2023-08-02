using System;

namespace LuaDecompilerCore.Utilities;

/// <summary>
/// An interval that represents a range between two integers
/// </summary>
public struct Interval
{
    private int _start;
    private int _range;

    /// <summary>
    /// Creates an interval representing a single value in the range
    /// </summary>
    /// <param name="value">The value that represents the single value in the range</param>
    public Interval(int value)
    {
        _start = value;
        _range = 1;
    }
    
    public Interval(int beginInclusive, int endExclusive)
    {
        _start = beginInclusive;
        _range = endExclusive - beginInclusive;
    }

    /// <summary>
    /// The first value in the interval inclusive
    /// </summary>
    public int Begin => _start;

    /// <summary>
    /// The last value in the interval exclusive
    /// </summary>
    public int End => _start + _range;

    /// <summary>
    /// The number of values contained in the range
    /// </summary>
    public int Count => _range;

    /// <summary>
    /// Returns if the value is included in the range
    /// </summary>
    /// <param name="value">The value to test</param>
    /// <returns>True if the value is included in the range</returns>
    public bool Contains(int value) => value >= Begin && value < End;

    /// <summary>
    /// Extends the interval to contain the given value
    /// </summary>
    /// <param name="value">The value to extend the interval to include</param>
    public void AddToRange(int value)
    {
        if (_range == 0)
        {
            _start = value;
            _range = 1;
        }
        else if (value < _start)
        {
            _range += _start - value;
            _start = value;
        }
        else if (value > _start + _range)
        {
            _range = value - _start + 1;
        }
    }

    /// <summary>
    /// Unions this interval with another such that the interval contains all the values of both this range and the
    /// other range
    /// </summary>
    /// <param name="interval">The interval to union with</param>
    public Interval UnionWith(Interval interval)
    {
        if (_range == 0)
            return interval;
        if (interval._range == 0)
            return this;
        
        Interval result;
        result._start = Math.Min(Begin, interval.Begin);
        result._range = Math.Max(End, interval.End) - result._start;
        return result;
    }

    public override string ToString()
    {
        if (_range == 0)
            return "empty";
        return $"[{_start} {_start + _range})";
    }
}