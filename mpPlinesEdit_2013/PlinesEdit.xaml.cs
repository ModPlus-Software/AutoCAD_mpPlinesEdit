using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Colors;
using System.Windows.Input;
using ModPlusAPI;
using ModPlusAPI.Windows;

namespace mpPlinesEdit
{
    public partial class PlinesEdit
    {

        public PlinesEdit()
        {
            InitializeComponent();
            Title = ModPlusAPI.Language.GetItem("mpPlinesEdit", "h1");
            BtColor.Background = new SolidColorBrush(ColorIndexToMediaColor(MpPlines.HelpGeometryColor));
            ChkRibbon.Checked -= ChkRibbon_OnChecked;
            ChkRibbon.Unchecked -= ChkRibbon_OnUnchecked;
            ChkRibbon.IsChecked = bool.TryParse(UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "LoadRibbonPanel"), out var b) && b;
            ChkRibbon.Checked += ChkRibbon_OnChecked;
            ChkRibbon.Unchecked += ChkRibbon_OnUnchecked;
        }
        // select color
        private void BtColor_OnClick(object sender, RoutedEventArgs e)
        {
            var clr = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)MpPlines.HelpGeometryColor);
            var cd = new ColorDialog
            {
                Color = clr,
                IncludeByBlockByLayer = false
            };
            if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MpPlines.HelpGeometryColor = cd.Color.ColorIndex;
                BtColor.Background = new SolidColorBrush(ColorIndexToMediaColor(cd.Color.ColorIndex));
                UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "HelpGeometryColor", cd.Color.ColorIndex.ToString(CultureInfo.InvariantCulture), true);
            }
        }

        public static short MediaColorToColorIndex(System.Windows.Media.Color clm)
        {
            return EntityColor.LookUpAci(clm.R, clm.G, clm.B);
        }

        public static System.Windows.Media.Color ColorIndexToMediaColor(int cla)
        {
            if (cla == 7) return System.Windows.Media.Color.FromArgb(255, 255, 255, 255);

            var acirgb = EntityColor.LookUpRgb((byte)cla);
            var b = (byte)(acirgb);
            var g = (byte)(acirgb >> 8);
            var r = (byte)(acirgb >> 16);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }

        private void ChkRibbon_OnChecked(object sender, RoutedEventArgs e)
        {
            UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "LoadRibbonPanel", true.ToString(), true);
            // load ribbon
            PlinesEditRibbonBuilder.AddPanelToRibbon(false, LvFunctions.ItemsSource as List<PlinesEditFunction.PlinesFunction>);
        }

        private void ChkRibbon_OnUnchecked(object sender, RoutedEventArgs e)
        {
            UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "LoadRibbonPanel", false.ToString(), true);
            // unload ribbon
            PlinesEditRibbonBuilder.RemovePanelFromRibbon(false);
        }

        private void FunctionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var fname = button.Tag.ToString();
                Close();
                AcApp.DocumentManager.MdiActiveDocument.SendStringToExecute(
                    "_" + fname + " ", true, false, false);
            }
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }

    public class PlinesEditFunction : IExtensionApplication
    {
        private const string LangItem = "mpPlinesEdit";

        public static bool LoadRibbonPanel;
        public static List<PlinesFunction> Functions;

        public void Initialize()
        {
            try
            {
                // get list of functions
                Functions = GetListOfFunctions();

                MpPlines.HelpGeometryColor = int.TryParse(
                    UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "HelpGeometryColor"), out var i)
                    ? i
                    : 150;
                // for ribbon
                ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
                AcApp.SystemVariableChanged += AcApp_SystemVariableChanged;
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }

        private void AcApp_SystemVariableChanged(object sender, Autodesk.AutoCAD.ApplicationServices.SystemVariableChangedEventArgs e)
        {
            if (e.Name.Equals("COLORTHEME"))
            {
                if (ComponentManager.Ribbon != null)
                {
                    LoadRibbonPanel = bool.TryParse(UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "LoadRibbonPanel"), out var b) && b;
                    if (LoadRibbonPanel)
                    {
                        PlinesEditRibbonBuilder.RemovePanelFromRibbon(false);
                        PlinesEditRibbonBuilder.AddPanelToRibbon(false, Functions);
                    }
                }
            }
        }

        public void Terminate()
        {
            // nothing
        }
        /* Обработчик события
         * Следит за событиями изменения окна автокада.
         * Используем его для того, чтобы "поймать" момент построения ленты,
         * учитывая, что наш плагин уже инициализировался
         */
        void ComponentManager_ItemInitialized(object sender, RibbonItemEventArgs e)
        {
            // Проверяем, что лента загружена
            if (ComponentManager.Ribbon != null)
            {
                LoadRibbonPanel = bool.TryParse(UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpPlinesedit", "LoadRibbonPanel"), out var b) && b;
                // Строим нашу вкладку
                // Ribbon
                if (LoadRibbonPanel)
                {
                    PlinesEditRibbonBuilder.AddPanelToRibbon(true, Functions);
                }
                //и раз уж лента запустилась, то отключаем обработчик событий
                //ComponentManager.ItemInitialized -= ComponentManager_ItemInitialized;
            }
        }
        private static List<PlinesFunction> GetListOfFunctions()
        {
            var list = new List<PlinesFunction>();
            // mpPl-3Dto2D
            var func = new PlinesFunction
            {
                Name = "mpPl-3Dto2D",
                LocalName = Language.GetItem(LangItem, "l1"),
                Description = Language.GetItem(LangItem, "d1"),
                P2D = false,
                P3D = true,
                Plw = false
            };
            list.Add(func);
            // mpPl-VxMatchRemove
            func = new PlinesFunction
            {
                Name = "mpPl-VxMatchRemove",
                LocalName = Language.GetItem(LangItem, "l2"),
                Description = Language.GetItem(LangItem, "d2"),
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // mpPl-VxCollin
            func = new PlinesFunction
            {
                Name = "mpPl-VxCollin",
                LocalName = Language.GetItem(LangItem, "l3"),
                Description = Language.GetItem(LangItem, "d3"),
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // mpPl-ObjectToVx
            func = new PlinesFunction
            {
                Name = "mpPl-ObjectToVx",
                LocalName = Language.GetItem(LangItem, "l4"),
                Description = Language.GetItem(LangItem, "d4"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Arc2Line
            func = new PlinesFunction
            {
                Name = "mpPl-Arc2Line",
                LocalName = Language.GetItem(LangItem, "l5"),
                Description = Language.GetItem(LangItem, "d5"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Line2Arc
            func = new PlinesFunction
            {
                Name = "mpPl-Line2Arc",
                LocalName = Language.GetItem(LangItem, "l6"),
                Description = Language.GetItem(LangItem, "d6"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-AddVertex
            func = new PlinesFunction
            {
                Name = "mpPl-AddVertex",
                LocalName = Language.GetItem(LangItem, "l7"),
                Description = Language.GetItem(LangItem, "d8"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Rect3Pt
            func = new PlinesFunction
            {
                Name = "mpPl-Rect3Pt",
                LocalName = Language.GetItem(LangItem, "l8"),
                Description = Language.GetItem(LangItem, "l8"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-NoArc
            func = new PlinesFunction
            {
                Name = "mpPl-NoArc",
                LocalName = Language.GetItem(LangItem, "l9"),
                Description = Language.GetItem(LangItem, "d9"),
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-MiddleLine
            func = new PlinesFunction
            {
                Name = "mpPl-MiddleLine",
                LocalName = Language.GetItem(LangItem, "l10"),
                Description = Language.GetItem(LangItem, "d10"),
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // icons
            foreach (var plinesFunction in list)
            {
                plinesFunction.ImageBig = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Icons/" + plinesFunction.Name + "_32x32.png"));
                plinesFunction.ImageSmall = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Icons/" + plinesFunction.Name + "_16x16.png"));
                plinesFunction.ImageDarkSmall = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + ";component/Icons/" + plinesFunction.Name + "_16x16_dark.png"));
            }
            return list;
        }
        private static PlinesEdit _win;
        [CommandMethod("ModPlus", "mpPlinesEdit", CommandFlags.Modal)]
        public static void Main()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            if (_win != null)
            {
                _win.Activate();
            }
            else
            {
                _win = new PlinesEdit();
                _win.Closed += Win_Closed;
                _win.LvFunctions.ItemsSource = Functions;
                AcApp.ShowModalWindow(AcApp.MainWindow.Handle, _win, false);
            }
        }

        private static void Win_Closed(object sender, EventArgs e)
        {
            _win = null;
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
        }

        public class PlinesFunction
        {
            public string Name { get; set; }
            public string LocalName { get; set; }
            public string Description { get; set; }
            public bool Plw { get; set; }
            public bool P3D { get; set; }
            public bool P2D { get; set; }
            public BitmapImage ImageBig { get; set; }
            public BitmapImage ImageSmall { get; set; }
            public BitmapImage ImageDarkSmall { get; set; }
        }
    }

    public class PlinesEditRibbonBuilder
    {
        private const string LangItem = "mpPlinesEdit";

        public static void AddPanelToRibbon(bool fromInit, List<PlinesEditFunction.PlinesFunction> functions)
        {
            if (IsLoaded())
            {
                GetColorTheme();
                // get ribbon
                var ribbon = ComponentManager.Ribbon;
                // get tab
                var ribbonTab = ribbon.FindTab("ModPlus_ID");

                // add panel
                AddPanel(ribbonTab, functions);
                // update
                ribbon.UpdateLayout();
            }
            else
            {
                if (!fromInit)
                    ModPlusAPI.Windows.MessageBox.Show(Language.GetItem(LangItem, "h7"), MessageBoxIcon.Close);
                else AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + Language.GetItem(LangItem, "h8"));
            }
        }

        public static void RemovePanelFromRibbon(bool fromInit)
        {
            if (IsLoaded())
            {
                // get ribbon
                var ribbon = ComponentManager.Ribbon;
                // get tab
                var ribbonTab = ribbon.FindTab("ModPlus_ID");
                foreach (var panel in ribbonTab.Panels)
                {
                    if (panel.Source.Title.Equals(Language.GetItem(LangItem, "h9")))
                    {
                        ribbonTab.Panels.Remove(panel);
                        break;
                    }
                }
                // update
                ribbon.UpdateLayout();
            }
            else
            {
                if (!fromInit)
                    ModPlusAPI.Windows.MessageBox.Show(Language.GetItem(LangItem, "h7"), MessageBoxIcon.Close);
                else AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + Language.GetItem(LangItem, "h8"));
            }
        }
        // Проверка "загруженности" ленты
        private static bool IsLoaded()
        {
            var ribCntrl = ComponentManager.Ribbon;
            // Делаем итерацию по вкладкам ленты
            return ribCntrl.Tabs.Any(tab => tab.Id.Equals("ModPlus_ID") & tab.Title.Equals("ModPlus"));
        }

        private static int _colorTheme = 1;

        private static void GetColorTheme()
        {
            try
            {
                var sv = AcApp.GetSystemVariable("COLORTHEME").ToString();
                if (int.TryParse(sv, out var i))
                    _colorTheme = i;
                else _colorTheme = 1; // light
            }
            catch
            {
                _colorTheme = 1;
            }
        }

        private static void AddPanel(RibbonTab ribTab, IEnumerable<PlinesEditFunction.PlinesFunction> functions)
        {
            var ribbonPanelSource = new RibbonPanelSource
            {
                Title = Language.GetItem(LangItem, "h9")
            };
            var ribbonPanel = new RibbonPanel
            {
                Source = ribbonPanelSource
            };
            // add buttons
            var i = 0;
            var ribbonRowPanel = new RibbonRowPanel();
            foreach (var plinesFunction in functions)
            {
                if (i%4 == 0)
                    ribbonRowPanel.Items.Add(new RibbonRowBreak());
                ribbonRowPanel.Items.Add(AddSmallButton(plinesFunction));
                i++;
            }
            ribbonPanelSource.Items.Add(ribbonRowPanel);
            // add panel to ribbon tab
            var contain = ribTab.Panels.Any(panel => panel.Source.Title.Equals(Language.GetItem(LangItem, "h9")));
            if (!contain)
                ribTab.Panels.Insert(ribTab.Panels.Count - 1, ribbonPanel);
        }
        private static RibbonButton AddSmallButton(PlinesEditFunction.PlinesFunction function)
        {
            try
            {
                var ribbonToolTip = new RibbonToolTip
                {
                    IsHelpEnabled = false,
                    Content = function.Description,
                    Command = function.Name,
                    Title = function.LocalName
                };
                var ribbonButton = new RibbonButton
                {
                    CommandParameter = function.Name,
                    Name = function.Name,
                    CommandHandler = new RibbonCommandHandler(),
                    Orientation = Orientation.Horizontal,
                    Size = RibbonItemSize.Standard,
                    ShowImage = true,
                    ShowText = false,
                    ToolTip = ribbonToolTip,
                    Image = _colorTheme == 1 ? function.ImageSmall : function.ImageDarkSmall
                };
                return ribbonButton;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
        private class RibbonCommandHandler : ICommand
        {
            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                if (!(parameter is RibbonButton))
                    return;
                AcApp.DocumentManager.MdiActiveDocument.SendStringToExecute(
                    ((RibbonButton)parameter).CommandParameter + " ", true, false, false);
            }
        }
    }
}
