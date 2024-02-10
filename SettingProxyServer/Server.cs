using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SettingProxyServer
{
    public class Server
    {

        public string Host { get;set; }
        public string Username { get;set; }
        public string Password {  get;set; }

        public Server(string host, string username, string password) 
        { 
            this.Host = host;
            this.Username = username;
            this.Password = password;
        }
    }
}
