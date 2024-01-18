namespace ResoHelperFP;

public class SessionData
{
    public int ActiveUserCount { get; set; }
    public int UserCount { get; set; }
    public required string AccessLevel { get; set; }

    private bool Equals(SessionData other)
    {
        return ActiveUserCount == other.ActiveUserCount && UserCount == other.UserCount && AccessLevel == other.AccessLevel;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SessionData)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ActiveUserCount, UserCount, AccessLevel);
    }
}