using System;
using OpenCvSharp;

namespace HandAngleDemo
{
    public static class HandAngleHelper
    {
        /// <summary>
        /// 计算（食指指尖-食指根部）与水平线的夹角，逆时针为正
        /// </summary>
        public static double GetIndexFingerAngle(Point2f basePt, Point2f tipPt)
        {
            double dx = tipPt.X - basePt.X;
            double dy = tipPt.Y - basePt.Y;
            double radians = Math.Atan2(-dy, dx); // 图像y轴向下
            double angle = radians * 180.0 / Math.PI;
            return angle;
        }
    }
}