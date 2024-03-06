using System;

namespace Clpsplug.I18n.Editor
{
    public static class EnumTool
    {
        public static ReadOnlySpan<T> Enumerate<T>() where T : Enum
        {
            return ((T[])Enum.GetValues(typeof(T)))
                .AsSpan();
        }
    }
}