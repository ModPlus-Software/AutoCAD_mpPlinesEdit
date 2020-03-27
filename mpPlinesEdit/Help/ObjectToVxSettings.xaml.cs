namespace mpPlinesEdit.Help
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using ModPlusAPI;

    public partial class ObjectToVxSettings
    {
        public ObjectToVxSettings()
        {
            InitializeComponent();

            ChkExcludeFirstAndLastPt.IsChecked =
                bool.TryParse(UserConfigFile.GetValue("PlObjectToVx", "ExcludeFirstAndLast"), out var b) && b;
            CbCopyBlockBy.SelectedIndex =
                int.TryParse(UserConfigFile.GetValue("PlObjectToVx", "CopyBlockBy"), out var i) ? i : 0;
            CbRotateBy.SelectedIndex =
                int.TryParse(UserConfigFile.GetValue("PlObjectToVx", "RotateBy"), out i) ? i : 0;
        }

        private void BtOk_OnClick(object sender, RoutedEventArgs e)
        {
            UserConfigFile.SetValue("PlObjectToVx", "ExcludeFirstAndLast", ChkExcludeFirstAndLastPt.IsChecked.ToString(), false);
            UserConfigFile.SetValue("PlObjectToVx", "CopyBlockBy", CbCopyBlockBy.SelectedIndex.ToString(), false);
            UserConfigFile.SetValue("PlObjectToVx", "RotateBy", CbRotateBy.SelectedIndex.ToString(), false);
            UserConfigFile.SaveConfigFile();
            DialogResult = true;
        }

        private void BtCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ChkExcludeFirstAndLastPt_OnChecked(object sender, RoutedEventArgs e)
        {
            ImgLeft.Visibility = Visibility.Hidden;
            ImgRight.Visibility = Visibility.Hidden;
        }

        private void ChkExcludeFirstAndLastPt_OnUnchecked(object sender, RoutedEventArgs e)
        {
            ImgLeft.Visibility = Visibility.Visible;
            ImgRight.Visibility = Visibility.Visible;
        }

        private void CbRotateBy_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            var index = cb?.SelectedIndex;
            if (index.Equals(0))
            {
                var rt = new RotateTransform(0);
                ImgLeft.RenderTransform = ImgObj1.RenderTransform = ImgObj2.RenderTransform = ImgObj3.RenderTransform = ImgRight.RenderTransform = rt;
            }
            else if (index.Equals(1))
            {
                var rtLeft = new RotateTransform(-45);
                var rtRight = new RotateTransform(45);
                ImgLeft.RenderTransform = ImgObj1.RenderTransform = ImgObj3.RenderTransform = ImgRight.RenderTransform = rtLeft;
                ImgObj2.RenderTransform = rtRight;
            }
            else if (index.Equals(2))
            {
                var rtLeft = new RotateTransform(-45);
                var rtRight = new RotateTransform(45);
                ImgLeft.RenderTransform = ImgObj3.RenderTransform = ImgRight.RenderTransform = rtLeft;
                ImgObj1.RenderTransform = ImgObj2.RenderTransform = rtRight;
            }
        }
    }
}
