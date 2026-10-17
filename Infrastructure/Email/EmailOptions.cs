using MailKit.Security;

namespace smart_pet_care_api.Infrastructure.Email
{
    public class EmailOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        // The name the inbox shows next to the message. Overridden by
        // Email__FromName / EMAIL_FROM_NAME in a deployed environment.
        public string FromName { get; set; } = "Miso";

        /// <summary>
        /// Allows a connection that never negotiates TLS. Off by default, because the
        /// alternative -- continuing in cleartext when the server does not advertise
        /// STARTTLS -- puts the SMTP password and the confirmation code on the wire in
        /// the clear, and a downgrade looks exactly like a server that is simply plain.
        /// Set it only for a relay reached over the loopback interface or a private
        /// network, where there is no segment to intercept.
        /// </summary>
        public bool AllowInsecureConnection { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

        /// <summary>
        /// 465 is implicit TLS (TLS before the greeting); everything else is submission
        /// with a mandatory STARTTLS upgrade. Chosen from the port rather than
        /// configured, since the two are the same decision and splitting them is one
        /// more thing to get wrong in an .env file.
        /// </summary>
        public SecureSocketOptions SocketOptions => (AllowInsecureConnection, Port) switch
        {
            (true, _) => SecureSocketOptions.StartTlsWhenAvailable,
            (false, 465) => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls
        };
    }
}
