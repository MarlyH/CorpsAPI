namespace CorpsAPI.Services
{
    public static class RefreshTokenStore
    {
        // key = jti
        // value = sub (userId)
        // TODO: make thread-safe and move to memory cache
        public static Dictionary<string, string> RefreshTokens = new();
    }
}
