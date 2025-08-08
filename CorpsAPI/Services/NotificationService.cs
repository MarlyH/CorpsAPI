using CorpsAPI.Data;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using System.Text;

namespace CorpsAPI.Services
{
    public class NotificationService
    {
        private readonly NotificationHubClient _hub;

        public NotificationService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureNotificationHub:ConnectionString"];
            var hubName = configuration["AzureNotificationHub:HubName"];

            _hub = NotificationHubClient.CreateClientFromConnectionString(connectionString, hubName);
        }

        public async Task RegisterDeviceAsync(string deviceToken, string platform, string userId)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                throw new ArgumentException("Device token must not be empty.");

            if (string.IsNullOrWhiteSpace(platform))
                throw new ArgumentException("Platform must not be empty.");

            RegistrationDescription registration;

            switch (platform)
            {
                case "Android":
                    registration = new FcmV1RegistrationDescription(deviceToken);
                    break;
                case "iOS":
                    registration = new AppleRegistrationDescription(deviceToken);
                    break;
                default:
                    throw new ArgumentException("Platform must be either 'iOS' or 'Android'");
            }

            // Add user tag for targetted push notifications
            registration.Tags = new HashSet<string> { $"user:{userId}" };

            try
            {
                // Find and delete any existing registrations with the same device token
                var existingRegistrations = await _hub.GetRegistrationsByChannelAsync(deviceToken, 100);
                foreach (var reg in existingRegistrations)
                {
                    await _hub.DeleteRegistrationAsync(reg);
                }

                // Create new registration
                await _hub.CreateRegistrationAsync(registration);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                throw;
            }
        }

        /*public async Task SendFcmV1NotificationAsync(string userId, string title, string body)
        {
            // Retrieve device token (you must store this when user registers)
            var registrations = await _hub.GetRegistrationsByTagAsync($"user:{userId}", 100);
            var registration = registrations?.FirstOrDefault();
            if (registration == null)
                throw new Exception($"No device token found for user {userId}");

            var deviceToken = registration.PnsHandle;

            var payload = $@"
            {{
              ""message"": {{
                ""token"": ""{deviceToken}"",
                ""notification"": {{
                  ""title"": ""{title}"",
                  ""body"": ""{body}""
                }},
                ""data"": {{
                  ""click_action"": ""FLUTTER_NOTIFICATION_CLICK"",
                  ""customKey"": ""customValue""
                }}
              }}
            }}";

            var notification = new FcmV1Notification(payload)
            {
                ContentType = "application/json"
            };

            try
            {
                await _hub.SendNotificationAsync(notification);
                Console.WriteLine("FCM v1 notification sent.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to send notification: {ex.Message}");
                throw;
            }
        }*/

        public async Task SendCrossPlatformNotificationAsync(string userId, string title, string body)
        {
            var registrations = await _hub.GetRegistrationsByTagAsync($"user:{userId}", 100);
            if (registrations == null || !registrations.Any())
                throw new Exception($"No device registrations found for user {userId}");

            foreach (var registration in registrations)
            {
                string deviceToken = registration.PnsHandle;
                Notification notification;

                if (string.IsNullOrWhiteSpace(deviceToken))
                {
                    Console.Error.WriteLine($"Skipping registration with empty token for user {userId}");
                    continue;
                }

                if (registration is FcmV1RegistrationDescription)
                {
                    var fcmPayload = $@"
                    {{
                        ""message"": {{
                            ""token"": ""{deviceToken}"",
                            ""notification"": {{
                                ""title"": ""{title}"",
                                ""body"": ""{body}""
                            }},
                            ""data"": {{
                                ""click_action"": ""FLUTTER_NOTIFICATION_CLICK"",
                                ""customKey"": ""customValue""
                            }}
                        }}
                    }}";

                    notification = new FcmV1Notification(fcmPayload)
                    {
                        ContentType = "application/json"
                    };
                }
                else if (registration is AppleRegistrationDescription)
                {
                    var apnsPayload = $@"
                    {{
                        ""aps"": {{
                            ""alert"": {{
                                ""title"": ""{title}"",
                                ""body"": ""{body}""
                            }},
                            ""sound"": ""default""
                        }},
                        ""customKey"": ""customValue""
                    }}";

                    notification = new AppleNotification(apnsPayload)
                    {
                        ContentType = "application/json"
                    };
                }
                else
                {
                    Console.Error.WriteLine($"Unsupported registration type {registration.GetType().Name} for user {userId}");
                    continue;
                }

                try
                {
                    await _hub.SendNotificationAsync(notification, $"user:{userId}");
                    Console.WriteLine($"Notification sent to {registration.GetType().Name} device for user {userId}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to send notification to {registration.GetType().Name} device: {ex.Message}");
                }
            }
        }
    }
}

