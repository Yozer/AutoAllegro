using System.Threading.Tasks;

namespace AutoAllegro.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}
