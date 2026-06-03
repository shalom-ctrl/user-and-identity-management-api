
using User.Management.Service.Models;

namespace User.Management.Service.Services
{
    public interface IEmailService
    {
        void SendEmail(User.Management.Service.Models.Message message);

    }
}
