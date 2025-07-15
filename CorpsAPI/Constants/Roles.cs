namespace CorpsAPI.Constants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string EventManager = "Event Manager";
        public const string Staff = "Staff";
        public const string User = "User";

        public static readonly string[] AllRoles = [Admin, EventManager, Staff, User];
    }
}
