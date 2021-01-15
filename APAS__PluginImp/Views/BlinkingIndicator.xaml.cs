using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace APAS__Plugin_RIGOL_DP800s.Views
{
    /// <summary>
    /// Interaction logic for BlinkingIndicator.xaml
    /// </summary>
    public partial class BlinkingIndicator : UserControl
    {
        public BlinkingIndicator()
        {
            InitializeComponent();
        }

        public void Blink()
        {
            Storyboard sb = this.FindResource("sbdBlinking") as Storyboard;
            Storyboard.SetTarget(sb, this.brd);
            sb.Begin();
        }
    }
}
