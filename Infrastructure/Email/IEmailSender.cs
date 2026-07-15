namespace smart_pet_care_api.Infrastructure.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string toAddress, string subject, string body);
    }
}
