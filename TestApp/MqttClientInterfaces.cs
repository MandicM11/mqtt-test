using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.MqttClientInterfaces
{
    public interface IPublisher
    {
        Task PublishAsync(string topic, byte[] data);

    }

    public interface ISubscriber
    {
        Task SubscribeAsync(string topic);
        
    }

    public interface IFileChanged
    {
        Task<byte[]> ReadPayloadAsync(object data);
        Task<bool> FileChangedAsync(object data);

        Task<byte[]>DatabaseChangedAsync();

        

    }

    public interface IOnFileChanged
    {
        Task ReadFileAsync(byte[] payload);

        Task ReaderDatabaseAsync(byte[] paylod);

    }

}

