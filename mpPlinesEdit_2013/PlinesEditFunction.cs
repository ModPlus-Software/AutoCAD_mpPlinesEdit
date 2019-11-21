/*
Полилинии бывают трех видов:
- Optimized (or "lightweight") 2D polylines
- Old-format (or "heavyweight") 2D polylines
- 3D polylines
*/
namespace mpPlinesEdit
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media.Imaging;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.Runtime;
    using Autodesk.Windows;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    public class PlinesEditFunction : IExtensionApplication
    {
        private PlinesEdit _win;
        private static bool _loadRibbonPanel;

        /// <summary>
        /// Language item key
        /// </summary>
        public static string LangItem => "mpPlinesEdit";

        /// <summary>
        /// Цвет вспомогательной линии
        /// </summary>
        public static int HelpGeometryColor = 150;

        /// <inheritdoc />
        public void Initialize()
        {
            try
            {
                HelpGeometryColor = int.TryParse(
                    UserConfigFile.GetValue("mpPlinesedit", "HelpGeometryColor"), out var i)
                    ? i
                    : 150;

                // for ribbon
                ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
                Application.SystemVariableChanged += AcApp_SystemVariableChanged;
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
                    _loadRibbonPanel = bool.TryParse(UserConfigFile.GetValue("mpPlinesedit", "LoadRibbonPanel"), out var b) && b;
                    if (_loadRibbonPanel)
                    {
                        PlinesEditRibbonBuilder.RemovePanelFromRibbon(false);
                        PlinesEditRibbonBuilder.AddPanelToRibbon(false, GetListOfFunctions());
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Terminate()
        {
            // nothing
        }

        /* Обработчик события
         * Следит за событиями изменения окна автокада.
         * Используем его для того, чтобы "поймать" момент построения ленты,
         * учитывая, что наш плагин уже инициализировался
         */
        private void ComponentManager_ItemInitialized(object sender, RibbonItemEventArgs e)
        {
            // Проверяем, что лента загружена
            if (ComponentManager.Ribbon != null)
            {
                _loadRibbonPanel = bool.TryParse(UserConfigFile.GetValue("mpPlinesedit", "LoadRibbonPanel"), out var b) && b;

                // Строим нашу вкладку
                // Ribbon
                if (_loadRibbonPanel)
                {
                    PlinesEditRibbonBuilder.AddPanelToRibbon(true, GetListOfFunctions());
                }

                // и раз уж лента запустилась, то отключаем обработчик событий
                // ComponentManager.ItemInitialized -= ComponentManager_ItemInitialized;
            }
        }

        public List<PlinesFunction> GetListOfFunctions()
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
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + 
                    ";component/Icons/" + plinesFunction.Name + "_32x32.png"));
                plinesFunction.ImageSmall = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + 
                    ";component/Icons/" + plinesFunction.Name + "_16x16.png"));
                plinesFunction.ImageDarkSmall = new BitmapImage(new Uri(
                    "pack://application:,,,/mpPlinesEdit_" + ModPlusConnector.Instance.AvailProductExternalVersion + 
                    ";component/Icons/" + plinesFunction.Name + "_16x16_dark.png"));
            }

            return list;
        }

        [CommandMethod("ModPlus", "mpPlinesEdit", CommandFlags.Modal)]
        public void StartMainFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());

            if (_win != null)
            {
                _win.Activate();
            }
            else
            {
                _win = new PlinesEdit();
                _win.Closed += (sender, args) =>
                {
                    _win = null;
                    Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
                };
                _win.LvFunctions.ItemsSource = GetListOfFunctions();
                Application.ShowModalWindow(Application.MainWindow.Handle, _win, false);
            }
        }
    }
}