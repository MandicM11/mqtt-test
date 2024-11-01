using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.helpers
{
    public class Helpers
    {

        public string GetFileExtension(byte[] payload)
        {

            if (payload.Length >= 4)
            {
                // Check for common image file signatures (magic numbers)
                if (payload[0] == 0xFF && payload[1] == 0xD8) // JPEG
                {
                    return ".jpg";
                }
                else if (payload[0] == 0x89 && payload[1] == 0x50 && payload[2] == 0x4E && payload[3] == 0x47) // PNG
                {
                    return ".png";
                }
                else if (payload[0] == 0x47 && payload[1] == 0x49 && payload[2] == 0x46) // GIF
                {
                    return ".gif";
                }

                else if (IsTextPayload(payload))
                {
                    
                    var text = Encoding.UTF8.GetString(payload);
                    if (text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("["))
                    {
                        return ".json";
                    }
                    return ".txt";
                }

            }

            // Default to .bin if file type is not recognized
            return ".bin";
        }
        public bool IsTextPayload(byte[] payload)
        {
            // Check if all bytes are within the ASCII printable range (32 to 126)
            return payload.All(b => b >= 32 && b <= 126);
        }
       

    }
}
