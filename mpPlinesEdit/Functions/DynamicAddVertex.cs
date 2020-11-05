namespace mpPlinesEdit.Functions
{
    using System;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.GraphicsInterface;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;
    using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

    /// <summary>
    /// Динамическое добавление вершины
    /// </summary>
    public class DynamicAddVertex
    {
        [CommandMethod("ModPlus", "mpPl-AddVertex", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
#if !DEBUG
            Statistic.SendCommandStarting(ModPlusConnector.Instance);
#endif
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var peo = new PromptEntityOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k13")}:")
                {
                    AllowNone = false,
                    AllowObjectOnLockedLayer = true
                };
                peo.SetRejectMessage($"\n{Language.GetItem(PlinesEditFunction.LangItem, "wrong")}");
                peo.AddAllowedClass(typeof(Polyline), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    return;
                }

                var polylineId = per.ObjectId;
                var loop = true;

                using (doc.LockDocument())
                {
                    while (loop)
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(polylineId, OpenMode.ForWrite);

                            if (dbObj is Polyline polyline)
                            {
                                var jig = new AddVertexJig();
                                var jigResult = jig.StartJig(polyline);
                                if (jigResult.Status != PromptStatus.OK)
                                {
                                    loop = false;
                                }
                                else
                                {
                                    if (!polyline.IsWriteEnabled)
                                    {
                                        polyline.UpgradeOpen();
                                    }

                                    polyline.AddVertexAt(jig.Vertex() + 1, jig.PickedPoint(), 0.0, 0.0, 0.0);
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
        
        internal class AddVertexJig : DrawJig
        {
            private Point3d _prevPoint;
            private Point3d _currentPoint;
            private Point3d _startPoint;
            private Polyline _polyline;
            private int _vertex;

            public PromptResult StartJig(Polyline polyline)
            {
                _polyline = polyline;
                _prevPoint = _polyline.GetPoint3dAt(0);
                _startPoint = _polyline.GetPoint3dAt(0);

                return Application.DocumentManager.MdiActiveDocument.Editor.Drag(this);
            }

            public int Vertex()
            {
                return _vertex;
            }

            public Point2d PickedPoint()
            {
                return new Point2d(_currentPoint.X, _currentPoint.Y);
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var ppo = new JigPromptPointOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k19")}:")
                {
                    BasePoint = _startPoint,
                    UseBasePoint = true,
                    UserInputControls = UserInputControls.Accept3dCoordinates
                                        | UserInputControls.NullResponseAccepted
                                        | UserInputControls.AcceptOtherInputString
                                        | UserInputControls.NoNegativeResponseAccepted
                };

                var ppr = prompts.AcquirePoint(ppo);

                if (ppr.Status != PromptStatus.OK)
                {
                    return SamplerStatus.Cancel;
                }

                if (ppr.Status == PromptStatus.OK)
                {
                    _currentPoint = ppr.Value;

                    if (CursorHasMoved())
                    {
                        _prevPoint = _currentPoint;
                        return SamplerStatus.OK;
                    }

                    return SamplerStatus.NoChange;
                }

                return SamplerStatus.NoChange;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                var mods = System.Windows.Forms.Control.ModifierKeys;
                var control = (mods & System.Windows.Forms.Keys.Control) > 0;
                var pt = _polyline.GetClosestPointTo(_currentPoint, false);
                var param = _polyline.GetParameterAtPoint(pt);
                _vertex = Convert.ToInt32(Math.Truncate(param));
                var maxVx = _polyline.NumberOfVertices - 1;
                if (control)
                {
                    if (_vertex < maxVx)
                    {
                        _vertex++;
                    }
                }

                if (_vertex != maxVx)
                {
                    // Если вершина не последня
                    var line1 = new Line(_polyline.GetPoint3dAt(_vertex), _currentPoint)
                    {
                        ColorIndex = PlinesEditFunction.HelpGeometryColor
                    };
                    draw.Geometry.Draw(line1);
                    var line2 = new Line(_polyline.GetPoint3dAt(_vertex + 1), _currentPoint)
                    {
                        ColorIndex = PlinesEditFunction.HelpGeometryColor
                    };
                    draw.Geometry.Draw(line2);
                }
                else
                {
                    var line1 = new Line(_polyline.GetPoint3dAt(_vertex), _currentPoint)
                    {
                        ColorIndex = PlinesEditFunction.HelpGeometryColor
                    };
                    draw.Geometry.Draw(line1);
                    if (_polyline.Closed)
                    {
                        // Если полилиния замкнута, то рисуем отрезок к первой вершине
                        var line2 = new Line(_polyline.GetPoint3dAt(0), _currentPoint)
                        {
                            ColorIndex = PlinesEditFunction.HelpGeometryColor
                        };
                        draw.Geometry.Draw(line2);
                    }
                }

                return true;
            }

            private bool CursorHasMoved()
            {
                return _currentPoint.DistanceTo(_prevPoint) > Tolerance.Global.EqualPoint;
            }
        }
    }
}
