
namespace mpPlinesEdit.Help
{
    using System.Windows;

    public partial class VxCollinHelp
    {
        public VxCollinHelp()
        {
            InitializeComponent();
            Title = ModPlusAPI.Language.GetItem("mpPlinesEdit", "h21");
        }

        private void BtOk_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
