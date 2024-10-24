using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbConnection
{
    public class RoomTemperature
    {
        public int Id { get; set; }
        public string RoomName { get; set; }
        public DateTime CurrentTime { get; set; }
        public int CurrentTemperature { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
