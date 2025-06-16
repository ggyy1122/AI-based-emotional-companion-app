using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OpenCvSharp.XImgProc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HandAngleDemo
{
    public partial class HandGesture
    {
        private VideoCapture? _cap;
  
        private HandLandmarkDetector _detector;
        private const int CameraWidth = 640;
        private const int CameraHeight = 480;

        public HandGesture()
        {
            // 使用应用程序基目录，而非动态计算路径
            string modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, // 指向EXE所在目录
                "Models",
                "hand_landmark_sparse_Nx3x224x224.onnx"
            );

            // 主动检查文件是否存在
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    $"模型文件未找到！请确保以下路径存在文件:\n{modelPath}\n" +
                    "解决方案:\n" +
                    "1. 在输出目录创建Models文件夹\n" +
                    "2. 将模型文件复制到该文件夹中",
                    modelPath
                );
            }

            _detector = new HandLandmarkDetector(modelPath);
            StartCamera();
        }

        public void StartCamera()
        {

            _cap = new VideoCapture(0);
            _cap.Set(VideoCaptureProperties.FrameWidth, CameraWidth);
            _cap.Set(VideoCaptureProperties.FrameHeight, CameraHeight);

            //_timer = new DispatcherTimer();
            //_timer.Interval = TimeSpan.FromMilliseconds(33);
            //_timer.Tick += OnFrame;
            //_timer.Start();
        }

        public void StopCamera()
        {
            if (_cap != null)
            {
                // 停止任何正在进行的帧捕获（重要！）
                _cap.Grab(); // 清除缓冲区

                _cap.Release();
                _cap.Dispose();
                _cap = null;
            }
        }

        public void OnFrame(Image CameraImage,ref double __angle, object? sender, EventArgs e)
        {
            
            using var frame = new Mat();
            if (!_cap.Read(frame) || frame.Empty())
                return;
            Cv2.Flip(frame, frame, FlipMode.Y);
            float[,] landmarks = _detector.DetectLandmarks(frame);
            


            if (landmarks != null)
            {
                // 画21个点
                for (int i = 0; i < 21; i++)
                {
                    int px = (int)(landmarks[i, 0] * frame.Width);
                    int py = (int)(landmarks[i, 1] * frame.Height);
                    //Cv2.Circle(frame, new OpenCvSharp.Point(px, py), 5, Scalar.Lime, -1);
                }

                // 食指根部(5), 食指指尖(8)
                var basePt = new Point2f(landmarks[1, 0] * frame.Width, landmarks[1, 1] * frame.Height);
                var tipPt = new Point2f(landmarks[4, 0] * frame.Width, landmarks[4, 1] * frame.Height);

                //infText.Text = $"{landmarks[5, 0]},{landmarks[5, 1]},{landmarks[8, 0]},{landmarks[8, 1]}";

                //Cv2.Line(frame, basePt.ToPoint(), tipPt.ToPoint(), Scalar.Red, 2);
               

                double angle = HandAngleHelper.GetIndexFingerAngle(basePt, tipPt);

                __angle = angle;


                int len = 300;

                double angleInRadians = angle / 180 * Math.PI;
                

                var basePt1 = new Point2d(CameraWidth/2,CameraHeight/2);
                var tipPt1 = new Point2d(CameraWidth / 2+Math.Cos(angleInRadians)*len, CameraHeight / 2- Math.Sin(angleInRadians) * len);

                Cv2.Circle(frame, CameraWidth / 2, CameraHeight / 2, 25, Scalar.Black, -1);
                Cv2.Line(frame, basePt1.ToPoint(),tipPt1.ToPoint(),Scalar.Blue,10);
                //AngleText.Text = $"Angle: {angle:F1}°";
            }
            else
            {
                //AngleText.Text = "No hand detected";
            }
            CameraImage.Source = BitmapSourceConverter.ToBitmapSource(frame);
        }

        public void Dispose()
        {
            _cap?.Release();
            _detector?.Dispose();
       
        }
    }
}