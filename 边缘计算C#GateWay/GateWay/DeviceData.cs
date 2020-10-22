using System;
using System.Collections.Generic;
using System.Text;

namespace GateWay
{
    public class DeviceData
    {
        /// <summary>
        /// 站号
        /// </summary>
        public int Sation { get; set; }

        public int Code { get; set; }


        public string Address { get; set; }


        public string Name { get; set; }

        public int Value { get; set; }

        /// <summary>
        /// 数据地址
        /// </summary>
       // public List<AddressData> AddressDataList { get; set; }
    }


    public class AddressData
    {
        
        public bool Online { get; set; }
        public string Address { get; set; }

        public int Value { get; set; }
    }
}
