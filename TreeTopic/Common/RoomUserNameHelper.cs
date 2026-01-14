using TreeTopic.Models;

namespace TreeTopic.Common;

public static class RoomUserNameHelper
{
    public const string DefaultUserToken = "{defaultuser}";

    public static bool IsSyncName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (string.Equals(name.Trim(), DefaultUserToken, StringComparison.Ordinal))
            return true;

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
                return false;
        }

        return true;
    }

    public static string ResolveDisplayName(RoomUser? roomUser)
    {
        if (roomUser == null)
            return string.Empty;

        var mainName = roomUser.ApplicationUser?.DisplayName
            ?? roomUser.ApplicationUser?.UserName
            ?? string.Empty;

        if (roomUser.UseMainName || IsSyncName(roomUser.Name))
            return mainName;

        if (!string.IsNullOrWhiteSpace(roomUser.Name))
            return roomUser.Name;

        return mainName;
    }
}
