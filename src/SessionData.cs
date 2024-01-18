namespace ResoHelperFP;

public class SessionData
{
    public int ActiveUserCount { get; set; }
    public int UserCount { get; set; }
    public required string AccessLevel { get; set; }

    private sealed class SessionDataEqualityComparer : IEqualityComparer<SessionData>
    {
        public bool Equals(SessionData? x, SessionData? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.ActiveUserCount == y.ActiveUserCount && x.UserCount == y.UserCount && x.AccessLevel == y.AccessLevel;
        }

        public int GetHashCode(SessionData obj)
        {
            return HashCode.Combine(obj.ActiveUserCount, obj.UserCount, obj.AccessLevel);
        }
    }

    public static IEqualityComparer<SessionData> SessionDataComparer { get; } = new SessionDataEqualityComparer();
}