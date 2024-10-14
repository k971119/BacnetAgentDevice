using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BacnetAgentDevice
{
    public class BacnetNode
    {
        BacnetAddress address;
        uint deviceId;

        public BacnetNode(BacnetAddress address, uint deviceId)
        {
            this.address = address;
            this.deviceId = deviceId;
        }

        public BacnetNode()
        {

        }

        public BacnetAddress getAddress(uint deviceId)
        {
            if (this.deviceId == deviceId)
                return address;
            else
                return null;
        }
        public uint getDeviceId()
        {
            return this.deviceId;
        }
    }
}
