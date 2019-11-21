namespace mpPlinesEdit
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Autodesk.AutoCAD.Colors;
    using Autodesk.AutoCAD.Windows;
    using ModPlusAPI;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

    public partial class PlinesEdit
    {
        public PlinesEdit()
        {
            InitializeComponent();
            Title = ModPlusAPI.Language.GetItem("mpPlinesEdit", "h1");
            BtColor.Background = new SolidColorBrush(ColorIndexToMediaColor(PlinesEditFunction.HelpGeometryColor));
            ChkRibbon.Checked -= ChkRibbon_OnChecked;
            ChkRibbon.Unchecked -= ChkRibbon_OnUnchecked;
            ChkRibbon.IsChecked = bool.TryParse(UserConfigFile.GetValue("mpPlinesedit", "LoadRibbonPanel"), out var b) && b;
            ChkRibbon.Checked += ChkRibbon_OnChecked;
            ChkRibbon.Unchecked += ChkRibbon_OnUnchecked;
        }

        // select color
        private void BtColor_OnClick(object sender, RoutedEventArgs e)
        {
            var clr = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)PlinesEditFunction.HelpGeometryColor);
            var cd = new ColorDialog
            {
                Color = clr,
                IncludeByBlockByLayer = false
            };
            if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PlinesEditFunction.HelpGeometryColor = cd.Color.ColorIndex;
                BtColor.Background = new SolidColorBrush(ColorIndexToMediaColor(cd.Color.ColorIndex));
                UserConfigFile.SetValue("mpPlinesedit", "HelpGeometryColor", cd.Color.ColorIndex.ToString(CultureInfo.InvariantCulture), true);
            }
        }
        
        public static System.Windows.Media.Color ColorIndexToMediaColor(int cla)
        {
            if (cla == 7)
                return System.Windows.Media.Color.FromArgb(255, 255, 255, 255);

            var acirgb = EntityColor.LookUpRgb((byte)cla);
            var b = (byte)acirgb;
            var g = (byte)(acirgb >> 8);
            var r = (byte)(acirgb >> 16);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }

        private void ChkRibbon_OnChecked(object sender, RoutedEventArgs e)
        {
            UserConfigFile.SetValue("mpPlinesedit", "LoadRibbonPanel", true.ToString(), true);

            // load ribbon
            PlinesEditRibbonBuilder.AddPanelToRibbon(false, LvFunctions.ItemsSource as List<PlinesFunction>);
        }

        private void ChkRibbon_OnUnchecked(object sender, RoutedEventArgs e)
        {
            UserConfigFile.SetValue("mpPlinesedit", "LoadRibbonPanel", false.ToString(), true);

            // unload ribbon
            PlinesEditRibbonBuilder.RemovePanelFromRibbon(false);
        }

        private void FunctionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var functionName = button.Tag.ToString();
                Close();
                AcApp.DocumentManager.MdiActiveDocument.SendStringToExecute(
                    "_" + functionName + " ", true, false, false);
            }
        }
    }
}
