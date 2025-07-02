namespace CorpsAPI.Constants
{
    public static class ErrorMessages
    {
        // generic errors
        public const string InvalidRequest = "Invalid request.";
        public const string InternalServerError = "Server error. Please try again later.";
        public const string AccountNotEligible = "This account is not eligible to perform the requested action.";
        public const string EmailConfirmationFailed = "Email confirmation failed.";

        // user errors
        public const string InvalidCredentials = "Invalid login credentials.";
        public const string EmailNotConfirmed = "Email not confirmed. Please check your inbox/spam folders for the verification link.";
        public const string EmailConfirmationExpired = "Your confirmation email has expired. Would you like to request a new one?";
        public const string ResendEmailRateLimited = "Sorry, you can only send one confirmation email every five minutes.";
        public const string ExpiredOtp = "Code has expired.";
        public const string IncorrectOtp = "The code you entered is incorrect. Please try again.";
        public const string EmailAlreadyConfirmed = "Email is already confirmed";
        public const string EmailTaken = "This email address is already taken.";
        public const string UserNameTaken = "This user name is already taken.";

        // promote role errors
        public const string RoleNotExist = "Role does not exist.";
        public const string AddToRoleFailed = "Failed to add to new role.";
        public const string RemoveFromExistingRoles = "Failed to remove from existing roles.";
        public const string UserAlreadyInRole = "User already has the specified role.";
        public const string CannotDemoteAdmin = "Admin users cannot be demoted.";
        public const string EventManagerRestrictions = "Event managers can only assign Staff or User roles.";
    }
}
