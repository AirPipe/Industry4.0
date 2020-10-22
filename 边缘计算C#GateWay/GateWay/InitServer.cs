using Common;
using MQTTnet;
using NewCommon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace GateWay
{
    public class InitServer
    {
        ModbusRtu busRtuClient;
        MqttHelper mqtt;
        List<DeviceData> deviceDatas;
        List<CmdData> cmdDatas;
        string json;
        public InitServer()
        {
            deviceDatas = new List<DeviceData>();
            cmdDatas = new List<CmdData>();
            LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/DeviceConfig.xml"));
            json =LoadJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/Data.json"));
            busRtuClient = new ModbusRtu();
            busRtuClient.SerialPortInni(sp =>
            {
                sp.PortName = Utils.PortName;
                sp.BaudRate = Utils.baudRate;
                sp.DataBits = 8;
                sp.StopBits = Utils.stopBits == 0 ? System.IO.Ports.StopBits.None : (Utils.stopBits == 1 ? System.IO.Ports.StopBits.One : System.IO.Ports.StopBits.Two);
                sp.Parity = Utils.parity == "无" ? System.IO.Ports.Parity.None : (Utils.parity == "奇" ? System.IO.Ports.Parity.Odd : System.IO.Ports.Parity.Even);
            });
            try
            {
                busRtuClient.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine( ex.Message);
            }
           


            mqtt = new MqttHelper(Utils.ServerIp,Utils.Port, Guid.NewGuid().ToString(),Utils.Username,Utils.Password,Utils.SubscribeTopic);
            mqtt.ApplicationMessageReceived += MqttApplicationMessageReceived;
            mqtt.ConnectServer();



            Thread th = new Thread(DoCore);
            th.IsBackground = true;
            th.Start();
        }

        /// <summary>
        /// 接收消息触发事件
        /// </summary>
        /// <param name="e"></param>
        private  void MqttApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                string json = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                Console.WriteLine("接收控制指令" + json);
                JObject obj = JObject.Parse(json);
                foreach (var x in obj)
                {
                    ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback((obj) =>
                    {
                        CmdData cmdData1 = cmdDatas.Find(delegate (CmdData cmdData)
                        {
                            return cmdData.Name == x.Key;
                        });
                        if (cmdData1 != null)
                        {
                            Console.WriteLine($"站地址{cmdData1.Sation},写入指令{cmdData1.Code},写入地址{cmdData1.Address},数据为:{x.Value}");
                            busRtuClient.Write($"s={cmdData1.Sation};x={cmdData1.Code};{cmdData1.Address}", Convert.ToInt16(x.Value));

                        }
                    }), null);
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine("指令接受错误："+ex.Message);
            }
            
           
        }
        private void  DoCore()
        {
            while(true)
            {
                Thread.Sleep(Utils.SleepTime);
                string newText = json;
                for (int i = 0; i < deviceDatas.Count; i++)
                {
                    var res = busRtuClient.ReadInt16($"s={deviceDatas[i].Sation};x={deviceDatas[i].Code};{deviceDatas[i].Address}");
                    while (!res.IsSuccess)
                    {
                        res = busRtuClient.ReadInt16($"s={deviceDatas[i].Sation};x={deviceDatas[i].Code};{deviceDatas[i].Address}");
                    }
                    if (res.IsSuccess)
                    {
                        deviceDatas[i].Value = res.Content; //数据
                        newText = newText.Replace(deviceDatas[i].Name, deviceDatas[i].Value.ToString());
                        
                    }
                }
                // 所有数据采集完  推送
                mqtt.Publish(Utils.PublishTopic, newText);
            }
        }

        //private string  GetJson()
        //{
        //    JObject obj = new JObject();
        //}

        private void LoadFile(string path)
        {
            if (File.Exists(path))
            {
                XElement root = XElement.Load(path);
                if(root!=null)
                {
                    Utils.PortName = root.Element("deviceinfo").Attribute("portName").Value.ToString();
                    Utils.baudRate = Convert.ToInt32(root.Element("deviceinfo").Attribute("baudRate").Value);
                    Utils.stopBits = Convert.ToInt32(root.Element("deviceinfo").Attribute("StopBits").Value);
                    Utils.parity = root.Element("deviceinfo").Attribute("Parity").Value.ToString();


                    Utils.SleepTime = Convert.ToInt32(root.Element("mqttinfo").Attribute("SleepTime").Value);
                    Utils.ServerIp = root.Element("mqttinfo").Attribute("serverIP").Value.ToString();
                    Utils.Username = root.Element("mqttinfo").Attribute("userName").Value.ToString();
                    Utils.Password = root.Element("mqttinfo").Attribute("Password").Value.ToString();
                    Utils.PublishTopic = root.Element("mqttinfo").Attribute("PublishTopic").Value.ToString();
                    Utils.SubscribeTopic = root.Element("mqttinfo").Attribute("SubscribeTopic").Value.ToString();

                    foreach (var item in root.Elements("deviceData").Elements())
                    {
                        string name = item.Attribute("name").Value.ToString();
                        int deviceid = Convert.ToInt32(item.Attribute("deviceid").Value);
                        int code = Convert.ToInt32(item.Attribute("code").Value);
                        string deviceaddress = item.Attribute("deviceaddress").Value.ToString();
                        deviceDatas.Add(new DeviceData
                        {
                            Sation = deviceid,
                            Code=code,
                            Address=deviceaddress,
                            Name=name
                        });
                    }


                    foreach (var item in root.Element("SendCmd").Elements())
                    {
                        string name = item.Attribute("name").Value.ToString();
                        int deviceid = Convert.ToInt32(item.Attribute("deviceid").Value);
                        int code = Convert.ToInt32(item.Attribute("code").Value);
                        string deviceaddress = item.Attribute("deviceaddress").Value.ToString();

                        cmdDatas.Add(new CmdData
                        {
                            Sation = deviceid,
                            Code = code,
                            Address = deviceaddress,
                            Name = name
                        });
                    }

                }              
            }
            else
            {
                Console.WriteLine("配置文件路径不存在");
            }
        }


        private string LoadJson(string path)
        {
            //using (System.IO.StreamReader file = System.IO.File.OpenText(path))
            //{
            //    using (JsonTextReader reader = new JsonTextReader(file))
            //    {
            //        JObject o = (JObject)JToken.ReadFrom(reader);
            //        //var value = o[key].ToString();
            //        return o.ToString();
            //    }
            //}

          return  File.ReadAllText(path);
        }

    }
}
