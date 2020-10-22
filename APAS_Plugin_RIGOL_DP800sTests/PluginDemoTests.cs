using APAS_Plugin_RIGOL_DP800s;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace APAS_Plugin_RIGOL_DP800s.Tests
{
    [TestClass()]
    public class PluginDemoTests
    {
        [TestMethod()]
        public void ControlTest()
        {
            PluginDemo plug = new PluginDemo(null);
            Window win = new Window
            {
                Content = plug.UserView,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Width = 800,
                Height = 600
            };
            win.ShowDialog();
        }
    }
}