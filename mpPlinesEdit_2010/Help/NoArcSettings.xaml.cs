using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using mpMsg;
using mpSettings;
using ModPlus;

namespace mpPlinesEdit.Help
{
    /// <summary>
    /// Логика взаимодействия для ObjectToVxSettings.xaml
    /// </summary>
    public partial class NoArcSettings
    {
        public NoArcSettings()
        {
            InitializeComponent();
            MpWindowHelpers.OnWindowStartUp(
                this,
                MpSettings.GetValue("Settings", "MainSet", "Theme"),
                MpSettings.GetValue("Settings", "MainSet", "AccentColor"),
                MpSettings.GetValue("Settings", "MainSet", "BordersType")
                );
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
            var cb = sender as ComboBox;
            if (cb != null && cb.SelectedIndex != -1)
            {
                try
                {
                    switch (cb.SelectedIndex)
                    {
                        case 0:
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Images/NoArc1.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = "Деление дуги на равное указанное количество сегментов" + Environment.NewLine +
                                           "На изображении показан вариант деления на 3 сегмента" + Environment.NewLine +
                                           "R - радиус дуги в полилинии";
                            break;
                        case 1:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Images/NoArc2.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = "Деление дуги на сегменты путем указания длины дуговых сегментов, получающихся в результате деления" + Environment.NewLine +
                                           "R - радиус дуги в полилинии" + Environment.NewLine +
                                           "L - длина дугового сегмента";
                            break;
                        case 2:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Images/NoArc3.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = "Деление дуги на сегменты путем указания отклонения хорды (высоты сегмента)" + Environment.NewLine +
                                           "R - радиус дуги в полилинии" + Environment.NewLine +
                                           "H - высота сегмента";
                            break;
                        case 3:
                            image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri("pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Images/NoArc4.png");
                            image.EndInit();
                            Img.Source = image;
                            TbDescr.Text = "Деление дуги на сегменты путем указания длины хорды" + Environment.NewLine +
                                           "R - радиус дуги в полилинии" + Environment.NewLine +
                                           "Х - длина хорды";
                            break;
                    }
                }
                catch (Exception exception)
                {
                    MpExWin.Show(exception);
                }
            }
        }

        private void NoArcSettings_OnLoaded(object sender, RoutedEventArgs e)
        {
            CbWorkType.SelectedIndex = 0;
        }
    }
}
