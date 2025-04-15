namespace CorpsAPI.Services
{
    public static class RefreshTokenStore
    {
        // key = jti
        // value = userId
        public static Dictionary<string, string> Tokens = new();
    }
}
