using NationalInstruments.VisaNS;
using System;

namespace DP800s
{
    public class CVisaOpt 
    {
        private MessageBasedSession mbSession = null;     //会话

        private ResourceManager mRes = null;              //资源管理

        public static string[] ResourceArray = null;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        /// <param name="strRes"></param>
        /// <returns></returns>
        ///
        public CVisaOpt()
        {
        }

        /// <summary>
        /// 静态函数，查找仪器资源
        /// </summary>
        /// <param name="strRes"></param>
        /// <returns></returns>
         public string[] FindResource(string strRes)
        {
            //string[] VisaRes = new string[1];
            try
            {
                mRes = null;
                mRes = ResourceManager.GetLocalManager();
                if (mRes == null)
                {
                    //throw new Exception("本机未安装Visa的.Net支持库！");
                }
                ResourceArray = mRes.FindResources(strRes);

                //mRes.Open();
            }
            catch (System.ArgumentException)
            {
                ResourceArray = new string[1];
                ResourceArray[0] = "未能找到可用资源!";
            }
            return ResourceArray;
        }

        /// <summary>
        /// 打开资源
        /// </summary>
        /// <param name="strResourceName"></param>
        public void OpenResource(string strResourceName)
        {
            //若资源名称为空，则直接返回
            if (strResourceName != null)
            {
                try
                {

                    mRes = ResourceManager.GetLocalManager();
                    mbSession = (MessageBasedSession)mRes.Open(strResourceName);
                    //此资源的超时属性
                    //setOutTime(5000);
                    mbSession.Timeout = 15000;
                }
                catch (Exception ex)
                {
                    throw new Exception("VisaCtrl-VisaOpen\n" + ex.Message);
                }
            }
        }


        /// <summary>
        /// 写命令函数
        /// </summary>
        /// <param name="strCommand"></param>
        public void Write(string strCommand)
        {
            try
            {
                if (mbSession != null)
                {
                    mbSession.Write(strCommand);
                }
            }
            catch (Exception exp)
            {
                throw new Exception("VisaCtrl-VisaOpen\n" + exp.Message);
            }
        }


        /// <summary>
        /// 读取返回值函数
        /// </summary>
        /// <returns></returns>
        public string Read()
        {
            try
            {
                if (mbSession != null)
                {
                    return mbSession.ReadString();
                }
            }
            catch (NationalInstruments.VisaNS.VisaException)
            {
                return Convert.ToString(0);
            }

            return Convert.ToString(0);
        }
        

        /// <summary>
        /// 设置超时时间
        /// </summary>
        /// <param name="time">MS</param>
        public void SetOutTime(int time)
        {
            mbSession.Timeout = time;
        }

        /// <summary>
        /// 释放会话
        /// </summary>
        public void Release()
        {
            if (mbSession != null)
            {
                mbSession.Dispose();
            }
        }
    }
}
