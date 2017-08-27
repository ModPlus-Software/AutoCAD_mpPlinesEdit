using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModPlusAPI.Windows.Helpers;

namespace mpPlinesEdit.Help
{
    /// <summary>
    /// Логика взаимодействия для VxCollinHelp.xaml
    /// </summary>
    public partial class VxCollinHelp
    {
        public VxCollinHelp()
        {
            InitializeComponent();
            this.OnWindowStartUp();
        }

        private void BtOk_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        // Запрет пробела
        private void TboxesInterpol_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = (e.Key == Key.Space);
        }
        //  - без минуса
        private void Tb_OnlyNums_NoMinus_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var txt = ((TextBox)sender).Text + e.Text;
            e.Handled = !DoubleCharChecker(txt, false, true, null);
        }
        // Проверка, что число, точка или знак минус
        private static bool DoubleCharChecker(string str, bool checkMinus, bool checkDot, double? max)
        {
            var result = false;
            if (str.Count(c => c.Equals('.')) > 1) return false;
            if (str.Count(c => c.Equals('-')) > 1) return false;
            // Проверять нужно только последний знак в строке!!!
            var ch = str.Last();
            if (checkMinus)
                if (ch.Equals('-'))
                    result = str.IndexOf(ch) == 0;
            if (checkDot)
                if (ch.Equals('.'))
                    result = true;

            if (char.IsNumber(ch))
                result = true;
            // На "максимальность" проверяем если предыдущие провреки успешны
            if (max != null & result)
            {
                double d;
                if (double.TryParse(str, out d))
                    if (Math.Abs(d) > max) result = false;
            }

            return result;
        }
    }
}
