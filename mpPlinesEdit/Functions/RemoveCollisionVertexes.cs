namespace mpPlinesEdit.Functions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using Help;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Удаление вершин, лежащих на одной прямой
    /// </summary>
    public class RemoveCollisionVertexes
    {
        [CommandMethod("ModPlus", "mpPl-VxCollin", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var maxA = 0.0; // Предельное отклонение угла
            var maxH = 0.0; // Предельное отклонение высоты

            var pso = new PromptSelectionOptions
            {
                AllowDuplicates = false
            };
            pso.Keywords.Add("Dop", Language.GetItem(PlinesEditFunction.LangItem, "k7"));

            // Set our prompts to include our keywords
            var kws = pso.Keywords.GetDisplayString(true);
            pso.MessageForAdding = "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k6") + ":" + kws;
            pso.KeywordInput +=
                (sender, e) =>
                {
                    if (e.Input.Equals("Dop"))
                    {
                        var win = new VxCollinHelp
                        {
                            TbMaxA =
                            {
                                Value = double.TryParse(
                                    ModPlus.Helpers.XDataHelpers.GetStringXData("mpPl_VxCollin_maxA"), 
                                    out var d) ? d : 0.0
                            },
                            TbMaxH =
                            {
                                Value = double.TryParse(
                                    ModPlus.Helpers.XDataHelpers.GetStringXData("mpPl_VxCollin_maxH"), 
                                    out d) ? d : 0.0
                            }
                        };
                        if (win.ShowDialog() == true)
                        {
                            maxH = win.TbMaxH.Value ?? 0.0;
                            ModPlus.Helpers.XDataHelpers.SetStringXData("mpPl_VxCollin_maxH", maxH.ToString(CultureInfo.InvariantCulture));
                            ed.WriteMessage("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k8") + ":" + maxH);
                            maxA = win.TbMaxA.Value ?? 0.0;
                            ModPlus.Helpers.XDataHelpers.SetStringXData("mpPl_VxCollin_maxA", maxA.ToString(CultureInfo.InvariantCulture));
                            ed.WriteMessage("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k9") + ":" + maxA);
                        }
                    }
                };

            try
            {
                var filList = new[] { new TypedValue((int)DxfCode.Start, "*POLYLINE") };
                var sf = new SelectionFilter(filList);

                var psr = ed.GetSelection(pso, sf);
                if (psr.Status == PromptStatus.OK)
                {
                    var ids = psr.Value.GetObjectIds();
                    var plCount = psr.Value.Count;
                    /*Сначала нужно пройти по полилинии
                    и получить коллекцию вершин для удаления
                    а потом уже удалить
                    В принципе - по аналогии удаления совпадающих вершин
                    только без проверки на скругление перед удалением
                    */
                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            foreach (var objectId in ids)
                            {
                                var dbObj = tr.GetObject(objectId, OpenMode.ForRead);
                                var deleted = 0;

                                // Если простая полилиния
                                if (dbObj is Polyline polyline)
                                {
                                    var listOfDuplicateVertex = new List<int>();
                                    if (polyline.NumberOfVertices <= 2)
                                    {
                                        if (plCount == 1)
                                        {
                                            MessageBox.Show(
                                                Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + polyline.NumberOfVertices + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }
                                        else
                                        {
                                            ed.WriteMessage("\n" +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + polyline.NumberOfVertices + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }

                                        continue;
                                    }

                                    for (var i = 0; i < polyline.NumberOfVertices - 2; i++)
                                    {
                                        // Если в текущей вершине или следующей
                                        // есть скругление, то пропускаем
                                        if (!polyline.GetBulgeAt(i).Equals(0.0))
                                        {
                                            continue;
                                        }

                                        if (!polyline.GetBulgeAt(i + 1).Equals(0.0))
                                        {
                                            continue;
                                        }

                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = polyline.GetPoint2dAt(i); // первая
                                        var pt2 = polyline.GetPoint2dAt(i + 1); // вторая (средняя)
                                        var pt3 = polyline.GetPoint2dAt(i + 2); // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                        {
                                            listOfDuplicateVertex.Add(i + 1);
                                        }
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                            {
                                                listOfDuplicateVertex.Add(i + 1);
                                            }

                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                            {
                                                listOfDuplicateVertex.Add(i + 1);
                                            }
                                        }
                                    }

                                    if (listOfDuplicateVertex.Count > 0)
                                    {
                                        /*
                                    при каждом удалении вершины количество вершин меняется
                                    значит после каждого удаления нужно нужно значение i уменьшать
                                    на переменную, в которую будет записано кол-во проходов
                                    */
                                        polyline.UpgradeOpen();
                                        var j = 0;
                                        foreach (var i in listOfDuplicateVertex)
                                        {
                                            if (j == 0)
                                            {
                                                polyline.RemoveVertexAt(i);
                                            }
                                            else
                                            {
                                                polyline.RemoveVertexAt(i - j);
                                            }

                                            j++;
                                        }

                                        deleted = listOfDuplicateVertex.Count;
                                    }
                                }
                                else if (dbObj is Polyline2d polyline2d)
                                {
                                    var vertexes =
                                        (from ObjectId vId in polyline2d
                                            select (Vertex2d)tr.GetObject(vId, OpenMode.ForRead))
                                        .ToList();
                                    if (vertexes.Count <= 2)
                                    {
                                        if (plCount == 1)
                                        {
                                            MessageBox.Show(
                                                Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + vertexes.Count + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }
                                        else
                                        {
                                            ed.WriteMessage("\n" +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + vertexes.Count + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }

                                        continue;
                                    }

                                    for (var i = 0; i < vertexes.Count - 2; i++)
                                    {
                                        // Если в текущей вершине или следующей
                                        // есть скругление, то пропускаем
                                        if (!vertexes[i].Bulge.Equals(0.0))
                                        {
                                            continue;
                                        }

                                        if (!vertexes[i + 1].Bulge.Equals(0.0))
                                        {
                                            continue;
                                        }

                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = vertexes[i].Position; // первая
                                        var pt2 = vertexes[i + 1].Position; // вторая (средняя)
                                        var pt3 = vertexes[i + 2].Position; // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                        {
                                            vertexes[i + 1].UpgradeOpen();
                                            vertexes[i + 1].Erase(true);
                                            deleted++;
                                        }
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }

                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                        }
                                    }

                                    if (deleted > 0)
                                    {
                                        polyline2d.UpgradeOpen();
                                        polyline2d.RecordGraphicsModified(true);
                                    }
                                }
                                else if (dbObj is Polyline3d polyline3d)
                                {
                                    var vertexes =
                                        (from ObjectId vId in polyline3d
                                            select (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead)).ToList();
                                    if (vertexes.Count <= 2)
                                    {
                                        if (plCount == 1)
                                        {
                                            MessageBox.Show(
                                                Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + vertexes.Count + " " +
                                                Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }
                                        else
                                        {
                                            ed.WriteMessage("\n" +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k10") + " " + vertexes.Count + " " +
                                                            Language.GetItem(PlinesEditFunction.LangItem, "k11") + "!");
                                        }

                                        continue;
                                    }

                                    for (var i = 0; i < vertexes.Count - 2; i++)
                                    {
                                        // 3d полилиния не содержит дуг!
                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = vertexes[i].Position; // первая
                                        var pt2 = vertexes[i + 1].Position; // вторая (средняя)
                                        var pt3 = vertexes[i + 2].Position; // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                        {
                                            vertexes[i + 1].UpgradeOpen();
                                            vertexes[i + 1].Erase(true);
                                            deleted++;
                                        }
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }

                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                        }
                                    }

                                    if (deleted > 0)
                                    {
                                        polyline3d.UpgradeOpen();
                                        polyline3d.RecordGraphicsModified(true);
                                    }
                                }

                                // Вывод результата
                                if (deleted > 0)
                                {
                                    if (plCount == 1)
                                    {
                                        MessageBox.Show(Language.GetItem(PlinesEditFunction.LangItem, "k3") + ":" + deleted);
                                    }
                                    else
                                    {
                                        ed.WriteMessage("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                                        Language.GetItem(PlinesEditFunction.LangItem, "k3").ToLower() + ":" + deleted);
                                    }
                                }
                                else
                                {
                                    if (plCount == 1)
                                    {
                                        MessageBox.Show(
                                            Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " +
                                            Language.GetItem(PlinesEditFunction.LangItem, "k12"));
                                    }
                                    else
                                    {
                                        ed.WriteMessage("\n" +
                                                        Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                                        Language.GetItem(PlinesEditFunction.LangItem, "k12"));
                                    }
                                }
                            }

                            tr.Commit();
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }
    }
}
