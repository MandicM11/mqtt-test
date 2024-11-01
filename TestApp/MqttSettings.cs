using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    public class MqttSettings
    {
        public string Broker { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Topic { get; set; }

        public string Role { get; set; }
        public string PublishedFilePath {  get; set; }
        public string SavedFilePath {  get; set; }

        public string LocalFilePath {  get; set; }
        public string DbChangesFilePath { get; set; }
        public string ConnectionString { get; set; }
        public string PublisherConnectionString { get; set; }
        public string SubscriberConnectionString {  get; set; }

    }

}
