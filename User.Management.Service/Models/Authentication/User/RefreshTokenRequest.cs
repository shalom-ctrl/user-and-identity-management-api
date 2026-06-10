using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace User.Management.Service.Models.Authentication.User
{
    public class RefreshTokenRequest
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }
    }
}
