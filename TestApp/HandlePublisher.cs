using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    public class HandlePublisher
    {
        public async Task<byte[]> HandlePayloadAsync(object data)
        {
            byte[] payload;

            if (data is string messageOrFilePath)
            {
                if (File.Exists(messageOrFilePath))  // Check if it's a file path
                {
                    // Read file content into byte array
                    payload = await File.ReadAllBytesAsync(messageOrFilePath);
                }
                else
                {
                    // Treat it as a regular message if it's not a file path
                    payload = Encoding.UTF8.GetBytes(messageOrFilePath);
                }
            }

            
           
            else
            {
                throw new ArgumentException("Unsupported data type for publishing.");
            }
            return payload;
        }

    }
}
