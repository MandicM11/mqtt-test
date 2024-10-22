using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.MqttClientInterfaces
{
    public interface IPublisher
    {
        Task PublishAsync(string topic, object data);
    }

    public interface ISubscriber
    {
        Task SubscribeAsync(string topic, Action<object> onDataReceived);
    }

}

