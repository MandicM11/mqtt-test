using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.MqttClientInterfaces
{
    public interface IPublisher
    {
        Task PublishAsync(string topic, byte[] payload);
        Task PublishFileAsync(string topic, string filePath);
    }

    public interface ISubscriber
    {
        Task SubscribeAsync(string topic);
        Task SubscribeToFileAsync(string topic, string saveFilePath);
    }
}

