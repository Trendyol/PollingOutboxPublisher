using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Extensions;

[ExcludeFromCodeCoverage]
public static class ListExtensions
{
    public static bool IsNullOrEmpty<T>(this List<T> list)
    {
        return list == null || list.Count == 0;
    }
}