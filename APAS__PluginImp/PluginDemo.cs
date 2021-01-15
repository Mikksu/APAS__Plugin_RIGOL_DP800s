using APAS__PluginContract.ImplementationBase;
using DP800s;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SystemServiceContract.Core;
using APAS__Plugin_RIGOL_DP800s.Classes;
using APAS__Plugin_RIGOL_DP800s.Views;

namespace APAS__Plugin_RIGOL_DP800s
{
    public class PluginDemo : PluginMultiChannelMeasurableEquipment
    {

        #region Variables

        enum REMOTE_CTRL_CH
        {
            CH1_RT_VCC,
            CH1_RT_CURR,
            CH2_RT_VCC,
            CH2_RT_CURR,
            CH3_RT_VCC,
            CH3_RT_CURR
        }

        const DP832A.CHANNEL VCC_CH = DP832A.CHANNEL.CH1;
        const DP832A.CHANNEL VMOD_CH = DP832A.CHANNEL.CH2;

        const string PATTEN_CONTROL_PARAM_ON = @"^ON ([1-3]|ALL)$";
        const string PATTEN_CONTROL_PARAM_OFF = @"^OFF ([1-3]|ALL)$";
        const string PATTEN_CONTROL_PARAM_VLEV = @"^VLEV ([1-3]),([0-9]+\.?[0-9]*)$";
        const string PATTEN_CONTROL_PARAM_OVP = @"^OVP ([1-3]),([0-9]+\.?[0-9]*)$";
        const string PATTEN_CONTROL_PARAM_OCP = @"^OCP ([1-3]),([0-9]+\.?[0-9]*)$";

        const string CFG_ITEM_OCP_1 = "DP831_OCP_A_CH1";
        const string CFG_ITEM_OVP_1 = "DP831_OVP_V_CH1";
        const string CFG_ITEM_VSET_1 = "DEF_VSET_CH1";
        const string CFG_ITEM_OCP_2 = "DP831_OCP_A_CH2";
        const string CFG_ITEM_OVP_2 = "DP831_OVP_V_CH2";
        const string CFG_ITEM_VSET_2 = "DEF_VSET_CH2";
        const string CFG_ITEM_OCP_3 = "DP831_OCP_A_CH3";
        const string CFG_ITEM_OVP_3 = "DP831_OVP_V_CH3";
        const string CFG_ITEM_VSET_3 = "DEF_VSET_CH3";

        public event EventHandler OnCommOneShot;

        DP832A dp800 = null;
        readonly string dp800sn = "";


        /// <summary>
        /// how long it takes to wait between the two sampling points.
        /// </summary>
        int readIntervalms = 200;

        private Task bgTask;
        private CancellationTokenSource cts;
        private CancellationToken ct;
        private bool _isInit;
        readonly Configuration config = null;

        IProgress<DP800ReadingResponse> progress;

        #endregion

        #region Constructors

        public PluginDemo(ISystemService APASService) : base(Assembly.GetExecutingAssembly(), APASService)
        {
            #region Configuration Reading

            config = GetAppConfig();

            _loadConfigItem(config, "ReadIntervalMillisec", out readIntervalms, 200);

            _loadConfigItem(config, "DP831_SN", out dp800sn, "");

            _loadConfigItem(config, CFG_ITEM_OCP_1, out double dp831_ocp_a_ch1, 0.2);
            _loadConfigItem(config, CFG_ITEM_OCP_2, out double dp831_ocp_a_ch2, 0.2);
            _loadConfigItem(config, CFG_ITEM_OCP_3, out double dp831_ocp_a_ch3, 0.2);
            _loadConfigItem(config, CFG_ITEM_OVP_1, out double dp831_ovp_v_ch1, 0.2);
            _loadConfigItem(config, CFG_ITEM_OVP_2, out double dp831_ovp_v_ch2, 0.2);
            _loadConfigItem(config, CFG_ITEM_OVP_3, out double dp831_ovp_v_ch3, 0.2);
            _loadConfigItem(config, CFG_ITEM_VSET_1, out double def_vset_1, 0);
            _loadConfigItem(config, CFG_ITEM_VSET_2, out double def_vset_2, 0);
            _loadConfigItem(config, CFG_ITEM_VSET_3, out double def_vset_3, 0);

            #endregion

            this.Port = $"USB IVI,{dp800sn}";

            this.PsSingleChannel = new PowerSupplyChannel[3]
            {
                new PowerSupplyChannel(DP832A.CHANNEL.CH1, this)
                {
                    OVPSet = dp831_ovp_v_ch1,
                    OCPSet = dp831_ocp_a_ch1,
                    VoltLevelSet = def_vset_1
                },
                new PowerSupplyChannel(DP832A.CHANNEL.CH2, this)
                {
                    OVPSet = dp831_ovp_v_ch2,
                    OCPSet = dp831_ocp_a_ch2,
                    VoltLevelSet = def_vset_2
                },
                new PowerSupplyChannel(DP832A.CHANNEL.CH3, this)
                {
                    OVPSet = dp831_ovp_v_ch3,
                    OCPSet = dp831_ocp_a_ch3,
                    VoltLevelSet = def_vset_3
                }
            };


            this.UserView = new PluginDemoView
            {
                DataContext = this
            };

            this.HasView = true;

            //! the progress MUST BE defined in the ctor since
            //! we operate the UI elements in the OnCommOneShot event.
            progress = new Progress<DP800ReadingResponse>(x =>
            {
                x.ChannelInstance.IsOutputEnabled = x.IsEnabled;
                x.ChannelInstance.RtVoltage = x.RtVoltage;
                x.ChannelInstance.RtCurrent = x.RtCurrent;
                x.ChannelInstance.RtWatt = x.RtWatt;

                OnCommOneShot?.Invoke(this, new EventArgs());
            });
        }

        #endregion

        #region Properties

        public override string Name => "RIGOL DP800s";

        public override string Usage =>
            "普源DP800系列直流电源控制程序。\n" +
            "Fetch(0)：CH1实时电压（V）。\n" +
            "Fetch(1)：CH1实时电流（A）。\n" +
            "Fetch(2)：CH2实时电压（V）。\n" +
            "Fetch(3)：CH2实时电流（A）。\n" +
            "Fetch(4)：CH3实时电压（V）。\n" +
            "Fetch(5)：CH3实时电流（A）。\n" +
            "支持的命令: \n" +
            "ON [1|2|3|ALL]：打开指定通道或全部电源输出；\n" +
            "OFF [1|2|3|ALL]：关闭指定通道或所有通道电源输出；\n" +
            "VLEV [1|2|3],value：设置指定通道的输出电压，单位V；\n" +
            "OVP [1|2|3],value：设置指定通道的保护电压，单位V；\n" +
            "OCP [1|2|3],value：设置指定通道的保护电流，单位V；";

        /// <summary>
        /// 最大测量通道。
        /// </summary>
        public override int MaxChannel => 6;

        public override string[] ChannelCaption => 
            new string[] 
            {
                "CH1电压",
                "CH1电流",
                "CH2电压",
                "CH2电流",
                "CH3电压",
                "CH3电流"
            };

        public override bool IsInitialized
        {
            get
            {
                return _isInit;
            }
            protected set
            {
                UpdateProperty(ref _isInit, value);
            }
        }

        public PowerSupplyChannel[] PsSingleChannel { get; }

        #endregion

        #region Methods

        public sealed override async Task<object> Execute(object args)
        {
            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// Switch to the specific channel.
        /// </summary>
        /// <param name="param">[int] The specific channel.</param>
        /// <returns></returns>
        public sealed override async Task Control(string param)
        {
            if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_ON)) // "ON"
            {
                var m = Regex.Match(param, PATTEN_CONTROL_PARAM_ON);
                if(m.Success)
                {
                    if (m.Groups[1].Value == "1")
                        _setOutput(DP832A.CHANNEL.CH1, true);
                    else if (m.Groups[1].Value == "2")
                        _setOutput(DP832A.CHANNEL.CH2, true);
                    else if (m.Groups[1].Value == "3")
                        _setOutput(DP832A.CHANNEL.CH3, true);
                    else  if(m.Groups[1].Value == "ALL")
                    {
                        _setOutput(DP832A.CHANNEL.CH1, true);
                        _setOutput(DP832A.CHANNEL.CH2, true);
                        _setOutput(DP832A.CHANNEL.CH3, true);
                    }
                    else
                        goto __param_err;

                }
                else
                {
                    goto __param_err;
                }
            }
            else if(Regex.IsMatch(param, PATTEN_CONTROL_PARAM_OFF)) // "OFF"
            {
                var m = Regex.Match(param, PATTEN_CONTROL_PARAM_OFF);
                if (m.Success)
                {
                    if (m.Groups[1].Value == "1")
                        _setOutput(DP832A.CHANNEL.CH1, false);
                    else if (m.Groups[1].Value == "2")
                        _setOutput(DP832A.CHANNEL.CH2, false);
                    else if (m.Groups[1].Value == "3")
                        _setOutput(DP832A.CHANNEL.CH3, false);
                    else if (m.Groups[1].Value == "ALL")
                    {
                        _setOutput(DP832A.CHANNEL.CH1, false);
                        _setOutput(DP832A.CHANNEL.CH2, false);
                        _setOutput(DP832A.CHANNEL.CH3, false);
                    }
                    else
                        goto __param_err;
                }
                else
                {
                    goto __param_err;
                }
            }
            else if(Regex.IsMatch(param, PATTEN_CONTROL_PARAM_VLEV)) // Set Output Voltage Level
            {
                var m = Regex.Match(param, PATTEN_CONTROL_PARAM_VLEV);
                if (m.Success)
                {
                    DP832A.CHANNEL ch = DP832A.CHANNEL.CH1;

                    if (m.Groups[1].Value == "1")
                        ch = DP832A.CHANNEL.CH1;
                    else if (m.Groups[1].Value == "2")
                        ch = DP832A.CHANNEL.CH2;
                    else if (m.Groups[1].Value == "3")
                        ch = DP832A.CHANNEL.CH3;
                    else
                        goto __param_err;

                    if (double.TryParse(m.Groups[2].Value, out double v))
                        _setVLevel(ch, v);
                    else
                        goto __param_err;
                }
                else
                {
                    goto __param_err;
                }
            }
            else if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_OVP)) // Set OVP
            {
                var m = Regex.Match(param, PATTEN_CONTROL_PARAM_OVP);
                if (m.Success)
                {
                    DP832A.CHANNEL ch = DP832A.CHANNEL.CH1;

                    if (m.Groups[1].Value == "1")
                        ch = DP832A.CHANNEL.CH1;
                    else if (m.Groups[1].Value == "2")
                        ch = DP832A.CHANNEL.CH2;
                    else if (m.Groups[1].Value == "3")
                        ch = DP832A.CHANNEL.CH3;
                    else
                        goto __param_err;

                    if (double.TryParse(m.Groups[2].Value, out double v))
                        _setOVP(ch, v);
                    else
                        goto __param_err;
                }
                else
                {
                    goto __param_err;
                }
            }
            else if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_OCP)) // Set OCP
            {
                var m = Regex.Match(param, PATTEN_CONTROL_PARAM_OCP);
                if (m.Success)
                {
                    DP832A.CHANNEL ch = DP832A.CHANNEL.CH1;

                    if (m.Groups[1].Value == "1")
                        ch = DP832A.CHANNEL.CH1;
                    else if (m.Groups[1].Value == "2")
                        ch = DP832A.CHANNEL.CH2;
                    else if (m.Groups[1].Value == "3")
                        ch = DP832A.CHANNEL.CH3;
                    else
                        goto __param_err;

                    if (double.TryParse(m.Groups[2].Value, out double v))
                        _setOCP(ch, v);
                    else
                        goto __param_err;
                }
                else
                {
                    goto __param_err;
                }
            }
            else
            {
                throw new ArgumentException($"无效的控制参数 [{param}]，请查看Usage以获取有效的参数列表。");
            }

            await Task.CompletedTask;

            return;

__param_err:
            throw new ArgumentException("控制命令参数错误。", nameof(param));


        }

        public override void Dispose()
        {
            _stopBackgroundTask();

            // disable the Rx.
            Control("OFFALL").Wait();

        }
        
        /// <summary>
        /// 获取指定通道的测量值。
        /// </summary>
        /// <param name="Channel">0至<see cref="MaxChannel"/>MaxChannel - 1</param>
        /// <returns>double</returns>
        public override object Fetch(int Channel)
        {
            if (Channel >= 0 && Channel < MaxChannel)
            {
                switch (Channel)
                {
                    case (int)REMOTE_CTRL_CH.CH1_RT_VCC:
                        return PsSingleChannel[0].RtVoltage;

                    case (int)REMOTE_CTRL_CH.CH1_RT_CURR:
                        return PsSingleChannel[0].RtCurrent;

                    case (int)REMOTE_CTRL_CH.CH2_RT_VCC:
                        return PsSingleChannel[1].RtVoltage;

                    case (int)REMOTE_CTRL_CH.CH2_RT_CURR:
                        return PsSingleChannel[1].RtCurrent;

                    case (int)REMOTE_CTRL_CH.CH3_RT_VCC:
                        return PsSingleChannel[2].RtVoltage;

                    case (int)REMOTE_CTRL_CH.CH3_RT_CURR:
                        return PsSingleChannel[2].RtCurrent;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(Channel));

                }

            }
            else
                throw new ArgumentOutOfRangeException(nameof(Channel));
        }

        public override object[] FetchAll()
        {
            // var rssi = readRssi();

            // FlushTestResult(rssi.Cast<double>().ToArray());

            List<double> ret = new List<double>();
            ret.Add(PsSingleChannel[0].RtVoltage);
            ret.Add(PsSingleChannel[0].RtCurrent);
            ret.Add(PsSingleChannel[1].RtVoltage);
            ret.Add(PsSingleChannel[1].RtCurrent);

            return ret.Cast<object>().ToArray();

        }

        public override object Fetch()
        {
            try
            {
                // var rssi = readRssi();
                // FlushTestResult(rssi.Cast<double>().ToArray());
                return PsSingleChannel[0].RtVoltage;

            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public override object DirectFetch()
        {
            return Fetch();
        }

        public override bool Init()
        {
            try
            {
                _stopBackgroundTask();

                IsInitialized = false;
                IsEnabled = false;

                // init DP831
                _init_dp800s();

                IsInitialized = true;
                IsEnabled = true;

                _startBackgroundTask(progress);

                return true;
            }
            catch (Exception ex)
            {
                // shutdown the 3.3V output.
        
                throw ex;
            }

        }

        public override void StartBackgroundTask()
        {
            // Do nothing
        }

        public override void StopBackgroundTask()
        {
            // Do nothing
        }

        private void _setOutput(DP832A.CHANNEL Channel, bool IsEnable)
        {
            if (dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            dp800.SetOutput(Channel, IsEnable);
        }

        private void _setVLevel(DP832A.CHANNEL Channel, double Voltage_V)
        {
            if(dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            dp800.SetVoltLevel(Channel, Voltage_V);

            switch(Channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(config, CFG_ITEM_VSET_1, Voltage_V);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(config, CFG_ITEM_VSET_2, Voltage_V);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(config, CFG_ITEM_VSET_3, Voltage_V);
                    break;

                default:

                    break;
            }
            
        }

        private void _setOVP(DP832A.CHANNEL Channel, double voltage)
        {
            if (dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            dp800.SetProtection(DP832A.OPMODE.OVP, Channel, voltage);

            switch (Channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(config, CFG_ITEM_OVP_1, voltage);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(config, CFG_ITEM_OVP_2, voltage);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(config, CFG_ITEM_OVP_3, voltage);
                    break;

                default:

                    break;
            }
        }

        private void _setOCP(DP832A.CHANNEL Channel, double voltage)
        {
            if (dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            dp800.SetProtection(DP832A.OPMODE.OCP, Channel, voltage);

            switch (Channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(config, CFG_ITEM_OCP_1, voltage);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(config, CFG_ITEM_OCP_2, voltage);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(config, CFG_ITEM_OCP_3, voltage);
                    break;

                default:

                    break;
            }
        }

        private void _init_dp800s()
        {
            dp800 = new DP832A();
            dp800.Init(dp800sn);

            foreach (var pschannel in PsSingleChannel)
            {
                dp800.SetProtection(DP832A.OPMODE.OVP, pschannel.BindingChannel, pschannel.OVPSet);
                dp800.SetProtection(DP832A.OPMODE.OCP, pschannel.BindingChannel, pschannel.OCPSet);
                dp800.SetCurrentLevel(pschannel.BindingChannel, pschannel.OCPSet);
                dp800.SetVoltLevel(pschannel.BindingChannel, pschannel.VoltLevelSet);
                dp800.SetOutput(pschannel.BindingChannel, false);
            }
        }

        private void _startBackgroundTask(IProgress<DP800ReadingResponse> progress = null)
        {
            if (bgTask == null || bgTask.IsCompleted)
            {
                cts = new CancellationTokenSource();
                ct = cts.Token;

                bgTask = Task.Run(() =>
                {
                    // wait for 2s to ensure the UI is initialized completely.
                    Thread.Sleep(2000);

                    while (true)
                    {
                        try
                        {
                            // check if the DP800 is initialized.
                            if (dp800 == null)
                            {
                                IsInitialized = false;
                                IsEnabled = false;
                                break;
                            }

                            foreach (var ch in PsSingleChannel)
                            {
                                // read from the DP800s
                                var isEnabled = dp800.GetOutputState(ch.BindingChannel);
                                dp800.Fetch(ch.BindingChannel);

                                // Update the UI elements in UI thread context.

                                progress?.Report(new DP800ReadingResponse()
                                {
                                    ChannelInstance = ch,
                                    IsEnabled = isEnabled,
                                    RtVoltage = dp800.MeasureValue[0],
                                    RtCurrent = dp800.MeasureValue[1],
                                    RtWatt = dp800.MeasureValue[2]
                                });
                            }
                        }
                        catch (Exception)
                        {

                        }

                        if (ct.IsCancellationRequested)
                            return;

                        Thread.Sleep(this.readIntervalms);

                        if (ct.IsCancellationRequested)
                            return;
                    }
                });
            }
        }

        private void _stopBackgroundTask()
        {
            if (bgTask != null)
            {
                // 结束背景线程
                cts?.Cancel();

                //! 延时，确保背景线程正确退出
                Thread.Sleep(500);

                bgTask = null;
            }

            this.IsInitialized = false;
            this.IsEnabled = false;
        }

        private void _loadConfigItem<T>(Configuration config, string itemName, out T holder, T defalutValue)
        {
            var cfgVal = config.AppSettings.Settings[itemName]?.Value;

            try
            {
                holder = (T)Convert.ChangeType(cfgVal, typeof(T));
            }
            catch(Exception ex)
            {
                holder = defalutValue;
                APASService?.__SSC_LogError($"Unable to load the config item [{itemName}] of plugin [{this.Name}], {ex.Message}");
            }
        }

        private void _saveConfigItem<T>(Configuration config, string itemName, T value)
        {
            var cfg = config.AppSettings.Settings[itemName];
            if(cfg == null)
            {
                config.AppSettings.Settings.Add(new KeyValueConfigurationElement(itemName, value.ToString()));
            }
            else
            {
                cfg.Value = value.ToString();
            }

            config.Save(ConfigurationSaveMode.Modified);
        }

        #endregion

        #region Commands

        public RelayCommand ReconnCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        if (cts != null)
                            cts.Cancel();

                        Init();
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show($"无法连接DP800s电源，{ex.Message}", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        #endregion
    }

    class DP800ReadingResponse
    {
        public PowerSupplyChannel ChannelInstance { get; set; }

        public bool IsEnabled { get; set; }

        public double RtVoltage { get; set; }

        public double RtCurrent { get; set; }

        public double RtWatt { get; set; }
    }
}
