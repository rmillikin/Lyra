using MassSpectrometry;
using RDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IO.Thermo;
using IO.MzML;
using UsefulProteomicsDatabases;
using Microsoft.Win32;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Shapes;
using Chemistry;

namespace Lyra
{
    public enum FileType { DeconvolutionTSV, MetaMorpheusPsmTsv, RawFile }

    public partial class MainPage
    {
        private Matrix WtoDMatrix, DtoWMatrix;
        private ObservableCollection<ChromatographicPeak> DeconvolutedFeatures = new ObservableCollection<ChromatographicPeak>();
        Dictionary<int, SolidColorBrush> chargeToColor;
        Dictionary<double, SolidColorBrush> intensityToColor;
        IMsDataFile<IMsDataScan<IMzSpectrum<IMzPeak>>> rawFile;
        private int fontSize = 8;
        private double lineStroke = 1;

        public MainPage()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string dir = System.IO.Directory.GetCurrentDirectory();
            Loaders.LoadElements(dir + @"\elements.dat");
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
            if (chargeToColor == null)
            {
                var converter = new BrushConverter();
                chargeToColor = new Dictionary<int, SolidColorBrush>();
                chargeToColor.Add(0, Brushes.Maroon);

                chargeToColor.Add(1, Brushes.DeepPink);
                chargeToColor.Add(2, Brushes.Purple);
                chargeToColor.Add(3, Brushes.Blue);
                chargeToColor.Add(4, Brushes.Green);
                chargeToColor.Add(5, Brushes.Gold);
                chargeToColor.Add(6, Brushes.Orange);
                //chargeToColor.Add(7, Brushes.DarkCyan);
                //chargeToColor.Add(8, Brushes.DimGray);
                //chargeToColor.Add(9, Brushes.Firebrick);
                //chargeToColor.Add(10, Brushes.LimeGreen);

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
                    dataPointsForEachCharge[zIndex].Add(new Tuple<double, double>(env.retentionTime, env.intensity));
            }

            canvas.Children.Clear();

            if (xAxisUpperbound - xAxisLowerbound == 0)
                return;

            double xAxisStep = (xAxisUpperbound - xAxisLowerbound) / 5;
            double yAxisStep = (yAxisUpperbound - yAxisLowerbound) / 5;

            DrawGraphAxes(canvas, xAxisLowerbound, xAxisUpperbound, xAxisStep, yAxisLowerbound, yAxisUpperbound, yAxisStep, 20, "{0:0.00}", "0.0E0");

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
                    polyline.StrokeThickness = lineStroke;
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
                    Canvas.SetTop(rectangle, offset + 5);
                    Canvas.SetLeft(rectangle, canvas.ActualWidth - 45);

                    TextBlock textBlock = new TextBlock();
                    textBlock.FontSize = fontSize;
                    textBlock.Text = "z=" + (j + 1) + "; " + dataPoints.Count;
                    Canvas.SetTop(textBlock, offset + 1);
                    Canvas.SetLeft(textBlock, canvas.ActualWidth - 39);

                    offset += 12;

                    canvas.Children.Add(rectangle);
                    canvas.Children.Add(textBlock);
                }
            }

            TextBlock sbr = new TextBlock();
            sbr.Text = peak.GetSignalToBaseline().ToString("F1");
            Canvas.SetTop(sbr, 0);
            Canvas.SetLeft(sbr, 0);
            canvas.Children.Add(sbr);

            TextBlock peakcount = new TextBlock();
            peakcount.Text = DeconvolutedFeatures.Count.ToString();
            Canvas.SetTop(peakcount, 20);
            Canvas.SetLeft(peakcount, 0);
            canvas.Children.Add(peakcount);
        }

        private void DrawHeatmapThing(ChromatographicPeak peak, Canvas canvas)
        {
            var allPeaks = peak.isotopicEnvelopes.SelectMany(p => p.peaks).ToList();
            List<Tuple<double, int, double>> datapoints = new List<Tuple<double, int, double>>();

            double xAxisLowerbound = peak.isotopicEnvelopes.Min(p => p.retentionTime);
            double xAxisUpperbound = peak.isotopicEnvelopes.Max(p => p.retentionTime);
            double yAxisLowerbound = -1;
            double yAxisUpperbound = 20;
            double xAxisStep = (xAxisUpperbound - xAxisLowerbound) / 5;
            double yAxisStep = 1;

            if (xAxisLowerbound == xAxisUpperbound)
            {
                xAxisLowerbound--;
                xAxisUpperbound++;
            }

            canvas.Children.Clear();
            DrawGraphAxes(canvas, xAxisLowerbound, xAxisUpperbound, xAxisStep, yAxisLowerbound, yAxisUpperbound, yAxisStep, 20, "{0:0.00}", "F0");
            

            foreach (var env in peak.isotopicEnvelopes)
            {
                foreach (var msPeak in env.peaks)
                {
                    double peakMass = ClassExtensions.ToMass(msPeak.mz, env.charge);
                    int massDiff = (int)Math.Round(peakMass - peak.mass, 0);

                    datapoints.Add(new Tuple<double, int, double>(env.retentionTime, massDiff, msPeak.intensity / env.charge));
                }
            }

            if (intensityToColor == null)
            {
                intensityToColor = new Dictionary<double, SolidColorBrush>();
                intensityToColor.Add(0.0, Brushes.Blue);
                intensityToColor.Add(0.2, Brushes.Violet);
                intensityToColor.Add(0.4, Brushes.Green);
                intensityToColor.Add(0.6, Brushes.Gold);
                intensityToColor.Add(0.8, Brushes.Orange);
                intensityToColor.Add(1.0, Brushes.Red);
            }

            // Make some data sets.
            PointCollection points = new PointCollection();
            List<Tuple<double, double, double>> xyz = new List<Tuple<double, double, double>>();

            var dataGroupedByMassDiff = datapoints.GroupBy(p => p.Item2);

            foreach (var massDiff in dataGroupedByMassDiff)
            {
                var group = massDiff.ToList().GroupBy(v => v.Item1);

                foreach (var g in group)
                {
                    var h = g.ToList();

                    double intensityHere = h.Sum(v => v.Item3);

                    xyz.Add(new Tuple<double, double, double>(h.First().Item1, h.First().Item2, intensityHere));
                }
            }

            double maxIntensity = xyz.Max(p => p.Item3);

            var timepoints = xyz.GroupBy(p => p.Item1).OrderBy(v => v.Key);

            double diff = (timepoints.Last().First().Item1 - timepoints.First().First().Item1);
            int numtimepoints = timepoints.Count();
            Point tick1W = WtoD(new Point(0, 0));
            Point tick2W = WtoD(new Point(diff / numtimepoints, 0));
            double widthOfHeatmapRectangle = tick2W.X - tick1W.X;

            double summedMaxColumn = timepoints.Max(p => p.Sum(v => v.Item3));

            foreach (var timepointIsotopes in timepoints)
            {
                var list = timepointIsotopes.ToList();

                double maxOfColumn = list.Max(p => p.Item3);

                foreach (var point in timepointIsotopes)
                {
                    Point p = new Point(point.Item1, point.Item2);
                    var transformedPoint = WtoD(p);
                    points.Add(transformedPoint);

                    var rectangle = new Rectangle();

                    var dfg = GetHeatmapColor(point.Item3 / maxOfColumn);
                    //double opacity = (byte)(255 * (list.Sum(g => g.Item3) / summedMaxColumn));

                    SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(255, (byte)dfg.Item1, (byte)dfg.Item2, (byte)dfg.Item3));

                    rectangle.Stroke = brush;
                    rectangle.Fill = brush;
                    rectangle.Height = 4;
                    rectangle.Width = widthOfHeatmapRectangle;
                    Canvas.SetLeft(rectangle, transformedPoint.X - rectangle.Width / 2);
                    Canvas.SetTop(rectangle, transformedPoint.Y - rectangle.Height / 2);
                    canvas.Children.Add(rectangle);

                    //TextBlock ratio = new TextBlock();
                    //ratio.Text = ((point.Item3 / maxOfColumn) * 100).ToString("F0");
                    //ratio.FontSize = 8;
                    //Canvas.SetLeft(ratio, transformedPoint.X - rectangle.Width / 2);
                    //Canvas.SetTop(ratio, transformedPoint.Y - rectangle.Height / 2 - 2);
                    //canvas.Children.Add(ratio);
                }
            }
        }

        private void DrawMassSpectrum(MassSpectrum massSpectrum, Canvas canvas)
        {

        }

        private void LoadData(string path, FileType fileType)
        {
            if (fileType == FileType.DeconvolutionTSV)
            {
                System.IO.StreamReader reader = new System.IO.StreamReader(path);
                string line;
                int lineNum = 1;

                while (reader.Peek() > 0)
                {
                    line = reader.ReadLine();
                    List<IsotopicEnvelope> envs = new List<IsotopicEnvelope>();

                    if (lineNum != 1)
                    {
                        var parsedLine = line.Split('\t');
                        var mass = double.Parse(parsedLine[0]);
                        var apexRt = double.Parse(parsedLine[10]);

                        var envelopes = parsedLine[17].Split(new string[] { "[", "]" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var envelope in envelopes)
                        {
                            var split = envelope.Split(new string[] { "|", "(", ")" }, StringSplitOptions.RemoveEmptyEntries);
                            int charge = int.Parse(split[0]);
                            double rt = double.Parse(split[1]);
                            int scan = int.Parse(split[2]);
                            List<MassSpectralPeak> peaks = new List<MassSpectralPeak>();

                            for (int i = 3; i < split.Length; i++)
                            {
                                string[] sp = split[i].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                                MassSpectralPeak peak = new MassSpectralPeak(double.Parse(sp[0]), double.Parse(sp[1]));
                                peaks.Add(peak);
                            }

                            IsotopicEnvelope env = new IsotopicEnvelope(rt, charge, peaks);
                            envs.Add(env);
                        }

                        var gsdf = envs.GroupBy(p => p.charge).Where(v => v.Count() > 1);

                        if (gsdf.Any())
                        {
                            var deconvolutedFeature = new ChromatographicPeak(envs, mass, apexRt);
                            if (deconvolutedFeature.GetSignalToBaseline() > 2.0)
                                DeconvolutedFeatures.Add(deconvolutedFeature);
                        }
                    }

                    lineNum++;
                }

                reader.Close();
            }
            else if (fileType == FileType.MetaMorpheusPsmTsv)
            {
                System.IO.StreamReader reader = new System.IO.StreamReader(path);
                string line;
                int lineNum = 1;

                while (reader.Peek() > 0)
                {
                    line = reader.ReadLine();
                    List<IsotopicEnvelope> envs = new List<IsotopicEnvelope>();

                    if (lineNum != 1)
                    {
                        var parsedLine = line.Split('\t');
                    }
                }
            }
            else if (fileType == FileType.RawFile)
            {
                string ext = System.IO.Path.GetExtension(path).ToUpperInvariant();

                if (ext.Equals(".RAW"))
                    rawFile = ThermoStaticData.LoadAllStaticData(path);
                if (ext.Equals(".MZML"))
                    rawFile = Mzml.LoadAllStaticData(path);
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
            var chromatographicPeak = listview.SelectedItem as ChromatographicPeak;

            DrawChromatogram(chromatographicPeak, topGraph);
            DrawHeatmapThing(chromatographicPeak, bottomGraph);
            //DrawChromatogram(chromatographicPeak.getSummedChromatographicPeak(), canGraph);
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void _LoadFromMenu(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter =
                "Tab-Delimited Text (*.tsv)|*.tsv|" +
                "MS Data Files (*.raw;*.mzml)|*.raw;*.mzml|" +
                "All files (*.*)|*.*";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Multiselect = false;

            openFileDialog.ShowDialog();

            string path = openFileDialog.FileName;
            var ext = System.IO.Path.GetExtension(path).ToUpperInvariant();

            if (ext.Equals(".MZML") || ext.Equals(".RAW"))
                LoadData(path, FileType.RawFile);
            else if (ext.Equals(".TSV") || ext.Equals(".TXT"))
                LoadData(path, FileType.DeconvolutionTSV);
        }

        private Tuple<double, double, double> GetHeatmapColor(double normalizedValue)
        {
            byte redByte = (byte)(byte.MaxValue * normalizedValue);
            byte blueByte = (byte)(byte.MaxValue - redByte);

            if (normalizedValue >= 1)
            {
                redByte = byte.MaxValue;
                blueByte = 0;
            }

            if (normalizedValue < 0)
            {
                redByte = 0;
                blueByte = byte.MaxValue;
            }

            return new Tuple<double, double, double>(redByte, 0, blueByte);
        }

        private void DrawGraphAxes(Canvas canvas, double xAxisLowerbound, double xAxisUpperbound, double xAxisStep, double yAxisLowerbound, double yAxisUpperbound, double yAxisStep, double graphMargin, string xAxisFormatOptions, string yAxisFormatOptions)
        {
            // prep
            double xMargin = graphMargin;
            double yMargin = graphMargin;
            double canvasXMin = xMargin;
            double canvasXMax = canvas.Width - xMargin;
            double canvasYMin = yMargin;
            double canvasYMax = canvas.Height - yMargin;
            
            CreateTransformationMatrix(
                xAxisLowerbound, xAxisUpperbound, yAxisLowerbound, yAxisUpperbound,
                canvasXMin, canvasXMax, canvasYMax, canvasYMin);
            
            double tickMarkSize = 4;
            
            // x axis
            GeometryGroup xAxis = new GeometryGroup();
            Point xAxisStart = WtoD(new Point(xAxisLowerbound, yAxisLowerbound));
            Point xAxisEnd = WtoD(new Point(xAxisUpperbound, yAxisLowerbound));
            xAxis.Children.Add(new LineGeometry(xAxisStart, xAxisEnd));
            
            for (double x = xAxisLowerbound; x <= xAxisUpperbound; x += xAxisStep)
            {
                Point tick1W = WtoD(new Point(x, yAxisLowerbound));
                Point tick2W = WtoD(new Point(x, yAxisLowerbound));

                Point tick1D = new Point(tick1W.X, tick1W.Y + (tickMarkSize / 2));
                Point tick2D = new Point(tick1W.X, tick1W.Y - (tickMarkSize / 2));

                xAxis.Children.Add(new LineGeometry(tick1D, tick2D));

                DrawGraphText(canvas, String.Format(xAxisFormatOptions, x),
                    new Point(tick2D.X, tick2D.Y + 5), fontSize,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }

            Path xAxisDrawnLine = new Path();
            xAxisDrawnLine.StrokeThickness = lineStroke;
            xAxisDrawnLine.Stroke = Brushes.Black;
            xAxisDrawnLine.Data = xAxis;

            canvas.Children.Add(xAxisDrawnLine);
            
            // y axis
            GeometryGroup yAxis = new GeometryGroup();
            Point yAxisStart = WtoD(new Point(xAxisLowerbound, yAxisLowerbound));
            Point yAxisEnd = WtoD(new Point(xAxisLowerbound, yAxisUpperbound));
            yAxis.Children.Add(new LineGeometry(yAxisStart, yAxisEnd));

            for (double y = yAxisLowerbound; y <= yAxisUpperbound; y += yAxisStep)
            {
                Point tick1W = WtoD(new Point(xAxisLowerbound, y));
                Point tick2W = WtoD(new Point(xAxisLowerbound, y));

                Point tick1D = new Point(tick1W.X + (tickMarkSize / 2), tick1W.Y);
                Point tick2D = new Point(tick1W.X - (tickMarkSize / 2), tick1W.Y);

                yAxis.Children.Add(new LineGeometry(tick1D, tick2D));

                DrawGraphText(canvas, y.ToString(yAxisFormatOptions),
                    new Point(tick2D.X, tick2D.Y), fontSize,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center);
            }

            Path yAxisDrawnLine = new Path();
            yAxisDrawnLine.StrokeThickness = lineStroke;
            yAxisDrawnLine.Stroke = Brushes.Black;
            yAxisDrawnLine.Data = yAxis;

            canvas.Children.Add(yAxisDrawnLine);
        }
    }
}
