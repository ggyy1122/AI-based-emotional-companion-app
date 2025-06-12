using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace HandAngleDemo
{
    public class HandLandmarkDetector : IDisposable
    {
        private InferenceSession _session;
        private readonly int _inputWidth = 224;
        private readonly int _inputHeight = 224;

        public HandLandmarkDetector(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        /// <summary>
        /// 检测手部关键点（返回21个点的x,y,置信度）
        /// </summary>
        /// <param name="frame">BGR格式帧</param>
        /// <returns>float[21,3]，无手时返回null</returns>
        public float[,] DetectLandmarks(Mat frame)
        {
            Mat resized = frame.Resize(new OpenCvSharp.Size(_inputWidth, _inputHeight));
            Mat rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            var input = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            for (int y = 0; y < _inputHeight; y++)
                for (int x = 0; x < _inputWidth; x++)
                {
                    var color = rgb.At<Vec3b>(y, x);
                    //input[0, 0, y, x] = color.Item2 / 255.0f; // R
                    //input[0, 1, y, x] = color.Item1 / 255.0f; // G
                    //input[0, 2, y, x] = color.Item0 / 255.0f; // B
                    input[0, 0, y, x] = color.Item0 / 255.0f; // R
                    input[0, 1, y, x] = color.Item1 / 255.0f; // G
                    input[0, 2, y, x] = color.Item2 / 255.0f; // B

                }

            var inputMeta = _session.InputMetadata;
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMeta.Keys.First(), input)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();

            // 手部关键点输出为 [1, 63] (21个点，每点x,y,z)
            if (output.Length < 63) return null;

            //Console.WriteLine(output.Length);

            float[,] landmarks = new float[21, 3];
            for (int i = 0; i < 21; i++)
            {
                landmarks[i, 0] = output[i * 3];       // x (归一化)
                landmarks[i, 1] = output[i * 3 + 1];   // y (归一化)
                landmarks[i, 2] = output[i * 3 + 2];   // z
            }

            return landmarks;
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}