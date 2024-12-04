using System.Windows;
using System.Windows.Controls;

namespace APAS.Plugin.RIGOL.DP800s.Views
{
    public partial class PluginDemoView : UserControl
    {
        public PluginDemoView()
        {
            InitializeComponent();
            DataContextChanged += PluginDemoView_DataContextChanged;
        }

        private void PluginDemoView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PluginDP800s dc)
            {
                dc.OnCommOneShot += (s, arg) =>
                {
                    blinkIndicator.Blink();
                };
            }
        }
    }
}
