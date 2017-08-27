#if ac2010
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif ac2013
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using mpSettings;
using ModPlus;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Colors;
using mpMsg;
using System.Windows.Input;

namespace mpPlinesEdit
{
    /// <summary>
    /// Логика взаимодействия для ObjectToVxSettings.xaml
    /// </summary>
    public partial class PlinesEdit
    {

        public PlinesEdit()
        {
            InitializeComponent();
            MpWindowHelpers.OnWindowStartUp(
                this,
                MpSettings.GetValue("Settings", "MainSet", "Theme"),
                MpSettings.GetValue("Settings", "MainSet", "AccentColor"),
                MpSettings.GetValue("Settings", "MainSet", "BordersType")
                );
            BtColor.Background = new SolidColorBrush(ColorIndexToMediaColor(MpPlines.HelpGeometryColor));
            bool b;
            ChkRibbon.Checked -= ChkRibbon_OnChecked;
            ChkRibbon.Unchecked -= ChkRibbon_OnUnchecked;
            ChkRibbon.IsChecked = bool.TryParse(MpSettings.GetValue("Settings", "mpPlinesedit", "LoadRibbonPanel"), out b) && b;
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
                MpSettings.SetValue("Settings", "mpPlinesedit", "HelpGeometryColor", cd.Color.ColorIndex.ToString(CultureInfo.InvariantCulture), true);
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
            MpSettings.SetValue("Settings", "mpPlinesedit", "LoadRibbonPanel", true.ToString(), true);
            // load ribbon
            PlinesEditRibbonBuilder.AddPanelToRibbon(false, LvFunctions.ItemsSource as List<PlinesEditFunction.PlinesFunction>);
        }

        private void ChkRibbon_OnUnchecked(object sender, RoutedEventArgs e)
        {
            MpSettings.SetValue("Settings", "mpPlinesedit", "LoadRibbonPanel", false.ToString(), true);
            // unload ribbon
            PlinesEditRibbonBuilder.RemovePanelFromRibbon(false);
        }

        private void FunctionButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                var fname = button.Tag.ToString();
                Close();
                AcApp.DocumentManager.MdiActiveDocument.SendStringToExecute(
                    "_" + fname + " ", true, false, false);
            }
        }
    }

    public class PlinesEditFunction : IExtensionApplication
    {
        public static bool LoadRibbonPanel;
        public static List<PlinesFunction> Functions;

        public void Initialize()
        {
            try
            {
                // get list of functions
                Functions = GetListOfFunctions();
                
                int i;
                MpPlines.HelpGeometryColor = int.TryParse(
                    MpSettings.GetValue("Settings", "mpPlinesedit", "HelpGeometryColor"), out i)
                    ? i
                    : 150;
                // for ribbon
                ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
            }
            catch (System.Exception exception)
            {
                MpExWin.Show(exception);
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
                bool b;
                LoadRibbonPanel = bool.TryParse(MpSettings.GetValue("Settings", "mpPlinesedit", "LoadRibbonPanel"), out b) && b;
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
                LocalName = "Преобразовать 3D полилинию в LW",
                Description = "Создание копий выбранных 3D-полилиний в виде LW-полилиний (2D) с переносом в уровень 0.0",
                P2D = false,
                P3D = true,
                Plw = false
            };
            list.Add(func);
            // mpPl-VxMatchRemove
            func = new PlinesFunction
            {
                Name = "mpPl-VxMatchRemove",
                LocalName = "Удаление совпадающих вершин полилинии",
                Description = "Удаление соседних вершин выбранных полилиний, которые имеют одинаковые координаты",
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // mpPl-VxCollin
            func = new PlinesFunction
            {
                Name = "mpPl-VxCollin",
                LocalName = "Удаление вершин, лежащих на одной прямой",
                Description = "Удаление соседних вершин выбранных полилиний, которые лежат на одной прямой. Имеется возможность задать допуск на отклонение от прямой или угловой допуск",
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // mpPl-ObjectToVx
            func = new PlinesFunction
            {
                Name = "mpPl-ObjectToVx",
                LocalName = "Расположение объекта в вершинах полилинии",
                Description = "Расположение выбранного объекта в вершинах полилинии. Имеется возможность поворота объекта по сегменту полилинии. Блоки могут быть расположены как по геометрическому центру, так и по точке вставки",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Arc2Line
            func = new PlinesFunction
            {
                Name = "mpPl-Arc2Line",
                LocalName = "Замена дугового сегмента линейным",
                Description = "Замена указанного дугового сегмента полилинии линейным (замена дуги на отрезок)",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Line2Arc
            func = new PlinesFunction
            {
                Name = "mpPl-Line2Arc",
                LocalName = "Замена линейного сегмента дуговым",
                Description = "Замена указанного линейного (или дугового) сегмента полилинии дуговым (замена отрезка на дугу). Имеется возможность строить дугу по касательной или точке на дуге",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-AddVertex
            func = new PlinesFunction
            {
                Name = "mpPl-AddVertex",
                LocalName = "Динамическое добавление вершины",
                Description = "Динамическое добавление вершины к указанной полилинии",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-Rect3Pt
            func = new PlinesFunction
            {
                Name = "mpPl-Rect3Pt",
                LocalName = "Отрисовка прямоугольника по трем точкам",
                Description = "Отрисовка прямоугольника по трем точкам",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-NoArc
            func = new PlinesFunction
            {
                Name = "mpPl-NoArc",
                LocalName = "Удаление из полилинии дуговых сегментов",
                Description = "Удаление из полилинии дуговых сегментов путем замены их линейными сегментами. Имеется несколько вариантов работы функции: количество сегментов, длина сегментов, высота сегментов (отклонение хорды), длина хорды",
                P2D = false,
                P3D = false,
                Plw = true
            };
            list.Add(func);
            // mpPl-MiddleLine
            func = new PlinesFunction
            {
                Name = "mpPl-MiddleLine",
                LocalName = "Построение средней линии",
                Description = "Построение средней линии (в виде полилинии) между двумя указанными кривыми (отрезками, полилиниями или сплайнами)",
                P2D = true,
                P3D = true,
                Plw = true
            };
            list.Add(func);
            // icons
            foreach (var plinesFunction in list)
            {
                plinesFunction.ImageBig = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Icons/" + plinesFunction.Name + "_32x32.png"));
                plinesFunction.ImageSmall = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + VersionData.FuncVersion + ";component/Icons/" + plinesFunction.Name + "_16x16.png"));
            }
            return list;
        }
        private static PlinesEdit _win;
        [CommandMethod("ModPlus", "mpPlinesEdit", CommandFlags.Modal)]
        public static void Main()
        {
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
        }
    }

    public class PlinesEditRibbonBuilder
    {
        public static void AddPanelToRibbon(bool fromInit, List<PlinesEditFunction.PlinesFunction> functions)
        {
            if (IsLoaded())
            {
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
                    MpMsgWin.Show("Не найдена вкладка ModPlus");
                else AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nНе найдена вкладка ModPlus для добавления панели работы с полилиниями");
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
                    if (panel.Source.Title.Equals("Полилинии"))
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
                    MpMsgWin.Show("Не найдена вкладка ModPlus");
                else AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nНе найдена вкладка ModPlus для добавления панели работы с полилиниями");
            }
        }
        // Проверка "загруженности" ленты
        private static bool IsLoaded()
        {
            var ribCntrl = ComponentManager.Ribbon;
            // Делаем итерацию по вкладкам ленты
            return ribCntrl.Tabs.Any(tab => tab.Id.Equals("ModPlus_ID") & tab.Title.Equals("ModPlus"));
        }

        private static void AddPanel(RibbonTab ribTab, IEnumerable<PlinesEditFunction.PlinesFunction> functions)
        {
            var ribbonPanelSource = new RibbonPanelSource
            {
                Title = "Полилинии"
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
            var contain = ribTab.Panels.Any(panel => panel.Source.Title.Equals("Полилинии"));
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
                    Image = function.ImageSmall,
                    ShowImage = true,
                    ShowText = false,
                    ToolTip = ribbonToolTip
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
