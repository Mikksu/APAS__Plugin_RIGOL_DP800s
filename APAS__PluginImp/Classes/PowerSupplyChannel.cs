using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DP800s;

namespace APAS__Plugin_RIGOL_DP800s.Classes
{
    public class PowerSupplyChannel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;


        private double _rtVoltage;
        private double _rtCurrent;
        private double _rtWatt;
        private bool _isOutputEnabled;
        private double _ovpSet, _ocpSet, _vLevel;

        #region Constructors

        public PowerSupplyChannel(DP832A.CHANNEL BindingChannel, PluginDemo Parent)
        {
            this.Parent = Parent;
            this.BindingChannel = BindingChannel;
        }

        #endregion

        #region Properties

        public PluginDemo Parent { get; }

        public DP832A.CHANNEL BindingChannel { get; }

        public double RtVoltage
        {
            get
            {
                return _rtVoltage;
            }
            internal set
            {
                _rtVoltage = value;
                OnPropertyChange();
            }
        }


        public double RtCurrent
        {
            get
            {
                return _rtCurrent;
            }
            internal set
            {
                _rtCurrent = value;
                OnPropertyChange();
            }

        }

        public double RtWatt
        {
            get
            {
                return _rtWatt;
            }
            internal set
            {
                _rtWatt = value;
                OnPropertyChange();
            }
        }

        public double VoltLevelSet
        {
            get
            {
                return _vLevel;
            }
            set
            {
                _vLevel = value;
                OnPropertyChange();
            }
        }
        
        public double OVPSet
        {
            get
            {
                return _ovpSet;
            }
            set
            {
                _ovpSet = value;
                OnPropertyChange();
            }
        }
        public double OCPSet
        {
            get
            {
                return _ocpSet;
            }
            set
            {
                _ocpSet = value;
                OnPropertyChange();
            }
        }


        public bool IsOutputEnabled
        {
            get
            {
                return _isOutputEnabled;
            }
            internal set
            {
                _isOutputEnabled = value;
                OnPropertyChange();
            }
        }


        #endregion

        #region Methods

        public void SetVoltageLevel(double level)
        {
            Parent.Control($"VLEV {(int)BindingChannel},{VoltLevelSet:F3}").Wait();
        }

        void OnPropertyChange([CallerMemberName] string PropertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        #endregion

        #region Commands

        public RelayCommand TurnONCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Parent.Control($"ON {(int)BindingChannel}").Wait();
                    }
                    catch(AggregateException ae)
                    {
                        ae.Handle(ex =>
                        {


                            MessageBox.Show($"无法打开电源输出，{ex.Message}",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            return true;
                        });
                    }
                });
            }
        }

        public RelayCommand TurnOFFCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Parent.Control($"OFF {(int)BindingChannel}").Wait();
                    }
                    catch (AggregateException ae)
                    {
                        ae.Handle(ex =>
                        {
                            MessageBox.Show($"无法关闭电源输出，{ex.Message}",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            return true;
                        });
                    }
                });
            }
        }

        public RelayCommand SetVoltageLevelCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Parent.Control($"VLEV {(int)BindingChannel},{VoltLevelSet:F3}").Wait();
                        //if (double.TryParse(x.ToString(), out double v))
                        //    Parent.SetVLevel(BindingChannel, (double)v);
                        //else
                        //    MessageBox.Show("无法设置输出电压，输入的电压值格式错误。", "错误",
                        //        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (AggregateException ae)
                    {
                        ae.Handle(ex =>
                        {
                            MessageBox.Show($"无法设置输出电压，{ex.Message}", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return true;
                        });
                        
                    }
                    
                });
            }
        }
        

        public RelayCommand SetOVPCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Parent.Control($"OVP {(int)BindingChannel},{OVPSet:F3}").Wait();
                        //if (double.TryParse(x.ToString(), out double v))
                        //    Parent.SetOVP(BindingChannel, (double)v);
                        //else
                        //    MessageBox.Show("无法设置OVP，输入的电压值格式错误。", "错误",
                        //        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch(AggregateException ae)
                    {
                        ae.Handle(ex =>
                        {
                            MessageBox.Show($"无法设置OVP，{ex.Message}", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);

                            return true;
                        });
                        
                    }
                });
            }
        }


        public RelayCommand SetOCPCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Parent.Control($"OCP {(int)BindingChannel},{OCPSet:F3}").Wait();
                        //if (double.TryParse(x.ToString(), out double v))
                        //    Parent.SetOCP(BindingChannel, (double)v);
                        //else
                        //    MessageBox.Show("无法设置OCP，输入的电压值格式错误。", "错误",
                        //        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (AggregateException ae)
                    {
                        ae.Handle(ex =>
                        {
                            MessageBox.Show($"无法设置OCP，{ex.Message}", "错误",
                               MessageBoxButton.OK, MessageBoxImage.Error);

                            return true;
                        });


                       
                    }
                });
            }
        }

        #endregion
    }
}
