using System;
using System.Threading;

namespace DP800s
{
    public class DP832A 
    {
        //USB0::0x1AB1::0x0E11::DP8C194807153::INSTR
        CVisaOpt m_VisaOpt = new CVisaOpt();

        public enum OUTPUTMODE { CV,CC,UR}

        public enum CHANNEL:int { CH1=1,CH2,CH3}

        public enum OPMODE : int {OCP,OVP}

        public double[] MeasureValue = new double[3] { 0.0f, 0.0f, 0.0f };  //V,I,P
      
        public void Fetch(object o)
        {
            int nCh = (int)o;
            if (nCh > (int)CHANNEL.CH3 || nCh < (int)CHANNEL.CH1)
                return;
            string cmd = string.Format(":MEASure:ALL? CH{0}",nCh.ToString());
            for (int nLoops = 0; nLoops < 10; nLoops++)
            {
                string strRet = Query(cmd).ToString().Replace("\r", "").Replace("\n", "");
                string[] strValues = strRet.Split(',');
                if (strValues.Length == 3)
                {
                    for (int i = 0; i < 3; i++)
                        if (!double.TryParse(strValues[i], out MeasureValue[i]))
                        {
                            MeasureValue[i] = 0.0f;
                            throw new InvalidCastException("DP832 fetch value is invalid");
                        }
                    break;
                }
            }
        }

        public  void Init(string sn)
        {
            try
            {
                string m_strResourceName = null; //仪器资源名

                string[] InstrResourceArray = m_VisaOpt.FindResource("?*INSTR"); //查找资源

                if (InstrResourceArray[0] == "未能找到可用资源!")
                {
                    throw new Exception("未能找到可用的设备资源！");
                }
                else
                {
                    //示例，选取DP8C系列仪器作为选中仪器
                    for (int i = 0; i < InstrResourceArray.Length; i++)
                    {
                        if (InstrResourceArray[i].Contains("DP8") && InstrResourceArray[i].Contains(sn))
                        {
                            m_strResourceName = InstrResourceArray[i];
                            break;
                        }
                    }
                }
                //如果没有找到指定仪器直接退出
                if (m_strResourceName == null)
                {
                    throw new Exception("未能找到可用的DP832资源！");
                }

                //打开指定资源
                m_VisaOpt.OpenResource(m_strResourceName);
               
            }
            catch (Exception ex)
            {
                throw new Exception($"连接 DP832 出错，{ex.Message}");
            }

        }

        private object Excute(object objCmd)
        {
            try
            {
                lock (m_VisaOpt)
                {
                    m_VisaOpt.Write(objCmd.ToString());
                    Thread.Sleep(50);
                    m_VisaOpt.Write(":OUTP? CH1");
                    string strErr = m_VisaOpt.Read();
                    Thread.Sleep(50);
                    return strErr != "";
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public object Query(object objCmd)
        {
            try
            {
                lock (m_VisaOpt)
                {
                    Thread.Sleep(50);
                    m_VisaOpt.Write(objCmd.ToString());
                    Thread.Sleep(50);
                    return m_VisaOpt.Read().Replace("\r", "").Replace("\n", "");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
       
        #region >>>output
        /// <summary>
        /// 设置输出模式
        /// OUTP: CVCC? CH1
        /// </summary>
        /// <param name="nChannel"></param>
        /// <param name="Mode"></param>
        /// <returns></returns>
        public OUTPUTMODE GetOutputMode(CHANNEL nChannel)
        {
            string strCmd = "";
            string strRet = "";
            strCmd = string.Format("OUTP:MODE? {0}", nChannel.ToString());
            strRet = Query(strCmd).ToString();
            if (strRet.ToUpper().Contains("CV"))
                return OUTPUTMODE.CV;
            else if (strRet.ToUpper().Contains("CC"))
                return OUTPUTMODE.CC;
            else
                return OUTPUTMODE.UR;
        }
        /// <summary>
        /// 设置通道电流值
        /// </summary>
        /// <param name="nChannel"></param>
        /// <param name="fValue"></param>
       
        public void SetCurrentLevel(CHANNEL nChannel, double fValue)
        {
            try
            {
                if (fValue < 0.0f)
                    fValue = 0.0f;
                if (fValue > 3.0f)
                    fValue = 3.0f;
                string strCmd = "";
                strCmd = string.Format(":SOUR{0}:CURR {1}", nChannel.ToString(), fValue.ToString("F6"));
                Excute(strCmd);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error happened when setting DP832 Current Level!{ex.Message}");
            }
           
        }
        /// <summary>
        /// 获取通道电流值
        /// </summary>
        /// <param name="nChannel"></param>
        /// <returns></returns>
        public double GetCurrentLevel(CHANNEL nChannel)
        {
            //[:SOURce[<n>]]:CURRent[:LEVel][:IMMediate][:AMPLitude]? 
            string strCmd = "";
            strCmd = string.Format(":SOUR{0}:CURR?", nChannel.ToString());
            string strRet= Query(strCmd).ToString();
            double fValue = 0.0f;
            if (double.TryParse(strRet, out fValue))
                return fValue;
            return 0.0f;
        }
        /// <summary>
        /// 设置通道电压值
        /// </summary>
        /// <param name="nChannel"></param>
        /// <param name="fValue"></param>
        /// <returns></returns>
        public bool SetVoltLevel(CHANNEL nChannel, double fValue)
        {
            //[:SOURce[<n>]]:VOLTage[:LEVel]
            if (fValue < 0.0f)
                fValue = 0.0f;
            if (fValue > 30.0f)
                fValue = 30.0f;
            string strCmd = "";
            strCmd = string.Format(":SOUR{0}:VOLT {1}", ((int)nChannel).ToString(), fValue.ToString("F6"));
            return (bool)Excute(strCmd);
        }
        /// <summary>
        /// 取得设置的通道电压值
        /// </summary>
        /// <param name="nChannel"></param>
        /// <returns></returns>
        public double GetVoltLevel(CHANNEL nChannel)
        {
            //[:SOURce[<n>]]:VOLTage[:LEVel][:IMMediate][:AMPLitude]? 
            string strCmd = "";
            strCmd = string.Format(":SOUR{0}:VOLT?", nChannel.ToString());
            string strRet = Query(strCmd).ToString();
            double fValue = 0.0f;
            if (double.TryParse(strRet, out fValue))
                return fValue;
            return 0.0f;
        }
        /// <summary>
        /// 设置过保电压和电流
        /// </summary>
        /// <param name="opMode"></param>
        /// <param name="channel"></param>
        /// <param name="fProtValue"></param>
        /// <returns></returns>
        public bool SetProtection(OPMODE opMode,CHANNEL channel, double fProtValue)
        {
            //[:SOURce[<n>]]:CURRent:PROTection[:LEVel]
            string strCmd = "";
            bool bRet = false;
            switch (opMode)
            {
                case OPMODE.OCP:
                    strCmd = string.Format("SOUR{0}:CURR:PROT {1}", (int)channel, fProtValue.ToString("F6"));
                    bRet=(bool)Excute(strCmd);
                    break;
                case OPMODE.OVP:
                    strCmd = string.Format("SOUR{0}:VOLT:PROT {1}", (int)channel, fProtValue.ToString("F6"));
                    bRet = (bool)Excute(strCmd);
                    break;
                default:
                    break;
            }
            return bRet;
        }
        public bool SetOutput(CHANNEL channel, bool bEnable)
        {
            string strCmd = "";
            bool bRet = false;
            strCmd = string.Format(":OUTP {0},{1}", channel.ToString(), bEnable?"ON":"OFF");
            bRet = (bool)Excute(strCmd);
            return bRet;
        }
        public bool GetOutputState(CHANNEL channel)
        {
            string strCmd = "";
           
            strCmd = string.Format(":OUTP? {0}", channel.ToString());
            string strRet = Query(strCmd).ToString();
            return strRet.Contains("ON");
        }
        #endregion
    }
}
