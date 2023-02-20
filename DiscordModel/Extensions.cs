using System.Collections;

namespace NKDiscordChatWidget.DiscordModel;

public static partial class Extensions
{
    public static bool IsNullOrEmpty( this IEnumerable @this ) =>
        @this == null || @this.IsEmpty();
    private static bool IsEmpty( this IEnumerable @this ) =>
        !@this.GetEnumerator().MoveNext();
}