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

        public event EventHandler OnCommOneShot;

        #region Variables

        private enum REMOTE_CTRL_CH
        {
            CH1_RT_VCC,
            CH1_RT_CURR,
            CH2_RT_VCC,
            CH2_RT_CURR,
            CH3_RT_VCC,
            CH3_RT_CURR
        }

        private const DP832A.CHANNEL VCC_CH = DP832A.CHANNEL.CH1;
        private const DP832A.CHANNEL VMOD_CH = DP832A.CHANNEL.CH2;

        private const string PATTEN_CONTROL_PARAM_ON = @"^ON ([1-3]|ALL)$";
        private const string PATTEN_CONTROL_PARAM_OFF = @"^OFF ([1-3]|ALL)$";
        private const string PATTEN_CONTROL_PARAM_VLEV = @"^VLEV ([1-3]),([0-9]+\.?[0-9]*)$";
        private const string PATTEN_CONTROL_PARAM_OVP = @"^OVP ([1-3]),([0-9]+\.?[0-9]*)$";
        private const string PATTEN_CONTROL_PARAM_OCP = @"^OCP ([1-3]),([0-9]+\.?[0-9]*)$";

        private const string CFG_ITEM_OCP_1 = "DP831_OCP_A_CH1";
        private const string CFG_ITEM_OVP_1 = "DP831_OVP_V_CH1";
        private const string CFG_ITEM_VSET_1 = "DEF_VSET_CH1";
        private const string CFG_ITEM_OCP_2 = "DP831_OCP_A_CH2";
        private const string CFG_ITEM_OVP_2 = "DP831_OVP_V_CH2";
        private const string CFG_ITEM_VSET_2 = "DEF_VSET_CH2";
        private const string CFG_ITEM_OCP_3 = "DP831_OCP_A_CH3";
        private const string CFG_ITEM_OVP_3 = "DP831_OVP_V_CH3";
        private const string CFG_ITEM_VSET_3 = "DEF_VSET_CH3";

        private DP832A _dp800 = null;
        private readonly string _dp800Sn = "";


        /// <summary>
        /// how long it takes to wait between the two sampling points.
        /// </summary>
        private readonly int _pollingIntervalMs = 200;

        private Task _bgTask;
        private CancellationTokenSource _cts;
        private CancellationToken _ct;
        private bool _isInit;
        private readonly Configuration _config = null;

        private readonly IProgress<DP800ReadingResponse> _progress;

        #endregion

        #region Constructors

        public PluginDemo(ISystemService APASService) : base(Assembly.GetExecutingAssembly(), APASService)
        {
            #region Configuration Reading

            _config = GetAppConfig();

            _loadConfigItem(_config, "ReadIntervalMillisec", out _pollingIntervalMs, 200);

            _loadConfigItem(_config, "DP831_SN", out _dp800Sn, "");

            _loadConfigItem(_config, CFG_ITEM_OCP_1, out var dp831_ocp_a_ch1, 0.2);
            _loadConfigItem(_config, CFG_ITEM_OCP_2, out var dp831_ocp_a_ch2, 0.2);
            _loadConfigItem(_config, CFG_ITEM_OCP_3, out var dp831_ocp_a_ch3, 0.2);
            _loadConfigItem(_config, CFG_ITEM_OVP_1, out var dp831_ovp_v_ch1, 0.2);
            _loadConfigItem(_config, CFG_ITEM_OVP_2, out var dp831_ovp_v_ch2, 0.2);
            _loadConfigItem(_config, CFG_ITEM_OVP_3, out var dp831_ovp_v_ch3, 0.2);
            _loadConfigItem(_config, CFG_ITEM_VSET_1, out var def_vset_1, 0.0);
            _loadConfigItem(_config, CFG_ITEM_VSET_2, out var def_vset_2, 0.0);
            _loadConfigItem(_config, CFG_ITEM_VSET_3, out var def_vset_3, 0.0);

            #endregion

            this.Port = $"USB IVI,{_dp800Sn}";

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
            _progress = new Progress<DP800ReadingResponse>(x =>
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
                        SetOutput(DP832A.CHANNEL.CH1, true);
                    else if (m.Groups[1].Value == "2")
                        SetOutput(DP832A.CHANNEL.CH2, true);
                    else if (m.Groups[1].Value == "3")
                        SetOutput(DP832A.CHANNEL.CH3, true);
                    else  if(m.Groups[1].Value == "ALL")
                    {
                        SetOutput(DP832A.CHANNEL.CH1, true);
                        SetOutput(DP832A.CHANNEL.CH2, true);
                        SetOutput(DP832A.CHANNEL.CH3, true);
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
                        SetOutput(DP832A.CHANNEL.CH1, false);
                    else if (m.Groups[1].Value == "2")
                        SetOutput(DP832A.CHANNEL.CH2, false);
                    else if (m.Groups[1].Value == "3")
                        SetOutput(DP832A.CHANNEL.CH3, false);
                    else if (m.Groups[1].Value == "ALL")
                    {
                        SetOutput(DP832A.CHANNEL.CH1, false);
                        SetOutput(DP832A.CHANNEL.CH2, false);
                        SetOutput(DP832A.CHANNEL.CH3, false);
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
                var ch = DP832A.CHANNEL.CH1;
                if (m.Success)
                {
                    if (m.Groups[1].Value == "1")
                        ch = DP832A.CHANNEL.CH1;
                    else if (m.Groups[1].Value == "2")
                        ch = DP832A.CHANNEL.CH2;
                    else if (m.Groups[1].Value == "3")
                        ch = DP832A.CHANNEL.CH3;
                    else
                        goto __param_err;

                    if (double.TryParse(m.Groups[2].Value, out double v))
                        SetVLevel(ch, v);
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
                        SetOvp(ch, v);
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
                var ch = DP832A.CHANNEL.CH1;
                if (m.Success)
                {
                    if (m.Groups[1].Value == "1")
                        ch = DP832A.CHANNEL.CH1;
                    else if (m.Groups[1].Value == "2")
                        ch = DP832A.CHANNEL.CH2;
                    else if (m.Groups[1].Value == "3")
                        ch = DP832A.CHANNEL.CH3;
                    else
                        goto __param_err;

                    if (double.TryParse(m.Groups[2].Value, out double v))
                        SetOcp(ch, v);
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
        /// <param name="channel">0至<see cref="MaxChannel"/>MaxChannel - 1</param>
        /// <returns>double</returns>
        public override object Fetch(int channel)
        {
            if (channel >= 0 && channel < MaxChannel)
            {
                switch (channel)
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
                        throw new ArgumentOutOfRangeException(nameof(channel));

                }

            }
            else
                throw new ArgumentOutOfRangeException(nameof(channel));
        }

        public override object[] FetchAll()
        {
            var ret = new List<double>
            {
                PsSingleChannel[0].RtVoltage,
                PsSingleChannel[0].RtCurrent,
                PsSingleChannel[1].RtVoltage,
                PsSingleChannel[1].RtCurrent
            };

            return ret.Cast<object>().ToArray();

        }

        public override object Fetch()
        {
            try
            {
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
                Init_dp800s();

                IsInitialized = true;
                IsEnabled = true;

                _startBackgroundTask(_progress);

                return true;
            }
            catch (Exception)
            {
                throw;
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

        private void SetOutput(DP832A.CHANNEL channel, bool isEnable)
        {
            if (_dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            _dp800.SetOutput(channel, isEnable);
        }

        private void SetVLevel(DP832A.CHANNEL channel, double voltageV)
        {
            if(_dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            _dp800.SetVoltLevel(channel, voltageV);

            switch(channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(_config, CFG_ITEM_VSET_1, voltageV);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(_config, CFG_ITEM_VSET_2, voltageV);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(_config, CFG_ITEM_VSET_3, voltageV);
                    break;

                default:

                    break;
            }
            
        }

        private void SetOvp(DP832A.CHANNEL channel, double voltage)
        {
            if (_dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            _dp800.SetProtection(DP832A.OPMODE.OVP, channel, voltage);

            switch (channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(_config, CFG_ITEM_OVP_1, voltage);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(_config, CFG_ITEM_OVP_2, voltage);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(_config, CFG_ITEM_OVP_3, voltage);
                    break;

                default:

                    break;
            }
        }

        private void SetOcp(DP832A.CHANNEL channel, double voltage)
        {
            if (_dp800 == null)
                throw new NullReferenceException("DP800未初始化。");

            _dp800.SetProtection(DP832A.OPMODE.OCP, channel, voltage);

            switch (channel)
            {
                case DP832A.CHANNEL.CH1:
                    _saveConfigItem(_config, CFG_ITEM_OCP_1, voltage);
                    break;

                case DP832A.CHANNEL.CH2:
                    _saveConfigItem(_config, CFG_ITEM_OCP_2, voltage);
                    break;

                case DP832A.CHANNEL.CH3:
                    _saveConfigItem(_config, CFG_ITEM_OCP_3, voltage);
                    break;

                default:

                    break;
            }
        }

        private void Init_dp800s()
        {
            _dp800 = new DP832A();
            _dp800.Init(_dp800Sn);

            foreach (var pschannel in PsSingleChannel)
            {
                _dp800.SetProtection(DP832A.OPMODE.OVP, pschannel.BindingChannel, pschannel.OVPSet);
                _dp800.SetProtection(DP832A.OPMODE.OCP, pschannel.BindingChannel, pschannel.OCPSet);
                _dp800.SetCurrentLevel(pschannel.BindingChannel, pschannel.OCPSet);
                _dp800.SetVoltLevel(pschannel.BindingChannel, pschannel.VoltLevelSet);
                _dp800.SetOutput(pschannel.BindingChannel, false);
            }
        }

        private void _startBackgroundTask(IProgress<DP800ReadingResponse> progress = null)
        {
            if (_bgTask == null || _bgTask.IsCompleted)
            {
                _cts = new CancellationTokenSource();
                _ct = _cts.Token;

                _bgTask = Task.Run(() =>
                {
                    // wait for 2s to ensure the UI is initialized completely.
                    Thread.Sleep(2000);

                    while (true)
                    {
                        try
                        {
                            // check if the DP800 is initialized.
                            if (_dp800 == null)
                            {
                                IsInitialized = false;
                                IsEnabled = false;
                                break;
                            }

                            foreach (var ch in PsSingleChannel)
                            {
                                // read from the DP800s
                                var isEnabled = _dp800.GetOutputState(ch.BindingChannel);
                                _dp800.Fetch(ch.BindingChannel);

                                // Update the UI elements in UI thread context.

                                progress?.Report(new DP800ReadingResponse()
                                {
                                    ChannelInstance = ch,
                                    IsEnabled = isEnabled,
                                    RtVoltage = _dp800.MeasureValue[0],
                                    RtCurrent = _dp800.MeasureValue[1],
                                    RtWatt = _dp800.MeasureValue[2]
                                });
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        if (_ct.IsCancellationRequested)
                            return;

                        Thread.Sleep(this._pollingIntervalMs);

                        if (_ct.IsCancellationRequested)
                            return;
                    }
                }, _ct);
            }
        }

        private void _stopBackgroundTask()
        {
            if (_bgTask != null)
            {
                // 结束背景线程
                _cts?.Cancel();

                //! 延时，确保背景线程正确退出
                Thread.Sleep(500);

                _bgTask = null;
            }

            this.IsInitialized = false;
            this.IsEnabled = false;
        }

        private void _loadConfigItem<T>(Configuration config, string itemName, out T holder, T defaultValue)
        {
            var cfgVal = config.AppSettings.Settings[itemName]?.Value;

            try
            {
                holder = (T)Convert.ChangeType(cfgVal, typeof(T));
            }
            catch(Exception ex)
            {
                holder = defaultValue;
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
                        _cts?.Cancel();

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

    internal class DP800ReadingResponse
    {
        public PowerSupplyChannel ChannelInstance { get; set; }

        public bool IsEnabled { get; set; }

        public double RtVoltage { get; set; }

        public double RtCurrent { get; set; }

        public double RtWatt { get; set; }
    }
}
