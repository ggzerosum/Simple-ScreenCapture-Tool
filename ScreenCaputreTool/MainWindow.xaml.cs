using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing; // System.Drawing.dll 참조 추가 필요
using System.Threading;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Forms;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle; // Screen 클래스 사용을 위해 추가

namespace WpfClipboardCapture
{
    // 설정을 저장하기 위한 클래스
    public class AppSettings
    {
        public double WindowWidth { get; set; } = 320;
        public double WindowHeight { get; set; } = 350;
        public double WindowLeft { get; set; } = -1; // -1은 위치가 설정되지 않았음을 의미
        public double WindowTop { get; set; } = -1;
        public int HistoryLimit { get; set; } = 3;
    }

    public class CaptureHistoryItem
    {
        public CroppedBitmap Image { get; set; }
        public int CaptureNumber { get; set; }
        public string FileSize { get; set; }
        public double PreviewCanvasWidth { get; set; }
        public double PreviewCanvasHeight { get; set; }
        public double PreviewImageX { get; set; }
        public double PreviewImageY { get; set; }
        public double PreviewImageWidth { get; set; }
        public double PreviewImageHeight { get; set; }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<CaptureHistoryItem> RecentCaptures { get; set; }
        
        private int captureCounter = 0;
        private AppSettings settings;
        private static readonly string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");


        public MainWindow()
        {
            InitializeComponent();
            LoadSettings(); // UI 초기화 전에 설정 로드
            this.DataContext = this;
            RecentCaptures = new ObservableCollection<CaptureHistoryItem>();
            this.Closing += MainWindow_Closing; // 창 닫기 이벤트 핸들러 연결
        }
        
        // 창이 닫힐 때 설정을 저장
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(settingsFilePath);
                    settings = JsonSerializer.Deserialize<AppSettings>(json);
                }
                catch
                {
                    settings = new AppSettings(); // 파일이 손상되었을 경우 기본값 사용
                }
            }
            else
            {
                settings = new AppSettings();
            }

            // 불러온 설정 적용
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;

            // 저장된 위치가 현재 화면에 보이는지 확인 후 적용
            if (IsPositionVisible(settings.WindowLeft, settings.WindowTop, this.Width, this.Height))
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = settings.WindowLeft;
                this.Top = settings.WindowTop;
            }
            // 유효하지 않으면 기본값(XAML에 설정된 CenterScreen)으로 실행됨
        }

        // 저장된 위치가 현재 연결된 모니터 중 하나에 표시될 수 있는지 확인하는 메서드
        private bool IsPositionVisible(double left, double top, double width, double height)
        {
            if (left == -1 || top == -1) return false;

            Rect windowBounds = new Rect(left, top, width, height);
            foreach (Screen screen in Screen.AllScreens)
            {
                Rect screenBounds = new Rect(
                    screen.WorkingArea.Left,
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height
                );

                // 창 영역이 화면 영역과 조금이라도 겹치면 유효한 것으로 간주
                if (windowBounds.IntersectsWith(screenBounds))
                {
                    return true;
                }
            }
            return false;
        }


        private void SaveSettings()
        {
            // 창이 Normal 상태일 때만 위치와 크기를 저장
            if (this.WindowState == WindowState.Normal)
            {
                settings.WindowWidth = this.Width;
                settings.WindowHeight = this.Height;
                settings.WindowLeft = this.Left;
                settings.WindowTop = this.Top;
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"설정 저장 중 오류 발생: {ex.Message}");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this.settings.HistoryLimit);
            if (settingsWindow.ShowDialog() == true)
            {
                this.settings.HistoryLimit = settingsWindow.HistoryLimit;
                while (RecentCaptures.Count > this.settings.HistoryLimit)
                {
                    RecentCaptures.RemoveAt(RecentCaptures.Count - 1);
                }
            }
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            Thread.Sleep(200);

            BitmapSource screenBitmap = CaptureFullScreen();
            var captureWindow = new CaptureWindow(screenBitmap);

            if (captureWindow.ShowDialog() == true)
            {
                Int32Rect selectedRect = captureWindow.SelectedRegion;
                if (selectedRect.Width > 0 && selectedRect.Height > 0)
                {
                    var croppedBitmap = new CroppedBitmap(screenBitmap, selectedRect);
                    
                    captureCounter++;
                    string fileSize = GetEncodedImageSize(croppedBitmap);
                    
                    double screenWidth = SystemParameters.VirtualScreenWidth;
                    double screenHeight = SystemParameters.VirtualScreenHeight;
                    double screenAspect = screenWidth / screenHeight;
                    double maxPreviewWidth = 90;
                    double maxPreviewHeight = 80;
                    double previewCanvasWidth = maxPreviewWidth;
                    double previewCanvasHeight = previewCanvasWidth / screenAspect;

                    if (previewCanvasHeight > maxPreviewHeight)
                    {
                        previewCanvasHeight = maxPreviewHeight;
                        previewCanvasWidth = previewCanvasHeight * screenAspect;
                    }

                    double scale = previewCanvasWidth / screenWidth;
                    
                    var historyItem = new CaptureHistoryItem
                    {
                        Image = croppedBitmap,
                        CaptureNumber = captureCounter,
                        FileSize = fileSize,
                        PreviewCanvasWidth = previewCanvasWidth,
                        PreviewCanvasHeight = previewCanvasHeight,
                        PreviewImageX = selectedRect.X * scale,
                        PreviewImageY = selectedRect.Y * scale,
                        PreviewImageWidth = selectedRect.Width * scale,
                        PreviewImageHeight = selectedRect.Height * scale
                    };
                    
                    RecentCaptures.Insert(0, historyItem);
                    
                    if (RecentCaptures.Count > this.settings.HistoryLimit)
                    {
                        RecentCaptures.RemoveAt(RecentCaptures.Count - 1);
                    }

                    EncodeAndCopyToClipboard(croppedBitmap);
                }
            }
            
            this.Show();
        }
        
        private void PreviewItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CaptureHistoryItem itemToCopy)
            {
                EncodeAndCopyToClipboard(itemToCopy.Image);
            }
        }

        private string GetEncodedImageSize(BitmapSource bitmapSource)
        {
            if (bitmapSource == null) return "";
            BitmapEncoder encoder;
            if (rbJpg.IsChecked == true)
            {
                var jpgEncoder = new JpegBitmapEncoder { QualityLevel = (int)qualitySlider.Value };
                encoder = jpgEncoder;
            }
            else
            {
                encoder = new PngBitmapEncoder();
            }
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                double sizeInKb = (double)ms.Length / 1024;
                return $"{sizeInKb:F1} KB";
            }
        }

        private void EncodeAndCopyToClipboard(BitmapSource bitmapSource)
        {
            BitmapEncoder encoder;
            if (rbJpg.IsChecked == true)
            {
                var jpgEncoder = new JpegBitmapEncoder();
                jpgEncoder.QualityLevel = (int)qualitySlider.Value;
                encoder = jpgEncoder;
            }
            else
            {
                encoder = new PngBitmapEncoder();
            }
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                ms.Position = 0;
                var newBitmap = new BitmapImage();
                newBitmap.BeginInit();
                newBitmap.StreamSource = ms;
                newBitmap.CacheOption = BitmapCacheOption.OnLoad;
                newBitmap.EndInit();
                Clipboard.SetImage(newBitmap);
            }
        }

        private BitmapSource CaptureFullScreen()
        {
            int screenLeft = (int)SystemParameters.VirtualScreenLeft;
            int screenTop = (int)SystemParameters.VirtualScreenTop;
            int screenWidth = (int)SystemParameters.VirtualScreenWidth;
            int screenHeight = (int)SystemParameters.VirtualScreenHeight;

            using (var bmp = new System.Drawing.Bitmap(screenWidth, screenHeight))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenLeft, screenTop, 0, 0, bmp.Size);
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    bmp.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
    }
    
    public class CaptureWindow : Window
    {
        public Int32Rect SelectedRegion { get; private set; }
        private System.Windows.Point startPoint;
        private Rectangle selectionRectangle;
        private Canvas selectionCanvas;
        public CaptureWindow(BitmapSource screenBackground)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // WindowState = WindowState.Maximized; // 이 부분을 제거합니다.
            Topmost = true;
            Cursor = Cursors.Cross;

            // --- 수정된 부분 ---
            // 창의 위치와 크기를 전체 가상 화면에 맞게 직접 설정합니다.
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
            // --- 여기까지 ---

            var mainGrid = new Grid();
            mainGrid.Background = new ImageBrush(screenBackground);
            this.Content = mainGrid;
            selectionCanvas = new Canvas();
            selectionCanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 0, 0, 0));
            mainGrid.Children.Add(selectionCanvas);
            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            selectionCanvas.Children.Add(selectionRectangle);
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.KeyDown += OnKeyDown;
        }
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                startPoint = e.GetPosition(this);
                Canvas.SetLeft(selectionRectangle, startPoint.X);
                Canvas.SetTop(selectionRectangle, startPoint.Y);
                selectionRectangle.Width = 0;
                selectionRectangle.Height = 0;
            }
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                var x = Math.Min(startPoint.X, currentPoint.X);
                var y = Math.Min(startPoint.Y, currentPoint.Y);
                var width = Math.Abs(startPoint.X - currentPoint.X);
                var height = Math.Abs(startPoint.Y - currentPoint.Y);
                Canvas.SetLeft(selectionRectangle, x);
                Canvas.SetTop(selectionRectangle, y);
                selectionRectangle.Width = width;
                selectionRectangle.Height = height;
            }
        }
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            SelectedRegion = new Int32Rect((int)Canvas.GetLeft(selectionRectangle), (int)Canvas.GetTop(selectionRectangle), (int)selectionRectangle.Width, (int)selectionRectangle.Height);
            this.DialogResult = true;
            this.Close();
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}