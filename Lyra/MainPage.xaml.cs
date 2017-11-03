using MassSpectrometry;
using RDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Lyra
{
    public enum FileType { DeconvolutionTSV, MetaMorpheusPsmTsv, RawFile }

    public partial class MainPage
    {
        private Matrix WtoDMatrix, DtoWMatrix;
        private List<ChromatographicPeak> DeconvolutedFeatures;
        Dictionary<int, SolidColorBrush> chargeToColor;
        IMsDataFile<IMsDataScan<IMzSpectrum<IMzPeak>>> rawFile;

        public MainPage()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string deconvolutionResults = @"C:\Users\rmillikin\Desktop\MS1Decon\DeconvolutionOutput-2017-11-02-16-36-05.tsv";
            string ms2Results = @"C:\Users\rmillikin\Desktop\MS1Decon\output.tsv";
            string rawFilePath = @"C:\Data\ionstarSample\B02_06_161103_A1_HCD_OT_4ul.raw";
            
            LoadData(deconvolutionResults, FileType.DeconvolutionTSV);
            LoadData(ms2Results, FileType.MetaMorpheusPsmTsv);
            //LoadData(rawFilePath, FileType.RawFile);

            listView.ItemsSource = DeconvolutedFeatures;
            /*
            REngine R;
            REngine.SetEnvironmentVariables();
            R = REngine.GetInstance();
            R.Initialize();
            R.Evaluate("source(\"https://bioconductor.org/biocLite.R\")");
            try
            {
                R.Evaluate("biocLite(\"Prostar\")");
            }
            catch(Exception ex)
            {

            }
            R.Evaluate("");
            R.Dispose();
            */
        }

        private void DrawChromatogram(ChromatographicPeak peak, Canvas canvas)
        {
            if(chargeToColor == null)
            {
                chargeToColor = new Dictionary<int, SolidColorBrush>();
                chargeToColor.Add(1, Brushes.DeepPink);
                chargeToColor.Add(2, Brushes.Purple);
                chargeToColor.Add(3, Brushes.Blue);
                chargeToColor.Add(4, Brushes.Green);
                chargeToColor.Add(5, Brushes.Gold);
                chargeToColor.Add(6, Brushes.Orange);

                chargeToColor.Add(int.MinValue, Brushes.Black);
            }

            List<Tuple<double, double>>[] dataPointsForEachCharge = new List<Tuple<double, double>>[peak.isotopicEnvelopes.Max(p => p.charge)];
            
            double xAxisLowerbound = double.MaxValue;
            double xAxisUpperbound = 0;
            double yAxisLowerbound = 0;
            double yAxisUpperbound = 0;

            for (int zIndex = 0; zIndex < dataPointsForEachCharge.Length; zIndex++)
            {
                int z = zIndex + 1;
                
                dataPointsForEachCharge[zIndex] = new List<Tuple<double, double>>();
                var g = peak.isotopicEnvelopes.Where(p => p.charge == z).OrderBy(p => p.retentionTime).ToList();

                if (g.Any())
                {
                    double rt = g.Min(p => p.retentionTime);
                    if (rt < xAxisLowerbound)
                        xAxisLowerbound = rt;
                    rt = g.Max(p => p.retentionTime);
                    if (rt > xAxisUpperbound)
                        xAxisUpperbound = rt;

                    double inten = g.Max(p => p.intensity);
                    if (inten > yAxisUpperbound)
                        yAxisUpperbound = inten;
                }

                foreach (var env in g)
                    dataPointsForEachCharge[zIndex].Add(new Tuple<double, double> (env.retentionTime, env.intensity));
            }

            canvas.Children.Clear();

            if (xAxisUpperbound - xAxisLowerbound == 0)
                return;
            
            double xAxisStep = (xAxisUpperbound - xAxisLowerbound) / 5;
            double yAxisStep = (yAxisUpperbound - yAxisLowerbound) / 5;

            double xMargin = canvas.ActualWidth * 0.1;
            double yMargin = canvas.ActualHeight * 0.1;
            double canvasXMin = xMargin;
            double canvasXMax = canvas.Width - xMargin;
            double canvasYMin = yMargin;
            double canvasYMax = canvas.Height - yMargin;

            // Prepare the transformation matrices.
            CreateTransformationMatrix(
                xAxisLowerbound, xAxisUpperbound, yAxisLowerbound, yAxisUpperbound,
                canvasXMin, canvasXMax, canvasYMax, canvasYMin);

            // Get the tic mark lengths.
            double tickMarkSize = 3;
            
            // Make the X axis.
            Point p0 = DtoW(new Point(xAxisLowerbound, yAxisLowerbound));
            Point p1 = DtoW(new Point(tickMarkSize, tickMarkSize));
            double ytic = p1.Y - p0.Y;
            GeometryGroup xAxis = new GeometryGroup();
            p0 = new Point(xAxisLowerbound, yAxisLowerbound);
            p1 = new Point(xAxisUpperbound, yAxisLowerbound);
            xAxis.Children.Add(new LineGeometry(WtoD(p0), WtoD(p1)));

            for (double x = xAxisLowerbound; x <= xAxisUpperbound; x += xAxisStep)
            {
                // Add the tic mark.
                Point tic0 = WtoD(new Point(x, -ytic));
                Point tic1 = WtoD(new Point(x, ytic));
                xAxis.Children.Add(new LineGeometry(tic0, tic1));
                
                // Label the tic mark's X coordinate.
                DrawGraphText(canvas, x.ToString(),
                    new Point(tic0.X, tic0.Y + 5), 12,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }

            Path xAxisDrawnLine = new Path();
            xAxisDrawnLine.StrokeThickness = 1;
            xAxisDrawnLine.Stroke = Brushes.Black;
            xAxisDrawnLine.Data = xAxis;

            canvas.Children.Add(xAxisDrawnLine);

            // Make the Y axis.
            p0 = DtoW(new Point(xAxisLowerbound, yAxisLowerbound));
            p1 = DtoW(new Point(tickMarkSize, tickMarkSize));
            double xtic = p1.X - p0.X;
            GeometryGroup yAxis = new GeometryGroup();
            p0 = new Point(xAxisLowerbound, yAxisLowerbound);
            p1 = new Point(xAxisLowerbound, yAxisUpperbound);
            yAxis.Children.Add(new LineGeometry(WtoD(p0), WtoD(p1)));

            for (double y = yAxisLowerbound; y <= yAxisUpperbound; y += yAxisStep)
            {
                // Add the tic mark.
                Point tic0 = WtoD(new Point(xtic, y));
                Point tic1 = WtoD(new Point(-xtic, y));
                xAxis.Children.Add(new LineGeometry(tic0, tic1));
                
                // Label the tic mark's Y coordinate.
                DrawGraphText(canvas, y.ToString("0.0e0"),
                    new Point(tic0.X - 17, tic0.Y), 12,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }

            Path yaxis_path = new Path();
            yaxis_path.StrokeThickness = 1;
            yaxis_path.Stroke = Brushes.Black;
            yaxis_path.Data = yAxis;

            canvas.Children.Add(yaxis_path);

            int offset = 0;
            // Make some data sets.
            bool observedSomethingYet = false;
            for (int j = 0; j < dataPointsForEachCharge.Length; j++)
            {
                PointCollection points = new PointCollection();

                var dataPoints = dataPointsForEachCharge[j];
                if (dataPoints.Any())
                    observedSomethingYet = true;

                if (observedSomethingYet)
                {
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        Point p = new Point(dataPoints[i].Item1, dataPoints[i].Item2);
                        var transformedPoint = WtoD(p);
                        points.Add(transformedPoint);
                    }

                    Polyline polyline = new Polyline();
                    polyline.StrokeThickness = 1;
                    SolidColorBrush brushColor;
                    if (chargeToColor.TryGetValue(j + 1, out brushColor))
                    {

                    }
                    else
                        brushColor = chargeToColor[int.MinValue];

                    polyline.Stroke = brushColor;
                    polyline.Points = points;

                    canvas.Children.Add(polyline);

                    var rectangle = new Rectangle();
                    rectangle.Stroke = brushColor;
                    rectangle.Fill = brushColor;
                    rectangle.Height = 4;
                    rectangle.Width = 4;
                    Canvas.SetTop(rectangle, offset + 50);
                    Canvas.SetLeft(rectangle, canvas.ActualWidth - 50);

                    TextBlock textBlock = new TextBlock();
                    textBlock.Text = "z=" + (j + 1) + "; " + dataPoints.Count;
                    Canvas.SetTop(textBlock, offset + 43);
                    Canvas.SetLeft(textBlock, canvas.ActualWidth - 40);

                    offset += 20;

                    canvas.Children.Add(rectangle);
                    canvas.Children.Add(textBlock);
                }
            }
        }

        private void DrawMassSpectrum(MassSpectrum massSpectrum, Canvas canvas)
        {

        }

        private void LoadData(string path, FileType fileType)
        {
            string[] parsedLine;
            double mass;
            double apexRt;
            string[] z;
            int ch;
            string[] str;

            if (fileType == FileType.DeconvolutionTSV)
            {
                DeconvolutedFeatures = new List<ChromatographicPeak>();
                var lines = System.IO.File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                        continue;

                    var deconvolutedFeature = new ChromatographicPeak();

                    parsedLine = lines[i].Split('\t');
                    mass = double.Parse(parsedLine[0]);
                    apexRt = double.Parse(parsedLine[10]);
                    z = parsedLine[15].Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach(var charge in z)
                    {
                        ch = (int)char.GetNumericValue(charge[0]);
                        str = charge.Split(new string[] { "]", ",", "|" }, StringSplitOptions.RemoveEmptyEntries);

                        for(int j = 1; j < str.Length; j++)
                        {
                            var h = str[j].Split(';');
                            double intensity = double.Parse(h[1]);
                            var env = new IsotopicEnvelope(intensity, double.Parse(h[0]), ch);
                            deconvolutedFeature.isotopicEnvelopes.Add(env);
                        }
                    }

                    
                    deconvolutedFeature.mass = mass;
                    deconvolutedFeature.apexRt = apexRt;

                    DeconvolutedFeatures.Add(deconvolutedFeature);
                }
            }
            else if (fileType == FileType.MetaMorpheusPsmTsv)
            {

            }
            else if (fileType == FileType.RawFile)
            {
                string ext = System.IO.Path.GetExtension(path).ToUpperInvariant();

                if (ext.Equals(".RAW"))
                    rawFile = IO.Thermo.ThermoStaticData.LoadAllStaticData(path);
                else if (ext.Equals(".MZML"))
                    rawFile = IO.MzML.Mzml.LoadAllStaticData(path);
                else
                    throw new Exception("Cannot read file format: " + ext);
            }
            else
            {
                throw new Exception("Cannot read file " + path);
            }
        }

        private void CreateTransformationMatrix(double wxmin, double wxmax, double wymin, double wymax, double dxmin, double dxmax, double dymin, double dymax)
        {
            // Make WtoD.
            WtoDMatrix = Matrix.Identity;
            WtoDMatrix.Translate(-wxmin, -wymin);

            double xscale = (dxmax - dxmin) / (wxmax - wxmin);
            double yscale = (dymax - dymin) / (wymax - wymin);
            WtoDMatrix.Scale(xscale, yscale);

            WtoDMatrix.Translate(dxmin, dymin);

            // Make DtoW.
            DtoWMatrix = WtoDMatrix;
            DtoWMatrix.Invert();
        }

        private Point WtoD(Point point)
        {
            return WtoDMatrix.Transform(point);
        }

        private Point DtoW(Point point)
        {
            return DtoWMatrix.Transform(point);
        }

        private void DrawGraphText(Canvas can, string text, Point location, double font_size, HorizontalAlignment halign, VerticalAlignment valign)
        {
            // Make the label.
            Label label = new Label();
            label.Content = text;
            label.FontSize = font_size;
            can.Children.Add(label);

            // Position the label.
            label.Measure(new Size(double.MaxValue, double.MaxValue));

            double x = location.X;
            if (halign == HorizontalAlignment.Center)
                x -= label.DesiredSize.Width / 2;
            else if (halign == HorizontalAlignment.Right)
                x -= label.DesiredSize.Width;
            Canvas.SetLeft(label, x);

            double y = location.Y;
            if (valign == VerticalAlignment.Center)
                y -= label.DesiredSize.Height / 2;
            else if (valign == VerticalAlignment.Bottom)
                y -= label.DesiredSize.Height;
            Canvas.SetTop(label, y);
        }

        private void listNode_Selected(object sender, SelectionChangedEventArgs e)
        {
            var listview = sender as ListView;
            var t = listview.SelectedItem as ChromatographicPeak;
            
            List<Tuple<double, double>> dataPoints = new List<Tuple<double, double>>();
            
            DrawChromatogram(t, canGraph);
        }
    }
}
