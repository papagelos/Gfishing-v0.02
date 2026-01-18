using System.Collections.Generic;
using UnityEngine;

public static class UiSorting
{
    static readonly Dictionary<UiCanvasCategory,int> _top = new();

    static int Base(UiCanvasCategory cat) => (int)cat;

    public static int Next(UiCanvasCategory cat)
    {
        if (!_top.TryGetValue(cat, out var cur)) cur = Base(cat);
        cur += 1;
        _top[cat] = cur;
        return cur;
    }

    public static void BringToFront(Canvas c, UiCanvasCategory cat, int offset = 0)
    {
        if (!c) return;
        c.overrideSorting = true;
        c.sortingOrder = Next(cat) + offset;
    }
}
