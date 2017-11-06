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

namespace Lyra
{
    public enum FileType { DeconvolutionTSV, MetaMorpheusPsmTsv, RawFile }

    public partial class MainPage
    {
        private Matrix WtoDMatrix, DtoWMatrix;
        private ObservableCollection<ChromatographicPeak> DeconvolutedFeatures = new ObservableCollection<ChromatographicPeak>();
        Dictionary<int, SolidColorBrush> chargeToColor;
        IMsDataFile<IMsDataScan<IMzSpectrum<IMzPeak>>> rawFile;

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
            if(chargeToColor == null)
            {
                var converter = new BrushConverter();
                chargeToColor = new Dictionary<int, SolidColorBrush>();
                chargeToColor.Add(1, Brushes.DeepPink);
                chargeToColor.Add(2, Brushes.Purple);
                chargeToColor.Add(3, Brushes.Blue);
                chargeToColor.Add(4, Brushes.Green);
                chargeToColor.Add(5, Brushes.Gold);
                chargeToColor.Add(6, Brushes.Orange);
                chargeToColor.Add(7, Brushes.DarkCyan);
                chargeToColor.Add(8, Brushes.DimGray);
                chargeToColor.Add(9, Brushes.Firebrick);
                chargeToColor.Add(10, Brushes.LimeGreen);

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
            if (fileType == FileType.DeconvolutionTSV)
            {
                var lines = System.IO.File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                        continue;

                    var deconvolutedFeature = new ChromatographicPeak();

                    var parsedLine = lines[i].Split('\t');
                    var mass = double.Parse(parsedLine[0]);
                    var apexRt = double.Parse(parsedLine[10]);
                    var chargeStates = parsedLine[16].Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach(var chargeState in chargeStates)
                    {
                        int z = int.Parse(chargeState.Split('|')[0]);
                        var str = chargeState.Split(new string[] { "]", ",", "|" }, StringSplitOptions.RemoveEmptyEntries);

                        for(int j = 1; j < str.Length; j++)
                        {
                            var h = str[j].Split(';');
                            double intensity = double.Parse(h[1]);
                            var env = new IsotopicEnvelope(intensity, double.Parse(h[0]), z);
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
            var t = listview.SelectedItem as ChromatographicPeak;
            
            DrawChromatogram(t, canGraph);
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

            if(ext.Equals(".MZML") || ext.Equals(".RAW"))
                LoadData(path, FileType.RawFile);
            else if(ext.Equals(".TSV") || ext.Equals(".TXT"))
                LoadData(path, FileType.DeconvolutionTSV);
        }
    }
}
