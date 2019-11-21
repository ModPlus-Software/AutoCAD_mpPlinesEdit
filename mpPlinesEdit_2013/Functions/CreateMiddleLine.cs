namespace mpPlinesEdit.Functions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

    /// <summary>
    /// Построение средней линии между двумя указанными кривыми
    /// </summary>
    public class CreateMiddleLine
    {
        [CommandMethod("ModPlus", "mpPl-MiddleLine", CommandFlags.Session)]
        public static void StartFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var points = new Point2dCollection();
            var polyline = new Polyline();
            try
            {
                // Блокируем документ
                using (doc.LockDocument())
                {
                    // Стартуем транзакцию (т.к. создаем объект)
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        // Выбор первого примитива (кривой)
                        var peo = new PromptEntityOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k27") + ":")
                        {
                            AllowNone = false,
                            AllowObjectOnLockedLayer = true
                        };
                        peo.SetRejectMessage("\n" + Language.GetItem(PlinesEditFunction.LangItem, "wrong"));
                        peo.AddAllowedClass(typeof(Polyline2d), true);
                        peo.AddAllowedClass(typeof(Polyline3d), true);
                        peo.AddAllowedClass(typeof(Polyline), true);
                        peo.AddAllowedClass(typeof(Line), true);
                        peo.AddAllowedClass(typeof(Spline), true);
                        peo.AddAllowedClass(typeof(Polyline2d), true);

                        var per = ed.GetEntity(peo);
                        if (per.Status != PromptStatus.OK)
                        {
                            return;
                        }

                        var firstCurve = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                        if (firstCurve == null)
                        {
                            return;
                        }

                        // Выбор второго примитива (кривой)
                        peo.Message = "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k28") + ":";
                        per = ed.GetEntity(peo);
                        if (per.Status != PromptStatus.OK)
                        {
                            return;
                        }

                        var secondCurve = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                        if (secondCurve == null)
                        {
                            return;
                        }

                        // Количество опорных точек
                        var pio = new PromptIntegerOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k29") + ":")
                        {
                            DefaultValue =
                                int.TryParse(UserConfigFile.GetValue("mpPl_MiddleLine", "PointCount"), out var i) ? i : 100,
                            LowerLimit = 2,
                            UpperLimit = 1000
                        };

                        var pir = ed.GetInteger(pio);
                        if (pir.Status != PromptStatus.OK)
                        {
                            return;
                        }

                        var oporPtCount = pir.Value;
                        UserConfigFile.SetValue("mpPl_MiddleLine", "PointCount", oporPtCount.ToString(CultureInfo.InvariantCulture), true);

                        // Получаем коллекцию точек для построения новой полилинии

                        for (var j = 0; j < oporPtCount; j++)
                        {
                            var firstDist = (firstCurve.GetDistanceAtParameter(firstCurve.EndParam) -
                                             firstCurve.GetDistanceAtParameter(firstCurve.StartParam)) / oporPtCount;
                            var secondDist = (secondCurve.GetDistanceAtParameter(secondCurve.EndParam) -
                                              secondCurve.GetDistanceAtParameter(secondCurve.StartParam)) / oporPtCount;
                            var firstPoint =
                                firstCurve.GetPointAtParameter(firstCurve.GetParameterAtDistance(firstDist * j));

                            // var secondPoint =
                            //    secondCurve.GetPointAtParameter(secondCurve.GetParameterAtDistance(secondDist * j));

                            var secondPoint = secondCurve.GetClosestPointTo(firstPoint, false);

                            points.Add(new Point2d(
                                (firstPoint.X + secondPoint.X) / 2,
                                (firstPoint.Y + secondPoint.Y) / 2));
                        }

                        // Строим полилинию
                        if (points.Count > 0)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);

                            for (var k = 0; k < points.Count; k++)
                            {
                                polyline.AddVertexAt(k, points[k], 0.0, 0.0, 0.0);
                            }

                            btr.AppendEntity(polyline);
                            tr.AddNewlyCreatedDBObject(polyline, true);
                        }

                        tr.Commit();
                    }

                    // Запрос на упрощение полилинии
                    if (points.Count > 2)
                    {
                        if (MessageBox.ShowYesNo(Language.GetItem(PlinesEditFunction.LangItem, "k30"), MessageBoxIcon.Question))
                        {
                            RemovePointsFromPolyline(polyline.ObjectId);
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }

        private static void RemovePointsFromPolyline(ObjectId objId)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var polyline = tr.GetObject(objId, OpenMode.ForWrite) as Polyline;

                var maxA = 0.0; // Предельное отклонение угла
                var maxH = 0.0; // Предельное отклонение высоты

                var listOfDuplicateVertex = new List<int>();
                for (var i = 0; i < polyline?.NumberOfVertices - 2; i++)
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
            значит после каждого удаления нужно значение i уменьшать
            на переменную, в которую будет записано кол-во проходов
            */
                    polyline?.UpgradeOpen();
                    var j = 0;
                    foreach (var i in listOfDuplicateVertex)
                    {
                        if (j == 0)
                        {
                            polyline?.RemoveVertexAt(i);
                        }
                        else
                        {
                            polyline?.RemoveVertexAt(i - j);
                        }

                        j++;
                    }
                }

                tr.Commit();
            }
        }
    }
}
