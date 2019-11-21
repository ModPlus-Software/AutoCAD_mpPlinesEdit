namespace mpPlinesEdit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.Windows;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    public class PlinesEditRibbonBuilder
    {
        private static int _colorTheme = 1;

        public static void AddPanelToRibbon(bool fromInit, List<PlinesFunction> functions)
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
                {
                    MessageBox.Show(Language.GetItem(PlinesEditFunction.LangItem, "h7"), MessageBoxIcon.Close);
                }
                else
                {
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                          "\n" + Language.GetItem(PlinesEditFunction.LangItem, "h8"));
                }
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
                    if (panel.Source.Title.Equals(Language.GetItem(PlinesEditFunction.LangItem, "h9")))
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
                {
                    MessageBox.Show(Language.GetItem(PlinesEditFunction.LangItem, "h7"), MessageBoxIcon.Close);
                }
                else
                {
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                        "\n" + Language.GetItem(PlinesEditFunction.LangItem, "h8"));
                }
            }
        }

        // Проверка "загруженности" ленты
        private static bool IsLoaded()
        {
            var ribbonControl = ComponentManager.Ribbon;

            // Делаем итерацию по вкладкам ленты
            return ribbonControl.Tabs.Any(tab => tab.Id.Equals("ModPlus_ID") & tab.Title.Equals("ModPlus"));
        }

        private static void GetColorTheme()
        {
            try
            {
                var sv = Application.GetSystemVariable("COLORTHEME").ToString();
                if (int.TryParse(sv, out var i))
                    _colorTheme = i;
                else
                    _colorTheme = 1; // light
            }
            catch
            {
                _colorTheme = 1;
            }
        }

        private static void AddPanel(RibbonTab ribTab, IEnumerable<PlinesFunction> functions)
        {
            var ribbonPanelSource = new RibbonPanelSource
            {
                Title = Language.GetItem(PlinesEditFunction.LangItem, "h9")
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
                if (i % 4 == 0)
                    ribbonRowPanel.Items.Add(new RibbonRowBreak());
                ribbonRowPanel.Items.Add(AddSmallButton(plinesFunction));
                i++;
            }

            ribbonPanelSource.Items.Add(ribbonRowPanel);

            // add panel to ribbon tab
            var contain = ribTab.Panels.Any(panel => panel.Source.Title.Equals(Language.GetItem(PlinesEditFunction.LangItem, "h9")));
            if (!contain)
                ribTab.Panels.Insert(ribTab.Panels.Count - 1, ribbonPanel);
        }

        private static RibbonButton AddSmallButton(PlinesFunction function)
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
            catch (Exception)
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
                Application.DocumentManager.MdiActiveDocument.SendStringToExecute(
                    ((RibbonButton)parameter).CommandParameter + " ", true, false, false);
            }
        }
    }
}