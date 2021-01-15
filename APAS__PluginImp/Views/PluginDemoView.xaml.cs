using System.Windows;
using System.Windows.Controls;

namespace APAS__Plugin_RIGOL_DP800s.Views
{
    public partial class PluginDemoView : UserControl
    {
        public PluginDemoView()
        {
            InitializeComponent();

            // once the datacontext is set, register the corresponding event to blink the indicator.
            this.DataContextChanged += PluginDemoView_DataContextChanged;
        }

        private void PluginDemoView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PluginDemo)
            {
                var dc = e.NewValue as PluginDemo;

                dc.OnCommOneShot += (s, arg) =>
                {
                    blinkIndicator.Blink();
                };
            }
        }
    }
}
