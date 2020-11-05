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
    /// Заменить линейный (или любой) сегмент дуговым
    /// </summary>
    public class ConvertSegmentLineToArc
    {
        [CommandMethod("ModPlus", "mpPl-Line2Arc", CommandFlags.UsePickSet)]
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
                var workType = "Tangent";
                while (true)
                {
                    var peo = new PromptEntityOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k16")}:")
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
                    var pickedPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(per.PickedPoint);

                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(polylineId, OpenMode.ForWrite);

                            if (dbObj is Polyline polyline)
                            {
                                var p = polyline.GetClosestPointTo(pickedPt, false);
                                var param = polyline.GetParameterAtPoint(p);
                                var vx = Convert.ToInt32(Math.Truncate(param));
                                var jig = new LineToArcSegment();
                                var jigResult = jig.StartJig(polyline, polyline.GetPoint3dAt(vx), vx, workType);
                                if (jigResult.Status != PromptStatus.OK)
                                {
                                    return;
                                }

                                workType = jig.WorkType();
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

        internal class LineToArcSegment : DrawJig
        {
            private Point3d _prevPoint;
            private Point3d _currentPoint;
            private Point3d _startPoint;
            private Polyline _polyline;
            private int _vertex;
            private string _workType;
            private double _startBulge;

            public PromptResult StartJig(Polyline polyline, Point3d fPt, int vx, string workType)
            {
                _prevPoint = fPt;
                _polyline = polyline;
                _startPoint = fPt;
                _vertex = vx;
                _startBulge = _polyline.GetBulgeAt(_vertex);
                _workType = workType;
                return Application.DocumentManager.MdiActiveDocument.Editor.Drag(this);
            }

            public string WorkType()
            {
                return _workType;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var ppo = new JigPromptPointOptions(string.Empty);
                while (true)
                {
                    if (_workType.Equals("Tangent"))
                    {
                        ppo.SetMessageAndKeywords($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k17")}", "Tangent Point");
                    }

                    if (_workType.Equals("Point"))
                    {
                        ppo.SetMessageAndKeywords($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k18")}", "Tangent Point");
                    }

                    ppo.BasePoint = _startPoint;
                    ppo.UseBasePoint = true;
                    ppo.UserInputControls = UserInputControls.Accept3dCoordinates
                                            | UserInputControls.NullResponseAccepted
                                            | UserInputControls.AcceptOtherInputString
                                            | UserInputControls.NoNegativeResponseAccepted;

                    var ppr = prompts.AcquirePoint(ppo);

                    if (ppr.Status == PromptStatus.Keyword)
                    {
                        _workType = ppr.StringResult;
                    }
                    else if (ppr.Status == PromptStatus.OK)
                    {
                        _currentPoint = ppr.Value;

                        if (CursorHasMoved())
                        {
                            _prevPoint = _currentPoint;
                            return SamplerStatus.OK;
                        }

                        return SamplerStatus.NoChange;
                    }
                    else if (ppr.Status != PromptStatus.OK | ppr.Status != PromptStatus.Keyword)
                    {
                        return SamplerStatus.Cancel;
                    }
                }
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                var line = new Line(_startPoint, _currentPoint)
                {
                    ColorIndex = PlinesEditFunction.HelpGeometryColor
                };
                draw.Geometry.Draw(line);
                _polyline.SetBulgeAt(_vertex, _startBulge);

                // polyline edit
                var tangent = _currentPoint - _startPoint;
                int? nextVertex;
                if (_vertex != _polyline.NumberOfVertices - 1)
                {
                    nextVertex = _vertex + 1;
                }
                else if (_polyline.Closed)
                {
                    nextVertex = 0;
                }
                else
                {
                    return true;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (nextVertex != null)
                {
                    var chordVector = _polyline.GetPoint3dAt(nextVertex.Value) - _startPoint;

                    // По касательной
                    if (_workType.Equals("Tangent"))
                    {
                        var bulge = Math.Tan(tangent.GetAngleTo(chordVector) / 2);
                        if (tangent.GetAngleTo(chordVector, _polyline.Normal) > Math.PI)
                        {
                            bulge = -bulge;
                        }

                        _polyline.SetBulgeAt(_vertex, bulge);
                        draw.Geometry.Draw(_polyline);
                    }

                    // По точке прохождения
                    else if (_workType.Equals("Point"))
                    {
                        // Строим вспомогательную геометрию в виде дуги для получения полного угла
                        var cArc = new CircularArc3d(_startPoint, _currentPoint, _polyline.GetPoint3dAt(nextVertex.Value));
                        var angle = cArc.ReferenceVector.AngleOnPlane(new Plane(cArc.Center, cArc.Normal));
                        var arc = new Arc(cArc.Center, cArc.Normal, cArc.Radius,
                            cArc.StartAngle + angle, cArc.EndAngle + angle);

                        var bulge = Math.Tan(arc.TotalAngle / 4);
                        if (tangent.GetAngleTo(chordVector, _polyline.Normal) > Math.PI)
                        {
                            bulge = -bulge;
                        }

                        _polyline.SetBulgeAt(_vertex, bulge);
                        draw.Geometry.Draw(_polyline);
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
