namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class LogoutRequest
    {
        /// <summary>
        /// The FCM token of the device signing out, so its push registration goes with the
        /// session. Optional: a client that omits it logs out as before, and the registration
        /// is then only cleared when some account next registers the same device.
        /// </summary>
        public string? DeviceToken { get; set; }
    }
}
