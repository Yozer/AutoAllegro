using System.Threading.Tasks;

namespace AutoAllegro.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string content, string replyTo = null, string displayName = null);
    }
}
