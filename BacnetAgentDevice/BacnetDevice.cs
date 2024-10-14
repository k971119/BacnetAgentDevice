using CShapeDeviceAgent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BacnetAgentDevice
{
    public class BacnetDevice : CShapeDeviceBase
    {
        int _Device;
        AlarmPointFinder _AlarmPointFinder;

        BacnetClient client = null;

        BacnetAddress systemAddress;

        ConcurrentBag<BacnetNode> bacNodes = new ConcurrentBag<BacnetNode>();

        String localIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
        int localPort = 0xBAC0;

        public override bool Init(int iDeviceID)
        {
            _Device = iDeviceID;
            _AlarmPointFinder = new AlarmPointFinder(_Device);

            //systemAddress = new BacnetAddress(BacnetAddressTypes.IP, "192.168.10.101", 0);

            return base.Init(iDeviceID);
        }

        public override bool Connect(string sIP, int iPort)
        {

            systemAddress = new BacnetAddress(BacnetAddressTypes.IP, sIP, (ushort)iPort);

            try
            {
                client = new BacnetClient(new BacnetIpUdpProtocolTransport(localPort));
                client.Start();

                client.OnIam += new System.IO.BACnet.BacnetClient.IamHandler((sender, addr, deviceId, maxAPDU, segmentation, vendroid) =>            //Iam 이벤트 등록
                {
                    foreach (BacnetNode node in bacNodes)          //이미 등록된 주소면 스킵
                    {
                        if (node.getAddress(deviceId) != null)
                        {
                            if (node.getAddress(deviceId).Equals(addr))
                                return;
                        }
                    }
                    Console.WriteLine(addr.adr.ToString());
                    bacNodes.Add(new BacnetNode(addr, deviceId));
                });
                client.WhoIs();     //onIam 이벤트 등록후 whois

                //이게 필요할까?
                client.RegisterAsForeignDevice(localIp, 60);
                Thread.Sleep(20);
                client.RemoteWhoIs(localIp, localPort);         //후이즈 등록

                Thread.Sleep(500);     //디바이스들을 bacNodes에 담기위해서 잠시대기

                ToConnectState(_Device, true);
            }
            catch (Exception e)
            {
                ToConnectState(_Device, false);
                Console.WriteLine(e.ToString());
            }

            new Thread(() =>        //2초마다 알람으로 등록된 포인트 감시
            {
                while (true)
                {
                    if (_AlarmPointFinder.getAlarmPointsCount() > 0)
                    {
                        SendData(0, _AlarmPointFinder.getAlarmPoints(), _AlarmPointFinder.getAlarmPointsCount());
                    }
                    else
                    {
                        break;
                    }
                    Thread.Sleep(2000);
                }
            }).Start();

            return true;
        }

        public override int SendData(int sendType, string cData, int DataSize)
        {
            bool control = false;

            String[] systemList = cData.Split(',');
            if (cData.Contains(";"))
            {
                control = true;
            }
            else
            {
                control = false;
            }
            String responseValue = String.Empty;

            //systemList에 들어가있는 systemID 잘요리해서 타입 인스턴스 번호뽑아서 BacnetObjectID만들어서 deviceIDreadScalarValue 함수 돌리자
            foreach (String systemId in systemList)
            {
                String[] systemIdSplit = systemId.Split('-');
                String deviceId = systemIdSplit[0];
                String moduleId = systemIdSplit[1];
                String sType = systemIdSplit[2];
                String instance = systemIdSplit[3];

                try
                {
                    uint uInstance = (131072 * uint.Parse(moduleId)) + uint.Parse(instance);

                    BacnetObjectId bacnetObjectId = new BacnetObjectId();
                    BacnetValue bacnetValue = new BacnetValue();
                    switch (sType)
                    {
                        case "AI":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, uInstance);
                            break;
                        case "AO":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, uInstance);
                            break;
                        case "AV":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, uInstance);
                            break;
                        case "BI":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, uInstance);
                            break;
                        case "BO":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, uInstance);
                            break;
                        case "BV":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, uInstance);
                            break;
                        case "MSI":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, uInstance);
                            break;
                        case "MSO":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, uInstance);
                            break;
                        case "MSV":
                            bacnetObjectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_VALUE, uInstance);
                            break;
                    }
                    if (control)
                    {
                        bacnetValue.Value = systemList[1];
                        if (!writeScalarValue(uint.Parse(deviceId), bacnetObjectId, bacnetValue))
                        {
                            Console.WriteLine("제어실패");
                        }
                        break;
                    }
                    else
                    {
                        if (readScalarValue(uint.Parse(deviceId), bacnetObjectId, BacnetPropertyIds.PROP_PRESENT_VALUE, out bacnetValue))
                        {
                            responseValue += String.Format("{0},{1},13;", systemId, bacnetValue.Value.ToString());
                        }
                    }

                    Thread.Sleep(10);
                }catch (Exception ex) { continue; }
            }

            base.ToReceive(_Device, 3, responseValue, systemList.Length);

            return base.SendData(sendType, cData, DataSize);
        }

        public override bool DisConnect()
        {
            ToConnectState(_Device, false);
            return base.DisConnect();
        }

        public bool writeScalarValue(uint deviceId, BacnetObjectId BacnetObjet, BacnetValue Value)
        {
            BacnetAddress adr = null;

            // Looking for the device

            foreach (BacnetNode node in bacNodes)
            {
                if (node.getDeviceId() == deviceId)
                {
                    adr = node.getAddress(deviceId);
                    break;
                }

            }

            if (adr == null) return false;  // not found

            // Property Write
            BacnetValue[] NoScalarValue = { Value };
            if (client.WritePropertyRequest(adr, BacnetObjet, BacnetPropertyIds.PROP_PRESENT_VALUE, NoScalarValue) == false)
                return false;

            return true;
        }

        public bool readScalarValue(BacnetObjectId bacnetObject, out BacnetValue value)
        {
            BacnetAddress adr;
            IList<BacnetValue> values;

            value = new BacnetValue(null);

            adr = systemAddress;
            if (adr == null) return false;

            try
            {
                if (client.ReadPropertyRequest(adr, bacnetObject, BacnetPropertyIds.PROP_PRESENT_VALUE, out values) == false) return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }

            value = values[0];
            return true;
        }

        public bool readScalarValue(uint deviceId, BacnetObjectId bacnetObject, BacnetPropertyIds property, out BacnetValue value)
        {
            BacnetAddress adr = null;
            IList<BacnetValue> values;

            // BACnet 노드에서 주어진 deviceId에 해당하는 주소 찾기
            foreach (BacnetNode node in bacNodes)
            {
                if (node.getDeviceId() == deviceId)
                {
                    adr = node.getAddress(deviceId);
                    break;
                }
            }

            value = new BacnetValue(null);

            if (adr == null)
            {
                Console.WriteLine($"Device with ID {deviceId} not found.");
                return false;
            }

            try
            {
                // 프로퍼티 값을 읽어오기
                if (!client.ReadPropertyRequest(adr, bacnetObject, property, out values) || values.Count == 0)
                {
                    Console.WriteLine($"Failed to read property {property} for device {deviceId}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading property {property} for device {deviceId}: {ex}");
                return false;
            }

            value = values[0];
            return true;
        }

        public bool readMultiScalarValue(uint deviceId, BacnetObjectId bacnetObject, BacnetPropertyIds property, out BacnetValue value)
        {
            BacnetAddress adr = null;
            IList<BacnetValue> values;

            // BACnet 노드에서 주어진 deviceId에 해당하는 주소 찾기
            foreach (BacnetNode node in bacNodes)
            {
                if (node.getDeviceId() == deviceId)
                {
                    adr = node.getAddress(deviceId);
                    break;
                }
            }

            value = new BacnetValue(null);

            if (adr == null)
            {
                Console.WriteLine($"Device with ID {deviceId} not found.");
                return false;
            }

            try
            {
                // 프로퍼티 값을 읽어오기
                if (!client.ReadPropertyRequest(adr, bacnetObject, property, out values) || values.Count == 0)
                {
                    Console.WriteLine($"Failed to read property {property} for device {deviceId}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading property {property} for device {deviceId}: {ex}");
                return false;
            }

            value = values[0];
            return true;
        }

        public bool writeScalarValue(int deviceId, BacnetObjectId bacnetObject, BacnetValue value)
        {
            BacnetAddress adr;
            IList<BacnetValue> values = new List<BacnetValue>();

            values.Add(value);

            adr = systemAddress;
            if (adr == null) return false;

            try
            {
                if (client.WritePropertyRequest(adr, bacnetObject, BacnetPropertyIds.PROP_PRESENT_VALUE, values) == false) return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            return true;
        }
    }
}
