namespace ResoHelperFP;

public class SessionData
{
    public int ActiveUserCount { get; init; }
    public int UserCount { get; init; }
    public required string AccessLevel { get; init; }
    public bool Hidden { get; init; }
    

    private bool Equals(SessionData other)
    {
        return ActiveUserCount == other.ActiveUserCount && UserCount == other.UserCount && AccessLevel == other.AccessLevel && Hidden == other.Hidden;
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
        return HashCode.Combine(ActiveUserCount, UserCount, AccessLevel, Hidden);
    }
}