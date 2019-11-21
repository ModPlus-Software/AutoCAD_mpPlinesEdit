namespace mpPlinesEdit.Help
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Imaging;
    using ModPlusAPI.Windows;

    public partial class NoArcSettings
    {
        private const string LangItem = "mpPlinesEdit";

        public NoArcSettings()
        {
            InitializeComponent();
        }

        private void BtOk_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CbWorkType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedIndex != -1)
            {
                try
                {
                    switch (cb.SelectedIndex)
                    {
                        case 0:
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Images/NoArc1.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = ModPlusAPI.Language.GetItem(LangItem, "h13");
                            break;
                        case 1:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Images/NoArc2.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = ModPlusAPI.Language.GetItem(LangItem, "h14");
                            break;
                        case 2:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Images/NoArc3.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = ModPlusAPI.Language.GetItem(LangItem, "h15");
                            break;
                        case 3:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Images/NoArc4.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = ModPlusAPI.Language.GetItem(LangItem, "h16");
                            break;
                    }
                }
                catch (Exception exception)
                {
                    ExceptionBox.Show(exception);
                }
            }
        }

        private void NoArcSettings_OnLoaded(object sender, RoutedEventArgs e)
        {
            CbWorkType.SelectedIndex = 0;
        }
    }
}
