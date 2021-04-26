/*
    Copyright (c) 2017 Felix Rampf - <f.rampf@tum.de>
   
    C3DIfcAlignmentPlugIn is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    C3DIfcAlignmentPlugIn is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with C3DIfcAlignmentPlugIn.  If not, see <http://www.gnu.org/licenses/>.
*/



using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Private.Windows;
using Autodesk.Windows;
using Microsoft.Win32;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Surface = Autodesk.Civil.DatabaseServices.Surface;
using Autodesk.AutoCAD.Geometry;
using Ifc4x1;
using C3DIfcAlignmentPlugIn.Forms;


namespace C3DIfcAlignmentPlugIn.PlugIn
{
    public class C3DIfcAlignmentAddIn
    {
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++  PLUGIN Methods  ++++++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        
        private BitmapImage GetBitmap(string fileName)
        {
            var bmp = new BitmapImage();
            // BitmapImage.UriSource must be in a BeginInit/EndInit block.             
            bmp.BeginInit();
            bmp.UriSource = new Uri(
                string.Format("pack://application:,,,/{0};component/{1}",
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, fileName));
            bmp.EndInit();
            return bmp;
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //++++++++++++++++++++++++++++++   EXPORT Methods +++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private static IfcBoolean CheckForConvexity(ProfileEntity entity)
        {
            bool isConvex = false;
            switch (entity.EntityType) //if Element-Type is a...
            {
                case ProfileEntityType.ParabolaAsymmetric:
                    var parabola1 = (ProfileParabolaAsymmetric)entity;
                    if (parabola1.CurveType == VerticalCurveType.Sag)
                    {
                        isConvex = true;
                    }
                    break;
                case ProfileEntityType.ParabolaSymmetric:
                    var parabola2 = (ProfileParabolaSymmetric)entity;
                    if (parabola2.CurveType == VerticalCurveType.Sag)
                    {
                        isConvex = true;
                    }
                    break;
                case ProfileEntityType.Circular:
                    var circ = (ProfileCircular)entity;
                    if (circ.CurveType == VerticalCurveType.Sag)
                    {
                        isConvex = true;
                    }
                    break;
            }
            IfcBoolean ifcIsConvex = new IfcBoolean()
            {
                _value = isConvex
            };
            return ifcIsConvex;
        }
        private static IfcGloballyUniqueId CreateUniqueId()
        {
            var g = Guid.NewGuid();
            var guidString = Convert.ToBase64String(g.ToByteArray());
            guidString = guidString.Replace("=", "");
            guidString = guidString.Replace("+", "");
            guidString = guidString.Replace("/", "");
            IfcGloballyUniqueId ifcGloballyUniqueId = new IfcGloballyUniqueId()
            {
                _value = guidString
            };
            return ifcGloballyUniqueId;
        }
        private static IfcCartesianPoint CreateIfcPoint3D(double x, double y, double z)
        {
            IfcLengthMeasure xCoordinate = new IfcLengthMeasure()
            {
                _value = x
            };
            IfcLengthMeasure yCoordinate = new IfcLengthMeasure()
            {
                _value = y
            };
            IfcLengthMeasure zCoordinate = new IfcLengthMeasure()
            {
                _value = z
            };
            List<IfcLengthMeasure> coordinateList = new List<IfcLengthMeasure>();
            coordinateList.Add(xCoordinate);
            coordinateList.Add(yCoordinate);
            coordinateList.Add(zCoordinate);
            IfcCartesianPoint point = new IfcCartesianPoint()
            {
                m_Coordinates = coordinateList
            };
            return point;
        }
        private static IfcCartesianPoint CreateIfcPoint2D(double x, double y)
        {
            IfcLengthMeasure xCoordinate = new IfcLengthMeasure()
            {
                _value = x
            };
            IfcLengthMeasure yCoordinate = new IfcLengthMeasure()
            {
                _value = y
            };
            List<IfcLengthMeasure> coordinateList = new List<IfcLengthMeasure>();
            coordinateList.Add(xCoordinate);
            coordinateList.Add(yCoordinate);
            IfcCartesianPoint point = new IfcCartesianPoint()
            {
                m_Coordinates = coordinateList
            };
            return point;
        }
        private static IfcBoolean CheckTheIfcTime(string civilClock)
        {
            bool ifcClock;
            if (civilClock == "True" || civilClock == "DirectionRight")
            {
                ifcClock = false;
            }
            else
            {
                ifcClock = true;
            }
            var ifcIsCcw = new IfcBoolean
            {
                _value = ifcClock
            };
            return ifcIsCcw;
        }
        private static IfcBoolean CheckTangential(string civilIsTangential)
        {
            bool ifcIsTangential = civilIsTangential == "Free" || civilIsTangential == "FloatOnNext";

            var ifcTangetialContinuity = new IfcBoolean
            {
                _value = ifcIsTangential
            };
            return ifcTangetialContinuity;
        }
        private static IfcPlaneAngleMeasure GetIfcDirection(double civilDirection)
        {
            var ifcDirection = Math.PI / 2 - civilDirection;
            var ifcStartDirection = new IfcPlaneAngleMeasure
            {
                _value = ifcDirection
            };
            return ifcStartDirection;
        }
        private static IfcTransitionCurveType GetIfcSpiralType(SpiralType civilSpiral)
        {
            IfcTransitionCurveType ifcSpiralType = new IfcTransitionCurveType();
            switch (civilSpiral)
            {
                case SpiralType.OffsetBiQuadratic:
                case SpiralType.BiQuadratic:
                    ifcSpiralType._value = IfcTransitionCurveType.Values.BIQUADRATICPARABOLA;
                    break;
                case SpiralType.OffsetClothoid:
                case SpiralType.Clothoid:
                    ifcSpiralType._value = IfcTransitionCurveType.Values.CLOTHOIDCURVE;
                    break;
                case SpiralType.OffsetBloss:
                case SpiralType.Bloss:
                    ifcSpiralType._value = IfcTransitionCurveType.Values.BLOSSCURVE;
                    break;
                case SpiralType.OffsetCubicParabola:
                case SpiralType.CubicParabola:
                    ifcSpiralType._value = IfcTransitionCurveType.Values.CUBICPARABOLA;
                    break;
                case SpiralType.OffsetSinusoidal:
                case SpiralType.Sinusoidal:
                    ifcSpiralType._value = IfcTransitionCurveType.Values.SINECURVE;
                    break;
            }
            return ifcSpiralType;
        }
        private static IfcRatioMeasure GetParabola2Gradient(ProfileParabolaAsymmetric civilParabolaA)
        {
            double parabola2StartGradient;
            if (civilParabolaA.GradeIn > civilParabolaA.GradeOut)
            {
                parabola2StartGradient = -civilParabolaA.AsymmetricLength1 / civilParabolaA.K * 100 +
                                         civilParabolaA.GradeIn;
            }
            else
            {
                parabola2StartGradient = civilParabolaA.AsymmetricLength1 / civilParabolaA.K * 100 +
                                         civilParabolaA.GradeIn;
            }
            IfcRatioMeasure ifcStartGradient = new IfcRatioMeasure()
            {
                _value = parabola2StartGradient
            };
            return ifcStartGradient;
        }
        private static List<AlignmentEntity> SortC3DAlignmentEntities(Alignment civilAlignment)
        {
            List<AlignmentEntity> entityList = new List<AlignmentEntity>();
            var entities = civilAlignment.Entities;

            int entityCount = entities.Count;
            entityList.Add(entities[entities.FirstEntity - 1]);


            int counter = 1;
            AlignmentEntity currentEntity = entityList[0];
            while (counter <= entityCount - 1)
            {
                var nextEntity = entities[currentEntity.EntityAfter - 1];
                entityList.Add(nextEntity);
                currentEntity = nextEntity;
                counter++;
            }

            return entityList;
        }
        private static List<ProfileEntity> SortC3DProfileEntities(Profile civilProfile)
        {
            List<ProfileEntity> entityList = new List<ProfileEntity>();
            var entities = civilProfile.Entities;

            int entityCount = entities.Count;
            entityList.Add(entities[checked((int)entities.FirstEntity) - 1]);


            int counter = 1;
            ProfileEntity currentEntity = entityList[0];
            while (counter <= entityCount - 1)
            {
                var nextEntity = entities[checked((int)currentEntity.EntityAfter) - 1];
                entityList.Add(nextEntity);
                currentEntity = nextEntity;
                counter++;
            }

            return entityList;
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //++++++++++++++++++++++++++++++   IMPORT Methods +++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private static Point3d ConvertIfcPoint2D(IfcCartesianPoint ifcPoint)
        {
            double x = ifcPoint.m_Coordinates[0]._value;
            double y = ifcPoint.m_Coordinates[1]._value;
            double z = 0;

            Point3d civilPoint = new Point3d(x, y, z);
            return civilPoint;
        }
        private static double ConvertIfcDirection(double ifcDir)
        {
            double civilDirection = Math.PI / 2 - ifcDir;
            if (civilDirection < 0)
            {
                civilDirection = 2 * Math.PI + civilDirection;
            }
            return civilDirection;
        }
        private static Point3d CalculateLinePoint(IfcLineSegment2D ifcLine, double dist)
        {
            //Convert Direction:
            double civilDir = ConvertIfcDirection(ifcLine.m_StartDirection._value);
            double x;
            double y;
            double z = 0;
            //Get Coordinates:
            if (civilDir >= 0 && civilDir < Math.PI / 2)
            {
                x = ifcLine.m_StartPoint.m_Coordinates[0]._value + dist * ifcLine.m_SegmentLength._value._value * Math.Cos(Math.PI / 2 - civilDir);
                y = ifcLine.m_StartPoint.m_Coordinates[1]._value + Math.Sin(Math.PI / 2 - civilDir) * ifcLine.m_SegmentLength._value._value * dist;
            }
            else if (civilDir >= Math.PI / 2 && civilDir < Math.PI)
            {
                x = ifcLine.m_StartPoint.m_Coordinates[0]._value + dist * ifcLine.m_SegmentLength._value._value * Math.Cos(civilDir - Math.PI / 2);
                y = ifcLine.m_StartPoint.m_Coordinates[1]._value - Math.Sin(civilDir - Math.PI / 2) * ifcLine.m_SegmentLength._value._value * dist;
            }
            else if (civilDir >= Math.PI && civilDir < 1.5 * Math.PI)
            {
                x = ifcLine.m_StartPoint.m_Coordinates[0]._value - dist * ifcLine.m_SegmentLength._value._value * Math.Cos(civilDir - Math.PI);
                y = ifcLine.m_StartPoint.m_Coordinates[1]._value - Math.Sin(civilDir - Math.PI) * ifcLine.m_SegmentLength._value._value * dist;
            }
            else
            {
                x = ifcLine.m_StartPoint.m_Coordinates[0]._value - dist * ifcLine.m_SegmentLength._value._value * Math.Cos(civilDir - 1.5 * Math.PI);
                y = ifcLine.m_StartPoint.m_Coordinates[1]._value + Math.Sin(civilDir - 1.5 * Math.PI) * ifcLine.m_SegmentLength._value._value * dist;
            }

            var civilPoint = new Point3d(x, y, z);
            return civilPoint;
        }
        private static Point3d GetArcCentre(IfcCircularArcSegment2D ifcArc)
        {
            double civilDir = ConvertIfcDirection(ifcArc.m_StartDirection._value);
            double x_s = ifcArc.m_StartPoint.m_Coordinates[0]._value;
            double y_s = ifcArc.m_StartPoint.m_Coordinates[1]._value;
            double R = ifcArc.m_Radius._value._value;
            double x_c;
            double y_c;
            if (civilDir >= 0 && civilDir < Math.PI / 2)
            {
                if (!ifcArc.m_IsCCW._value)
                {
                    x_c = x_s + R * Math.Cos(civilDir);
                    y_c = y_s - R * Math.Sin(civilDir);
                }
                else
                {
                    x_c = x_s - R * Math.Cos(civilDir);
                    y_c = y_s + R * Math.Sin(civilDir);
                }
            }
            else if (civilDir >= Math.PI / 2 && civilDir < Math.PI)
            {
                if (!ifcArc.m_IsCCW._value)
                {
                    x_c = x_s - R * Math.Cos(-civilDir + Math.PI);
                    y_c = y_s - R * Math.Sin(-civilDir + Math.PI);
                }
                else
                {
                    x_c = x_s + R * Math.Cos(-civilDir + Math.PI);
                    y_c = y_s + R * Math.Sin(-civilDir + Math.PI);
                }
            }
            else if (civilDir >= Math.PI && civilDir < 1.5 * Math.PI)
            {
                if (!ifcArc.m_IsCCW._value)
                {
                    x_c = x_s - R * Math.Cos(civilDir + Math.PI);
                    y_c = y_s + R * Math.Sin(civilDir + Math.PI);
                }
                else
                {
                    x_c = x_s + R * Math.Cos(civilDir + Math.PI);
                    y_c = y_s - R * Math.Sin(civilDir + Math.PI);
                }
            }
            else
            {
                if (!ifcArc.m_IsCCW._value)
                {
                    x_c = x_s + R * Math.Cos(-civilDir + 2 * Math.PI);
                    y_c = y_s + R * Math.Sin(-civilDir + 2 * Math.PI);
                }
                else
                {
                    x_c = x_s - R * Math.Cos(-civilDir + 2 * Math.PI);
                    y_c = y_s - R * Math.Sin(-civilDir + 2 * Math.PI);
                }
            }

            var centerPoint = new Point3d(x_c, y_c, 0);
            return centerPoint;
        }
        private static Point3d GetArcPoint(IfcCircularArcSegment2D ifcArc, double dist)
        {
            double civilDir = ConvertIfcDirection(ifcArc.m_StartDirection._value);
            var centrePt = GetArcCentre(ifcArc);
            double x_c = centrePt.X;
            double y_c = centrePt.Y;
            double x_s = ifcArc.m_StartPoint.m_Coordinates[0]._value;
            double y_s = ifcArc.m_StartPoint.m_Coordinates[1]._value;
            double r = ifcArc.m_Radius._value._value;
            double l = ifcArc.m_SegmentLength._value._value;
            double angle = Math.Atan2(y_s - y_c, x_s - x_c);

            if (!ifcArc.m_IsCCW._value)
            {
                angle = angle - dist * l / r;
            }
            else
            {
                angle = angle + dist * l / r;
            }
            double x = x_c + r * Math.Cos(angle);
            double y = y_c + r * Math.Sin(angle);


            var pt = new Point3d(x, y, 0);
            return pt;
        }
        private static bool CheckTheCivilTime(bool isCCW)
        {
            bool civilTime = false;
            if (!isCCW)
            {
                civilTime = true;
            }
            return civilTime;
        }
        private static int GetQuadrant(IfcCircularArcSegment2D ifcArc)
        {
            var centre = GetArcCentre(ifcArc);
            double x_c = centre.X;
            double y_c = centre.Y;
            double x_s = ifcArc.m_StartPoint.m_Coordinates[0]._value;
            double y_s = ifcArc.m_StartPoint.m_Coordinates[1]._value;
            int quadrant;
            if (x_s < x_c) //Quadrant III or IV
            {
                if (y_s < y_c) //Quadrant III
                {
                    quadrant = 3;
                }
                else //Quadrant IV
                {
                    quadrant = 4;
                }
            }
            else //Quadrant I or II
            {
                if (y_s < y_c) //Quadrant II
                {
                    quadrant = 2;
                }
                else //Quadrant I
                {
                    quadrant = 1;
                }
            }
            return quadrant;
        }
        private static Point3d GetClothoidIntersection(IfcTransitionCurveSegment2D spiral)
        {
            double l = spiral.m_SegmentLength._value._value;
            double arcRadius;
            double civilDir = ConvertIfcDirection(spiral.m_StartDirection._value);
            double angle = -civilDir;
            if (spiral.o_EndRadius == null)
            {
                arcRadius = spiral.o_StartRadius._value; //out spiral
            }
            else
            {
                arcRadius = spiral.o_EndRadius._value; //in spiral
            }
            //calculate endpoint:
            double x_e = l - Math.Pow(l, 3) / (40 * Math.Pow(arcRadius, 2)) + Math.Pow(l, 5) / (3456 * Math.Pow(arcRadius, 4)) - Math.Pow(l, 7) / (599040 * Math.Pow(arcRadius, 6));
            double y_e = Math.Pow(l, 2) / (6 * arcRadius) - Math.Pow(l, 4) / (336 * Math.Pow(arcRadius, 3)) + Math.Pow(l, 6) / (42240 * Math.Pow(arcRadius, 5)) - Math.Pow(l, 8) / (9676800 * Math.Pow(arcRadius, 7));
            double phi = l / (2 * arcRadius);
            double dx = y_e / Math.Tan(phi);
            double x_ip = x_e - dx;
            double y_ip = 0;

            //Transform:
            //Rotate:
            x_ip = x_ip * Math.Cos(-civilDir);
            y_ip = -x_ip * Math.Sin(-civilDir);
            //Translate:
            x_ip = x_ip + spiral.m_StartPoint.m_Coordinates[0]._value;
            y_ip = y_ip + spiral.m_StartPoint.m_Coordinates[1]._value;

            var pt = new Point3d(x_ip, y_ip, 0);
            return pt;
        }
        private static Point3d GetClothoidEnd(IfcTransitionCurveSegment2D spiral)
        {
            double l = spiral.m_SegmentLength._value._value;
            double arcRadius;
            double civilDir = ConvertIfcDirection(spiral.m_StartDirection._value);
            double angle = -civilDir;
            if (spiral.o_EndRadius == null)
            {
                arcRadius = spiral.o_StartRadius._value; //out spiral
            }
            else
            {
                arcRadius = spiral.o_EndRadius._value; //in spiral
            }
            //calculate endpoint:
            double x_e = l - Math.Pow(l, 3) / (40 * Math.Pow(arcRadius, 2)) + Math.Pow(l, 5) / (3456 * Math.Pow(arcRadius, 4)) - Math.Pow(l, 7) / (599040 * Math.Pow(arcRadius, 6));
            double y_e = Math.Pow(l, 2) / (6 * arcRadius) - Math.Pow(l, 4) / (336 * Math.Pow(arcRadius, 3)) + Math.Pow(l, 6) / (42240 * Math.Pow(arcRadius, 5)) - Math.Pow(l, 8) / (9676800 * Math.Pow(arcRadius, 7));


            //Transform:
            //Rotate:
            x_e = x_e * Math.Cos(-civilDir) + y_e * Math.Sin(-civilDir);
            y_e = -x_e * Math.Sin(-civilDir) + y_e * Math.Cos(-civilDir);
            //Translate:
            x_e = x_e + spiral.m_StartPoint.m_Coordinates[0]._value;
            y_e = y_e + spiral.m_StartPoint.m_Coordinates[1]._value;

            var pt = new Point3d(x_e, y_e, 0);
            return pt;
        }
        private static List<string> CreateIfcHorizontalEntityList(IfcAlignment2DHorizontal align)
        {
            List<string> horizontalEntityList = new List<string>();
            foreach (IfcAlignment2DHorizontalSegment entity in align.m_Segments)
            {
                if (entity.m_CurveGeometry.GetType() == typeof(IfcLineSegment2D))
                {
                    horizontalEntityList.Add("Line");
                }
                else if (entity.m_CurveGeometry.GetType() == typeof(IfcCircularArcSegment2D))
                {
                    horizontalEntityList.Add("Arc");
                }
                else if (entity.m_CurveGeometry.GetType() == typeof(IfcTransitionCurveSegment2D))
                {
                    horizontalEntityList.Add("Spiral");
                }
            }
            return horizontalEntityList;
        }
        private static Point3d CheckForVicintiy(double compareX, double compareY, IfcCartesianPoint ifcPoint)
        {
            double xValue = ifcPoint.m_Coordinates[0]._value;
            double yValue = ifcPoint.m_Coordinates[1]._value;

            double deviation = Math.Sqrt(Math.Pow(Math.Abs(compareX - ifcPoint.m_Coordinates[0]._value), 2) + Math.Pow(Math.Abs(compareY - ifcPoint.m_Coordinates[1]._value), 2));

            if (deviation <= 0.001)
            {
                xValue = compareX;
                yValue = compareY;
            }

            Point3d civilPoint = new Point3d(xValue, yValue, 0);
            return civilPoint;
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++  IMPORT Function  +++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        [CommandMethod("IFCImportTest")]
        public void ifcimporttest()
        {
            //Read my IFC File...
            var dlg = new OpenFileDialog();
            var result = dlg.ShowDialog();
            var ifcFile = dlg.FileName;

            if (result == true)
            {
                //Intialize Ifc Reader:
                IfcAlignmentReader ifcReader = new IfcAlignmentReader();

                //Create Ifc Structure:                    
                ifcReader.ReadFile(ifcFile);


                //Start Import:
                var civilDatabase = Application.DocumentManager.MdiActiveDocument.Database;
                var civilDocument = CivilApplication.ActiveDocument;

                var ifcEntities = ifcReader._entityList;
                int ifcEntityCounter = ifcReader.CountIfcEntities(ifcFile);
                int surfaceCounter = 1;
                int alignCounter = 1;
                double pointCompareX = 0;
                double pointCompareY = 0;
                Point3d startPoint;
                for (int i = 1; i <= ifcEntityCounter; i++)    //erhöhe i um entitycount nach einem alignment, spart zeit!
                {
                    if (ifcEntities[i].GetType() == typeof(IfcAlignment))
                    {
                        IfcAlignment castedAlignment = (IfcAlignment)ifcEntities[i];
                        //Create C3D Alignment:
                        string name;
                        if (castedAlignment.o_Name == null)
                        {
                            if (castedAlignment.o_Description == null)
                            {
                                name = "Imported Alignment" + alignCounter.ToString();
                            }
                            else
                            {
                                name = castedAlignment.o_Description._value;
                            }
                        }
                        else
                        {
                            name = castedAlignment.o_Name._value;
                        }

                        var civilAlignmentId = Alignment.Create(civilDocument, name, "", "C-ROAD", "Proposed", "All Labels");

                        //Horizontal Alignment:
                        using (Transaction civilTransactionManager = civilDatabase.TransactionManager.StartTransaction())
                        {
                            var civilAlignment = (Alignment)civilTransactionManager.GetObject(civilAlignmentId, OpenMode.ForWrite);
                            

                            //Get the Alignment Curve:
                            IfcAlignmentCurve ifcCurve = (IfcAlignmentCurve)castedAlignment.m_Axis;

                            //Get Horizontal/Vertical Alignment:
                            IfcAlignment2DHorizontal ifcAlignmentHorizontal = ifcCurve.m_Horizontal;
                            List<string> ifcHorizontalEntities = CreateIfcHorizontalEntityList(ifcAlignmentHorizontal);
                            IfcAlignment2DVertical ifcAlignmentVertical = ifcCurve.o_Vertical;

                            Int32 entityCounter = 0;
                            Int32 spiralCounter = 1;
                            //Set horizontal Alignment:
                            //for (int j = 0; j <= ifcAlignmentHorizontal.m_Segments.Count - 1; j++)
                            foreach (var entity in ifcAlignmentHorizontal.m_Segments)
                            {
                                //var entity = ifcAlignmentHorizontal.m_Segments[j];
                                if (entity.m_CurveGeometry.GetType() == typeof(IfcLineSegment2D))
                                {
                                    IfcLineSegment2D castedLine = (IfcLineSegment2D)entity.m_CurveGeometry;                                
                                    if (entityCounter > 0)
                                    {
                                        startPoint = CheckForVicintiy(pointCompareX, pointCompareY, castedLine.m_StartPoint);
                                    }
                                    else
                                    {
                                        startPoint = ConvertIfcPoint2D(castedLine.m_StartPoint);
                                    }

                                    //Create Civil Line:
                                    AlignmentLine civilLine = civilAlignment.Entities.AddFixedLine(entityCounter, startPoint, CalculateLinePoint(castedLine, 1));
                                    entityCounter++;
                                    pointCompareX = civilLine.EndPoint.X;
                                    pointCompareY = civilLine.EndPoint.Y;
                                    
                                }
                                else if (entity.m_CurveGeometry.GetType() == typeof(IfcCircularArcSegment2D))
                                {
                                    IfcCircularArcSegment2D castedArc = (IfcCircularArcSegment2D)entity.m_CurveGeometry;
                                    if (entityCounter > 0)
                                    {
                                        startPoint = CheckForVicintiy(pointCompareX, pointCompareY, castedArc.m_StartPoint);
                                    }
                                    else
                                    {
                                        startPoint = ConvertIfcPoint2D(castedArc.m_StartPoint);
                                    }

                                    //Create Civil Arc:
                                    AlignmentArc civilArc = civilAlignment.Entities.AddFixedCurve(entityCounter, startPoint, GetArcPoint(castedArc, 0.5), GetArcPoint(castedArc, 1));
                                    entityCounter++;
                                    pointCompareX = civilArc.EndPoint.X;
                                    pointCompareY = civilArc.EndPoint.Y;
                                }
                                // Assures downward compatibility:
                                else if (entity.m_CurveGeometry.GetType() == typeof(IfcClothoidalArcSegment2D))
                                {

                                }
                                else if (entity.m_CurveGeometry.GetType() == typeof(IfcTransitionCurveSegment2D))
                                {
                                    IfcTransitionCurveSegment2D castedTransition = (IfcTransitionCurveSegment2D)entity.m_CurveGeometry;


                                    //Check Transition Type:
                                    if (castedTransition.m_TransitionCurveType._value == IfcTransitionCurveType.Values.CLOTHOIDCURVE)
                                    {
                                        float zero = 0;
                                        double endRadius;

                                        if (castedTransition.o_EndRadius == null)
                                        {

                                            endRadius = 1 / zero;
                                        }
                                        else
                                        {
                                            endRadius = castedTransition.o_EndRadius._value;
                                        }
                                        //var spiralIP = GetClothoidIntersection(castedTransition);
                                        //var spiralEnd = GetClothoidEnd(castedTransition);
                                        //AlignmentSpiral civilSpiral = civilAlignment.Entities.AddFixedSpiral(entityCounter, ConvertIfcPoint2D(castedTransition.m_StartPoint), spiralIP, spiralEnd, SpiralType.Clothoid);
                                        AlignmentSpiral civilSpiral = civilAlignment.Entities.AddFloatSpiral(entityCounter, endRadius, castedTransition.m_SegmentLength._value._value, CheckTheCivilTime(castedTransition.m_IsStartRadiusCCW._value), SpiralType.Clothoid);
                                        spiralCounter++;
                                        pointCompareX = civilSpiral.EndPoint.X;
                                        pointCompareY = civilSpiral.EndPoint.Y;
                                    }
                                    else if (castedTransition.m_TransitionCurveType._value == IfcTransitionCurveType.Values.BLOSSCURVE)
                                    {

                                    }
                                    entityCounter++;
                                }
                            }

                            //Set vertical Alignment:
                            if (ifcAlignmentVertical != null)
                            {
                                ObjectId layerId = civilAlignment.LayerId;
                                ObjectId surfaceId = civilDocument.GetSurfaceIds()[0];
                                ObjectId styleId = civilDocument.Styles.ProfileStyles[0];
                                ObjectId labelSetId = civilDocument.Styles.LabelSetStyles.ProfileLabelSetStyles[0];
                                ///Create Profile Object:
                                ObjectId profileId = Profile.CreateFromSurface("Profile_to_" + name, civilAlignmentId, surfaceId, layerId, styleId, labelSetId);
                            }

                            //Commit to Changes:
                            civilTransactionManager.Commit();
                        }
                    }
                    else if (ifcEntities[i].GetType() == typeof(IfcTriangulatedFaceSet))
                    {

                        //Get name:
                        string name;

                        name = "Imported_TIN_Surface_" + surfaceCounter.ToString();
                        surfaceCounter++;

                        var castedTin = (IfcTriangulatedFaceSet)ifcEntities[i];
                        //Get PointList:
                        var pointList = castedTin.m_Coordinates.m_CoordList;

                        // Select a Surface style to use
                        using (Transaction civilTransactionManager = civilDatabase.TransactionManager.StartTransaction())
                        {
                            ObjectId styleId = civilDocument.Styles.SurfaceStyles["Cut and Fill Banding 0.5m Interval (2D)"];
                            ObjectIdCollection pointEntitiesIdColl = new ObjectIdCollection();
                            if (styleId != null && pointEntitiesIdColl != null)
                            {
                                // Create an empty TIN Surface
                                ObjectId surfaceId = TinSurface.Create(name, styleId);
                                TinSurface surface = civilTransactionManager.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;
                                //Create Points:
                                //foreach (var point in pointList)
                                for (int k = 0; k <= pointList.Count - 1; k++)
                                {
                                    var simplifier = 0;
                                    if (pointList.Count >= 1000)
                                    {
                                        simplifier = 100;
                                    }
                                    else if (pointList.Count >= 10000)
                                    {
                                        simplifier = 500;
                                    }
                                    var point = pointList[k];
                                    double x = point[0]._value;
                                    double y = point[1]._value;
                                    double z = point[2]._value;
                                    var pt = new Point3d(x, y, z);
                                    k = k + simplifier;
                                    surface.AddVertex(pt);
                                }

                            }
                            //Commit to Changes:
                            civilTransactionManager.Commit();
                        }

                    }
                }
            }

        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++  Create Button  +++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        [CommandMethod("ifc", CommandFlags.Session)]
        public void Main()
        {

            //Open License Agreement:
            License license = new License();
            license.ShowDialog();

            var ribbon = ComponentManager.Ribbon; //Get C3D UI-Information

            var btab = ribbon.FindTab("CIVIL.ID_Civil3DAddins"); //Find the correct Ribbon

            //------------------  Check if the Panel already exists  ------------------------         
            if (btab != null)
            {
                if (btab.Panels != null)
                {
                    foreach (var panel in btab.Panels)
                    {
                        if (panel.UID == "UID_IfcPanel") //if the Panel already exists...
                        {
                            var removePnl = panel;


                            btab.Panels.Remove(removePnl); //...delete the Panel
                            break;
                        }
                    }
                }
            }
            //-------------------------------------------------------------------------------

            //------------------------  Create the new Button  ------------------------------
            var civilPanel = new RibbonPanel
            {
                UID = "UID_IfcPanel",
                Id = "ID_IfcPanel"
            };

            var source = new RibbonPanelSource
            {
                Id = "ID_IfcPanelSource",
                Name = "IfcPanelSource",
                Title = "IFC Alignment v1.1"
            }; //Create Panel Source
            civilPanel.Source = source; //... and bind it to the Panel


            var rbb1 = new RibbonButton
            {
                Name = "Export Ifc...",
                Text = "Ifc Export",
                GroupLocation = RibbonItemGroupLocation.Single,
                ResizeStyle = RibbonItemResizeStyles.HideText,
                Orientation = Orientation.Horizontal,
                ShowText = false,
                CommandHandler = new ExportCmdHandler(),
                Id = "ID_TestButton",
                ShowImage = true,
                LargeImage = GetBitmap("Export_Icon.png"),   //Get Button Image
                Image = GetBitmap("Export_Icon.png"),
                Size = RibbonItemSize.Large
            }; //Create Ribbon Button 1

            var rbn1Tt = new RibbonToolTip
            {
                Command = "IFC",
                Title = "Ifc-Export",
                Content = "This Add-in let's you export your current .dwg-file to an IFC 4 (.ifc) File.",
                ExpandedContent = "In the dialog box, navigate to the file directory you want to save your file."
            }; //Create Tooltip
            rbb1.ToolTip = rbn1Tt;

            var rbb2 = new RibbonButton
            {
                Name = "Import Ifc...",
                Text = "Ifc Import",
                CommandParameter = "_.IFCImportTest ",
                GroupLocation = RibbonItemGroupLocation.Single,
                ResizeStyle = RibbonItemResizeStyles.HideText,
                Orientation = Orientation.Horizontal,
                ShowText = false,
                Id = "ID_TestButton",
                ShowImage = true,
                CommandHandler = new ImportCmdHandler(),
                LargeImage = GetBitmap("Import_Icon.png"),
                Image = GetBitmap("Import_Icon.png"),
                Size = RibbonItemSize.Large
            }; //Create Ribbon Button 2

            var rbn2Tt = new RibbonToolTip
            {
                Command = "IFC",
                Title = "Ifc-Import",
                Content = "This Add-in let's you import a IFC 4 (.ifc) File to your current drawing.",
                ExpandedContent = "In the dialog box, navigate to the file directory your IFC-File ist stored."
            }; //Create Tooltip
            rbb2.ToolTip = rbn2Tt;

            var rbSplitButton = new RibbonSplitButton
            {
                Text = "Ifc Add-in",
                ShowText = true,
                Size = RibbonItemSize.Large,
                Name = "Ifc Add-in"
            }; //Create new Split-Button


            rbSplitButton.Items.Add(rbb1); //Add Button 1 to the Split-Button
            rbSplitButton.Items.Add(rbb2); //Add Button 2 to the Split-Button

            if (btab != null)
            {
                if (btab.Panels != null) btab.Panels.Add(civilPanel); //Add Panel to the Add-ins Tab
                btab.IsActive = true; //...and set as active
            }

            source.Items.Add(rbSplitButton); //Add Split-Button to the Panel
            //-------------------------------------------------------------------------------
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++  EXPORT Function  +++++++++++++++++++++++++++++++++++
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        public class ExportCmdHandler : ICommand
        {
            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public void Execute(object parameter)
            {
                if (parameter is RibbonButton) //if Button is clicked...
                {
                    //---------------------  Open SaveFile-Dialog  -------------------------------
                    var dlg = new SaveFileDialog
                    {
                        FileName = "ifcAlignment", // Default File Name
                        DefaultExt = ".ifc", // Default File Extension
                        Filter = "Ifc Files (.ifc)|*.ifc" //Filter Files by Extension                   
                    };
                    var result = dlg.ShowDialog();
                    //----------------------------------------------------------------------------

                    if (result == true)
                    {
                        //Get Civil3D Document
                        var civilDatabase = Application.DocumentManager.MdiActiveDocument.Database;
                        var civilDocument = CivilApplication.ActiveDocument;
                        using (Transaction civilTransactionManager =
                            civilDatabase.TransactionManager.StartTransaction())
                        {
                            //Create Writer
                            IfcWriter ifcWriter = new IfcWriter();

                            //Create Subheader
                            IfcPerson ifcPerson = new IfcPerson()
                            {
                                o_FamilyName = new IfcLabel()
                                {
                                    _value = "Last Name"
                                },
                                o_GivenName = new IfcLabel()
                                {
                                    _value = "First Name"
                                }
                            };

                            IfcOrganization ifcOrganization = new IfcOrganization()
                            {
                                m_Name = new IfcLabel()
                                {
                                    _value = "FlixFixCoding"
                                }
                            };

                            IfcApplication ifcApplication = new IfcApplication()
                            {
                                m_ApplicationDeveloper = ifcOrganization,
                                m_ApplicationFullName = new IfcLabel()
                                {
                                    _value = "Autodesk Civil 3D"
                                },
                                m_ApplicationIdentifier = new IfcIdentifier()
                                {
                                    _value = "C3D"
                                },
                                m_Version = new IfcLabel()
                                {
                                    _value = "1.0"
                                }
                            };

                            IfcPersonAndOrganization ifcPersonAndOrganization = new IfcPersonAndOrganization()
                            {
                                m_TheOrganization = ifcOrganization,
                                m_ThePerson = ifcPerson
                            };

                            IfcDimensionalExponents ifcDimensionalExponents = new IfcDimensionalExponents()
                            {
                                m_AmountOfSubstanceExponent = 0,
                                m_ElectricCurrentExponent = 0,
                                m_LengthExponent = 0,
                                m_LuminousIntensityExponent = 0,
                                m_MassExponent = 0,
                                m_ThermodynamicTemperatureExponent = 0,
                                m_TimeExponent = 0
                            };

                            IfcSIUnit ifcIfcSiUnit1 = new IfcSIUnit()
                            {
                                m_Name = new IfcSIUnitName()
                                {
                                    _value = IfcSIUnitName.Values.METRE
                                },
                                m_Dimensions = ifcDimensionalExponents,
                                m_UnitType = new IfcUnitEnum()
                                {
                                    _value = IfcUnitEnum.Values.LENGTHUNIT
                                },
                            };

                            IfcSIUnit ifcIfcSiUnit2 = new IfcSIUnit()
                            {
                                m_Name = new IfcSIUnitName()
                                {
                                    _value = IfcSIUnitName.Values.RADIAN
                                },
                                m_Dimensions = ifcDimensionalExponents,
                                m_UnitType = new IfcUnitEnum()
                                {
                                    _value = IfcUnitEnum.Values.PLANEANGLEUNIT
                                },
                            };

                            IfcSIUnit ifcIfcSiUnit3 = new IfcSIUnit()
                            {
                                m_Name = new IfcSIUnitName()
                                {
                                    _value = IfcSIUnitName.Values.SQUARE_METRE
                                },
                                m_Dimensions = ifcDimensionalExponents,
                                m_UnitType = new IfcUnitEnum()
                                {
                                    _value = IfcUnitEnum.Values.AREAUNIT
                                },
                            };

                            IfcSIUnit ifcIfcSiUnit4 = new IfcSIUnit()
                            {
                                m_Name = new IfcSIUnitName()
                                {
                                    _value = IfcSIUnitName.Values.CUBIC_METRE
                                },
                                m_Dimensions = ifcDimensionalExponents,
                                m_UnitType = new IfcUnitEnum()
                                {
                                    _value = IfcUnitEnum.Values.VOLUMEUNIT
                                },
                            };

                            IfcUnit ifcUnit1 = new IfcUnit()
                            {
                                _value = ifcIfcSiUnit1
                            };

                            IfcUnit ifcUnit2 = new IfcUnit()
                            {
                                _value = ifcIfcSiUnit2
                            };

                            IfcUnit ifcUnit3 = new IfcUnit()
                            {
                                _value = ifcIfcSiUnit3
                            };

                            IfcUnit ifcUnit4 = new IfcUnit()
                            {
                                _value = ifcIfcSiUnit4
                            };

                            IfcUnitAssignment ifcUnitAssignment = new IfcUnitAssignment()
                            {
                                m_Units = new List<IfcUnit> { ifcUnit1, ifcUnit2, ifcUnit3, ifcUnit4 }
                            };

                            /*IfcProject ifcProject = new IfcProject()
                            {
                                m_GlobalId = CreateUniqueId(),
                                o_Name = new IfcLabel()
                                {
                                    _value = Application.DocumentManager.MdiActiveDocument.Database.ProjectName
                                },
                                o_UnitsInContext = ifcUnitAssignment
                            };*/

                            IfcCartesianPoint ifcZeroPoint = CreateIfcPoint3D(0, 0, 0);

                            IfcAxis2Placement3D ifcAxis2Placement3D = new IfcAxis2Placement3D()
                            {
                                m_Location = ifcZeroPoint
                            };

                            IfcLocalPlacement ifcLocalPlacement = new IfcLocalPlacement()
                            {
                                m_RelativePlacement = new IfcAxis2Placement()
                                {
                                    _value = ifcAxis2Placement3D
                                }
                            };

                            ifcWriter.insertEntity(ifcPerson);
                            ifcWriter.insertEntity(ifcOrganization);
                            ifcWriter.insertEntity(ifcApplication);
                            ifcWriter.insertEntity(ifcPersonAndOrganization);
                            ifcWriter.insertEntity(ifcDimensionalExponents);
                            ifcWriter.insertEntity(ifcUnitAssignment);
                            ifcWriter.insertEntity(ifcIfcSiUnit1);
                            ifcWriter.insertEntity(ifcIfcSiUnit2);
                            ifcWriter.insertEntity(ifcIfcSiUnit3);
                            ifcWriter.insertEntity(ifcIfcSiUnit4);
                            //ifcWriter.insertEntity(ifcProject);
                            ifcWriter.insertEntity(ifcZeroPoint);
                            ifcWriter.insertEntity(ifcAxis2Placement3D);
                            ifcWriter.insertEntity(ifcLocalPlacement);

                            //Get Building Site
                            var civilSiteCollection = CivilApplication.ActiveDocument.GetSiteIds();

                            foreach (ObjectId siteId in civilSiteCollection)
                            {
                                Site civilSite =
                                    siteId.GetObject(OpenMode.ForRead) as Site;
                                IfcSite ifcSite = new IfcSite()
                                {
                                    m_GlobalId = CreateUniqueId(),
                                    o_ObjectPlacement = ifcLocalPlacement
                                };
                                if (civilSite != null && civilSite.Name != null)
                                {
                                    ifcSite.o_Name._value = civilSite.Name;
                                }
                                if (civilSite != null && civilSite.Description != null)
                                {
                                    ifcSite.o_Description._value = civilSite.Description;
                                }
                                ifcWriter.insertEntity(ifcSite);
                            }


                            //Coordinate System
                            /*var ifcCrsCode = drawingSettings.UnitZoneSettings.CoordinateSystemCode.GetTypeCode();
                            IfcProjectedCRS ifcProjectedCrs = new IfcProjectedCRS()
                            {
                                o_Description = new IfcText()
                                {
                                    _value = ifcCrsCode.ToString()
                                },
                                m_Name = new IfcLabel()
                                {
                                    _value = SettingsUnitZone.GetCoordinateSystemByCode(ifcCrsCode.ToString()).ToString()
                                },
                                o_MapUnit = new IfcSIUnit()
                                {
                                    m_UnitType = new IfcUnitEnum()
                                    {
                                        _value = IfcUnitEnum.Values.LENGTHUNIT
                                    },
                                    m_Name = new IfcSIUnitName()
                                    {
                                        _value = IfcSIUnitName.Values.METRE
                                    }
                                }
                            };
                            ifcWriter.insertEntity(ifcProjectedCrs);*/

                            //missing IfcRelAggregates!!!!
                            //missing IfcRelContainedInSpatialStructure!!!!
                            //missing IfcMapConversion!!!!
                            //missing IfcGeometricRepresenationContext!!!!


                            //-------------------------------------------Surface(s)----------------------------------------------
                            var civilSurfaceCollection = civilDocument.GetSurfaceIds();
                            foreach (ObjectId surfaceId in civilSurfaceCollection)
                            {
                                var civilSurface =
                                    (Surface)civilTransactionManager.GetObject(surfaceId, OpenMode.ForRead);
                                if (civilSurface is TinSurface)
                                {
                                    //Ifc Geometric Representation Context
                                    IfcGeometricRepresentationContext ifcGeometricRepresentationContext =
                                        new IfcGeometricRepresentationContext()
                                        {
                                            m_WorldCoordinateSystem = new IfcAxis2Placement()
                                            {
                                                _value = ifcAxis2Placement3D
                                            },
                                            m_CoordinateSpaceDimension = new IfcDimensionCount()
                                            {
                                                _value = 3
                                            },
                                            o_ContextType = new IfcLabel()
                                            {
                                                _value = "Surface"
                                            }
                                        };
                                    ifcWriter.insertEntity(ifcGeometricRepresentationContext);

                                    var civilTinSurface = civilSurface as TinSurface;

                                    //Ifc Triangulated Face Set
                                    IfcTriangulatedFaceSet ifcTriangulatedFaceSet = new IfcTriangulatedFaceSet()
                                    {
                                        m_CoordIndex = new List<List<IfcPositiveInteger>>(),
                                        o_Closed = new IfcBoolean()
                                        {
                                            _value = false
                                        }
                                    };
                                    ifcWriter.insertEntity(ifcTriangulatedFaceSet);

                                    IfcCartesianPointList3D vertexList = new IfcCartesianPointList3D()
                                    {
                                        m_CoordList = new List<List<IfcLengthMeasure>>()
                                    };
                                    ifcTriangulatedFaceSet.m_Coordinates = vertexList;
                                    ifcWriter.insertEntity(vertexList);

                                    var civilTinTriangleCollection = civilTinSurface.GetTriangles(false);
                                    int vertexCounter = 1;
                                    foreach (TinSurfaceTriangle civilTinTriangle in civilTinTriangleCollection)
                                    {
                                        var vertex1 = new List<IfcLengthMeasure>();
                                        var triangle = new List<IfcPositiveInteger>();
                                        var vertex1X = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex1.Location.X
                                        };
                                        var vertex1Y = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex1.Location.Y
                                        };
                                        var vertex1Z = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex1.Location.Z
                                        };
                                        vertex1.Add(vertex1X);
                                        vertex1.Add(vertex1Y);
                                        vertex1.Add(vertex1Z);
                                        triangle.Add(new IfcPositiveInteger() { _value = new IfcInteger() { _value = vertexCounter } });
                                        vertexCounter++;

                                        var vertex2 = new List<IfcLengthMeasure>();
                                        var vertex2X = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex2.Location.X
                                        };
                                        var vertex2Y = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex2.Location.Y
                                        };
                                        var vertex2Z = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex2.Location.Z
                                        };
                                        vertex2.Add(vertex2X);
                                        vertex2.Add(vertex2Y);
                                        vertex2.Add(vertex2Z);
                                        triangle.Add(new IfcPositiveInteger() { _value = new IfcInteger() { _value = vertexCounter } });
                                        vertexCounter++;

                                        var vertex3 = new List<IfcLengthMeasure>();
                                        var vertex3X = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex3.Location.X
                                        };
                                        var vertex3Y = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex3.Location.Y
                                        };
                                        var vertex3Z = new IfcLengthMeasure()
                                        {
                                            _value = civilTinTriangle.Vertex3.Location.Z
                                        };
                                        vertex3.Add(vertex3X);
                                        vertex3.Add(vertex3Y);
                                        vertex3.Add(vertex3Z);
                                        triangle.Add(new IfcPositiveInteger() { _value = new IfcInteger() { _value = vertexCounter } });
                                        vertexCounter++;

                                        //Add to IFC Entity
                                        vertexList.m_CoordList.Add(vertex1);
                                        vertexList.m_CoordList.Add(vertex2);
                                        vertexList.m_CoordList.Add(vertex3);
                                        ifcTriangulatedFaceSet.m_CoordIndex.Add(triangle);
                                    }

                                    IfcShapeRepresentation ifcShape = new IfcShapeRepresentation()
                                    {
                                        m_ContextOfItems = ifcGeometricRepresentationContext,
                                        m_Items = new List<IfcRepresentationItem>()
                                    };
                                    ifcShape.m_Items.Add(ifcTriangulatedFaceSet);
                                    ifcWriter.insertEntity(ifcShape);

                                    IfcProductDefinitionShape ifcProductDefinitionShape =
                                        new IfcProductDefinitionShape()
                                        {
                                            m_Representations = new List<IfcRepresentation>()
                                        };

                                    ifcProductDefinitionShape.m_Representations.Add(ifcShape);
                                    ifcWriter.insertEntity(ifcProductDefinitionShape);

                                    IfcGeographicElement ifcGeographicElement = new IfcGeographicElement()
                                    {
                                        m_GlobalId = CreateUniqueId(),
                                        o_ObjectPlacement = ifcLocalPlacement,
                                        o_Representation = ifcProductDefinitionShape
                                    };
                                    ifcWriter.insertEntity(ifcGeographicElement);
                                }
                            }



                            var civilAlignmentCollection = civilDocument.GetAlignmentIds();
                            //-------------------------------------------Horizontal Alignment(s)---------------------------------
                            foreach (ObjectId alignId in civilAlignmentCollection)
                            {
                                var civilAlignment =
                                    (Alignment)civilTransactionManager
                                        .GetObject(alignId, OpenMode.ForRead); //Open File for Read

                                //Ifc Alignment                                
                                IfcAlignment ifcAlignment = new IfcAlignment()
                                {
                                    m_GlobalId = CreateUniqueId(),
                                    o_Name = new IfcLabel()
                                    {
                                        _value = civilAlignment.Name
                                    },
                                    o_ObjectPlacement = ifcLocalPlacement,

                                };
                                ifcWriter.insertEntity(ifcAlignment);

                                //IfcAlignmentCurve
                                IfcAlignmentCurve ifcAlignmentCurve = new IfcAlignmentCurve();
                                ifcAlignment.m_Axis = ifcAlignmentCurve;
                                ifcWriter.insertEntity(ifcAlignmentCurve);

                                //IfcAlignment2DHorizontal
                                IfcAlignment2DHorizontal ifcAlignment2DHorizontal = new IfcAlignment2DHorizontal()
                                {
                                    o_StartDistAlong = new IfcLengthMeasure()
                                    {
                                        _value = 0
                                    },
                                    m_Segments = new List<IfcAlignment2DHorizontalSegment>()
                                };
                                ifcAlignmentCurve.m_Horizontal = ifcAlignment2DHorizontal;
                                ifcWriter.insertEntity(ifcAlignment2DHorizontal);

                                //Get Alignment Entities
                                var civilAlignmentEntityList = SortC3DAlignmentEntities(civilAlignment);
                                foreach (var horizontalAlignmentEntity in civilAlignmentEntityList)
                                {
                                    var civilAlignmentSubentityCounter = horizontalAlignmentEntity.SubEntityCount;
                                    for (var i = 0; (i <= (civilAlignmentSubentityCounter - 1)); i++)
                                    {
                                        var civilAlignmentSubentity = horizontalAlignmentEntity[i];
                                        switch (civilAlignmentSubentity.SubEntityType) //if Element-Type is a...
                                        {
                                            //...Circular Arc   
                                            case AlignmentSubEntityType.Arc:
                                                var alignSubEntArc = (AlignmentSubEntityArc)civilAlignmentSubentity;

                                                //Ifc Alignment2DHorizontalSegment
                                                IfcAlignment2DHorizontalSegment ifcHorSegArc =
                                                    new IfcAlignment2DHorizontalSegment
                                                    {
                                                        o_TangentialContinuity =
                                                            CheckTangential(horizontalAlignmentEntity.Constraint1
                                                                .ToString())
                                                    };

                                                //StartPoint
                                                IfcCartesianPoint ifcArcStartPoint =
                                                    CreateIfcPoint2D(alignSubEntArc.StartPoint.X,
                                                        alignSubEntArc.StartPoint.Y);

                                                //Ifc CircularArcSegment2D
                                                IfcCircularArcSegment2D ifcCircularArc = new IfcCircularArcSegment2D()
                                                {
                                                    m_IsCCW = CheckTheIfcTime(alignSubEntArc.Clockwise.ToString()),
                                                    m_Radius = new IfcPositiveLengthMeasure()
                                                    {
                                                        _value = new IfcLengthMeasure()
                                                        {
                                                            _value = alignSubEntArc.Radius
                                                        }
                                                    },
                                                    m_SegmentLength = new IfcPositiveLengthMeasure()
                                                    {
                                                        _value = new IfcLengthMeasure()
                                                        {
                                                            _value = alignSubEntArc.Length
                                                        }
                                                    },
                                                    m_StartDirection = GetIfcDirection(alignSubEntArc.StartDirection),
                                                    m_StartPoint = ifcArcStartPoint
                                                };

                                                //Write Attributes to other Entities
                                                ifcHorSegArc.m_CurveGeometry = ifcCircularArc;
                                                ifcAlignment2DHorizontal.m_Segments.Add(ifcHorSegArc);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcHorSegArc);
                                                ifcWriter.insertEntity(ifcCircularArc);
                                                ifcWriter.insertEntity(ifcArcStartPoint);

                                                //Start next Element:
                                                break;

                                            //...Line   
                                            case AlignmentSubEntityType.Line:
                                                var alignSubEntLine = (AlignmentSubEntityLine)civilAlignmentSubentity;

                                                //Ifc Alignment2DHorizontalSegment
                                                IfcAlignment2DHorizontalSegment ifcHorSegLine =
                                                    new IfcAlignment2DHorizontalSegment
                                                    {
                                                        o_TangentialContinuity =
                                                            CheckTangential(horizontalAlignmentEntity.Constraint1
                                                                .ToString())
                                                    };

                                                //StartPoint
                                                IfcCartesianPoint ifcLineStartPoint =
                                                    CreateIfcPoint2D(alignSubEntLine.StartPoint.X,
                                                        alignSubEntLine.StartPoint.Y);

                                                //Ifc LineSegment2D
                                                IfcLineSegment2D ifcLine = new IfcLineSegment2D()
                                                {
                                                    m_StartDirection = GetIfcDirection(alignSubEntLine.Direction),
                                                    m_SegmentLength = new IfcPositiveLengthMeasure()
                                                    {
                                                        _value = new IfcLengthMeasure()
                                                        {
                                                            _value = alignSubEntLine.Length
                                                        }
                                                    },
                                                    m_StartPoint = ifcLineStartPoint
                                                };

                                                //Write Attributes to other Entities
                                                ifcHorSegLine.m_CurveGeometry = ifcLine;
                                                ifcAlignment2DHorizontal.m_Segments.Add(ifcHorSegLine);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcHorSegLine);
                                                ifcWriter.insertEntity(ifcLine);
                                                ifcWriter.insertEntity(ifcLineStartPoint);

                                                //Start next Element:
                                                break;

                                            //...Spiral 
                                            case AlignmentSubEntityType.Spiral:
                                                var alignSubEntSpiral =
                                                    (AlignmentSubEntitySpiral)civilAlignmentSubentity;

                                                //Ifc Alignment2DHorizontalSegment
                                                IfcAlignment2DHorizontalSegment ifcHorSegSpiral =
                                                    new IfcAlignment2DHorizontalSegment
                                                    {
                                                        o_TangentialContinuity =
                                                            CheckTangential(horizontalAlignmentEntity.Constraint1
                                                                .ToString())
                                                    };

                                                //StartPoint
                                                IfcCartesianPoint ifcSpiralStartPoint =
                                                    CreateIfcPoint2D(alignSubEntSpiral.StartPoint.X,
                                                        alignSubEntSpiral.StartPoint.Y);

                                                //Ifc TransitionCurveSegment
                                                IfcTransitionCurveSegment2D ifcSpiral =
                                                    new IfcTransitionCurveSegment2D()
                                                    {
                                                        m_SegmentLength = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = alignSubEntSpiral.Length
                                                            }
                                                        },
                                                        m_StartPoint = ifcSpiralStartPoint,
                                                        m_StartDirection =
                                                            GetIfcDirection(alignSubEntSpiral.StartDirection),
                                                        m_IsEndRadiusCCW =
                                                            CheckTheIfcTime(alignSubEntSpiral.Direction.ToString()),
                                                        m_IsStartRadiusCCW =
                                                            CheckTheIfcTime(alignSubEntSpiral.Direction.ToString()),
                                                        m_TransitionCurveType =
                                                            GetIfcSpiralType(alignSubEntSpiral.SpiralDefinition),
                                                        o_StartRadius = new IfcLengthMeasure()
                                                        {
                                                            _value = alignSubEntSpiral.RadiusIn
                                                        },
                                                        o_EndRadius = new IfcLengthMeasure()
                                                        {
                                                            _value = alignSubEntSpiral.RadiusOut
                                                        }
                                                    };

                                                //Write Attributes to other Entities
                                                ifcHorSegSpiral.m_CurveGeometry = ifcSpiral;
                                                ifcAlignment2DHorizontal.m_Segments.Add((ifcHorSegSpiral));

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcHorSegSpiral);
                                                ifcWriter.insertEntity(ifcSpiral);
                                                ifcWriter.insertEntity(ifcSpiralStartPoint);

                                                //Start next Element:
                                                break;
                                        }
                                    }
                                }



                                //----------------------Vertical Alignment(s)--------------------------------------------------
                                foreach (ObjectId profileId in civilAlignment.GetProfileIds())
                                {
                                    var civilProfile =
                                        (Profile)civilTransactionManager.GetObject(profileId, OpenMode.ForRead);

                                    if (civilProfile.StyleName == "Existing Ground Profile") continue;

                                    //Ifc Vertical Alignment
                                    IfcAlignment2DVertical ifcAlignment2DVertical = new IfcAlignment2DVertical()
                                    {
                                        m_Segments = new List<IfcAlignment2DVerticalSegment>()
                                    };
                                    ifcAlignmentCurve.o_Vertical = ifcAlignment2DVertical;
                                    ifcWriter.insertEntity(ifcAlignment2DVertical);


                                    //Change second and third entity:
                                    var civilProfileEntityList = SortC3DProfileEntities(civilProfile);
                                    foreach (var verAlignmentEntity in civilProfileEntityList)
                                    {
                                        switch (verAlignmentEntity.EntityType) //if Element-Type is a...
                                        {
                                            //...Tangent:
                                            case ProfileEntityType.Tangent:
                                                var verTangent = (ProfileTangent)verAlignmentEntity;

                                                IfcAlignment2DVerSegLine ifcVerSegLine = new IfcAlignment2DVerSegLine()
                                                {
                                                    m_StartDistAlong = new IfcLengthMeasure()
                                                    {
                                                        _value = verTangent.StartStation
                                                    },
                                                    m_HorizontalLength = new IfcPositiveLengthMeasure()
                                                    {
                                                        _value = new IfcLengthMeasure()
                                                        {
                                                            _value = verTangent.Length
                                                        }
                                                    },
                                                    m_StartGradient = new IfcRatioMeasure()
                                                    {
                                                        _value = verTangent.Grade
                                                    },
                                                    m_StartHeight = new IfcLengthMeasure()
                                                    {
                                                        _value = verTangent.StartElevation
                                                    }
                                                };

                                                //Write Attributes to other Entities
                                                ifcAlignment2DVertical.m_Segments.Add(ifcVerSegLine);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcVerSegLine);

                                                //Start next Element:
                                                break;

                                            //... Asymmetric Parabola:
                                            case ProfileEntityType.ParabolaAsymmetric:
                                                var verParabolaA = (ProfileParabolaAsymmetric)verAlignmentEntity;

                                                //Ifc VerSegParabolicArc
                                                //Get Parabola 1 Parameters:
                                                IfcAlignment2DVerSegParabolicArc ifcVerSegParabola1 =
                                                    new IfcAlignment2DVerSegParabolicArc()
                                                    {
                                                        m_HorizontalLength = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaA.AsymmetricLength1
                                                            }
                                                        },
                                                        m_StartHeight = new IfcLengthMeasure()
                                                        {
                                                            _value = verParabolaA.StartElevation
                                                        },
                                                        m_StartGradient = new IfcRatioMeasure()
                                                        {
                                                            _value = verParabolaA.GradeIn
                                                        },
                                                        m_StartDistAlong = new IfcLengthMeasure()
                                                        {
                                                            _value = verParabolaA.StartStation
                                                        },
                                                        m_IsConvex =
                                                            CheckForConvexity(verAlignmentEntity),
                                                        m_ParabolaConstant = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaA.K * 100
                                                            }
                                                        }
                                                    };

                                                IfcAlignment2DVerSegParabolicArc ifcVerSegParabola2 =
                                                    new IfcAlignment2DVerSegParabolicArc()
                                                    {
                                                        m_HorizontalLength = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaA.AsymmetricLength2
                                                            }
                                                        },
                                                        m_StartHeight = new IfcLengthMeasure()
                                                        {
                                                            _value = ifcVerSegParabola1.m_StartHeight._value +
                                                                     ifcVerSegParabola1.m_StartGradient._value *
                                                                     ifcVerSegParabola1.m_HorizontalLength._value
                                                                         ._value + ifcVerSegParabola1.m_HorizontalLength
                                                                         ._value._value *
                                                                     ifcVerSegParabola1.m_HorizontalLength._value
                                                                         ._value / (200 * ifcVerSegParabola1
                                                                                        .m_ParabolaConstant._value
                                                                                        ._value)
                                                        },
                                                        m_StartGradient = GetParabola2Gradient(verParabolaA),
                                                        m_StartDistAlong = new IfcLengthMeasure()
                                                        {
                                                            _value = verParabolaA.StartStation + ifcVerSegParabola1
                                                                         .m_HorizontalLength._value._value
                                                        },
                                                        m_ParabolaConstant = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaA.K * 100
                                                            }
                                                        },
                                                    };
                                                ifcVerSegParabola2.m_IsConvex =
                                                    CheckForConvexity(
                                                        verAlignmentEntity);

                                                //Write Attributes to other Entities
                                                ifcAlignment2DVertical.m_Segments.Add(ifcVerSegParabola1);
                                                ifcAlignment2DVertical.m_Segments.Add(ifcVerSegParabola2);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcVerSegParabola1);
                                                ifcWriter.insertEntity(ifcVerSegParabola2);

                                                //Start next Element:
                                                break;

                                            //...symmetric Parabola:
                                            case ProfileEntityType.ParabolaSymmetric:
                                                var verParabolaS = (ProfileParabolaSymmetric)verAlignmentEntity;

                                                //Ifc VerSegParabolicArc
                                                IfcAlignment2DVerSegParabolicArc ifcVerSegParabola =
                                                    new IfcAlignment2DVerSegParabolicArc()
                                                    {
                                                        m_HorizontalLength = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaS.Length
                                                            }
                                                        },
                                                        m_StartHeight = new IfcLengthMeasure()
                                                        {
                                                            _value = verParabolaS.StartElevation
                                                        },
                                                        m_StartGradient = new IfcRatioMeasure()
                                                        {
                                                            _value = verParabolaS.GradeIn
                                                        },
                                                        m_StartDistAlong = new IfcLengthMeasure()
                                                        {
                                                            _value = verParabolaS.StartStation
                                                        },
                                                        m_ParabolaConstant = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verParabolaS.Radius
                                                            }
                                                        }
                                                    };
                                                ifcVerSegParabola.m_IsConvex =
                                                    CheckForConvexity(verAlignmentEntity);

                                                //Write Attributes to other Entities
                                                ifcAlignment2DVertical.m_Segments.Add(ifcVerSegParabola);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcVerSegParabola);

                                                //Start next Element:
                                                break;

                                            //...Circular Arc:
                                            case ProfileEntityType.Circular:
                                                var verCirc = (ProfileCircular)verAlignmentEntity;

                                                //Ifc VerSegCircularArc
                                                IfcAlignment2DVerSegCircularArc ifcVerSegArc =
                                                    new IfcAlignment2DVerSegCircularArc()
                                                    {
                                                        m_HorizontalLength = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verCirc.Length
                                                            }
                                                        },
                                                        m_StartHeight = new IfcLengthMeasure()
                                                        {
                                                            _value = verCirc.StartElevation
                                                        },
                                                        m_StartDistAlong = new IfcLengthMeasure()
                                                        {
                                                            _value = verCirc.StartStation
                                                        },
                                                        m_StartGradient = new IfcRatioMeasure()
                                                        {
                                                            _value = verCirc.GradeIn
                                                        },
                                                        m_Radius = new IfcPositiveLengthMeasure()
                                                        {
                                                            _value = new IfcLengthMeasure()
                                                            {
                                                                _value = verCirc.Radius
                                                            }
                                                        },
                                                        m_IsConvex = CheckForConvexity(verAlignmentEntity)
                                                    };

                                                //Write Attributes to other Entities
                                                ifcAlignment2DVertical.m_Segments.Add(ifcVerSegArc);

                                                //Insert Entities
                                                ifcWriter.insertEntity(ifcVerSegArc);

                                                //Start next Element:
                                                break;
                                        }
                                    }

                                }
                            }
                            //Create the IfcFile
                            TextWriter file = new StreamWriter(dlg.FileName);
                            var ifcfile = ifcWriter.WriteFile();

                            foreach (var s in ifcfile)
                            {
                                file.WriteLine(s); //Write Element-List to File
                            }
                            file.Close();
                        }
                    }

                    //----------------------------  Success Message  -----------------------------
                    if (File.Exists(dlg.FileName)) //if File exists...
                    {
                        Application.ShowAlertDialog("IFC-File created succesfully!"); //...success!
                    }
                    //----------------------------------------------------------------------------
                }
            }
        }
        public class ImportCmdHandler : ICommand
        {
            public bool CanExecute(object parameter)
            {
                return true;
            }

            public event EventHandler CanExecuteChanged;

            public void Execute(object parameter)
            {
                if (parameter is RibbonButton) //if Button is clicked...
                {
                    string esc = "";

                    string cmds = (string)Application.GetSystemVariable("CMDNAMES");
                    if (cmds.Length > 0)
                    {
                        int cmdNum = cmds.Split(new char[] { '\'' }).Length;

                        for (int i = 0; i < cmdNum; i++)
                            esc += '\x03';
                    }
                    string cmdString = parameter.GetType().GetProperty("CommandParameter").GetValue(parameter, null) as string;

                    if (!String.IsNullOrEmpty(cmdString))
                        Application.DocumentManager.MdiActiveDocument.SendStringToExecute(esc + cmdString, true, false, true);
                }
            }
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    }
}



