using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using APAS__Plugin_RIGOL_DP800s.Classes;

namespace APAS__Plugin_RIGOL_DP800s.Views
{
    /// <summary>
    /// Interaction logic for NumericInputDialog.xaml
    /// </summary>
    public partial class NumericInputDialog : Window
    {
        public NumericInputDialog()
        {
            InitializeComponent();
        }

        public PowerSupplyChannel TargetPsChannel
        {
            get; set;
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void btnSet_Click(object sender, RoutedEventArgs e)
        {
            if(double.TryParse(txtValue.Text, out double val))
            {
                TargetPsChannel.SetVoltageLevel(val);
            }
            else
            {
                MessageBox.Show(
                    $"输入的数据格式错误。", "错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }

            
        }
    }
}
