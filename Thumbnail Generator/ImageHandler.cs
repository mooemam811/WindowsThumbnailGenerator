﻿using ImageMagick;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Thumbnail_Generator
{
    class ImageHandler
    {
        private static readonly int contentWidth = 224;
        private static readonly int contentHeight = 75;

        public void generateThumbnail(string[] fileArray, string filePath)
        {
            using MagickImage bgImage = new MagickImage(bitmapToArray(Properties.Resources.Background));
            using MagickImage contents = new MagickImage(compositeThumbnail(fileArray));
            using MagickImage fgImage = new MagickImage(bitmapToArray(Properties.Resources.Foreground));

            contents.Crop(contentWidth, contentHeight);

            bgImage.Composite(contents, Gravity.Center, 1, -20, CompositeOperator.Over);
            bgImage.Composite(fgImage, Gravity.Center, CompositeOperator.Over);
            exportImage(bgImage.ToByteArray(), filePath);
        }

        private byte[] compositeThumbnail(string[] fileArray)
        {
            using MagickImageCollection magickCollection = new();

            bool isFirst = true;
            foreach (string filePath in fileArray)
            {
                using Bitmap thumb = WindowsThumbnailProvider.GetThumbnail(
                    filePath, 1024, 1024, ThumbnailOptions.None);
                using MagickImage image = new(bitmapToArray(thumb));

                int calcHeight = image.Height * (contentWidth / image.Height);
                image.Scale(contentWidth, calcHeight);
                image.Extent(0, image.Height / 8, contentWidth, contentHeight / fileArray.Length);
                image.RePage();

                MagickImage composite = new(applyGradient(image.ToByteArray()));

                if (isFirst)
                {
                    MagickImage roundedImage = new(roundEdges(composite.ToByteArray()));
                    magickCollection.Add(roundedImage);
                    isFirst = false;
                }
                else
                {
                    magickCollection.Add(composite);
                }

                thumb.Dispose();
            }

            byte[] result_bytes = magickCollection.AppendVertically().ToByteArray();

            return result_bytes;
        }

        private byte[] roundEdges(byte[] srcBytes, int radius = 10)
        {
            using MagickImage srcImage = new MagickImage(srcBytes);

            MagickImage mask = new MagickImage(MagickColors.White, srcImage.Width, srcImage.Height);
            _ = new Drawables()
                .FillColor(MagickColors.Black)
                .StrokeColor(MagickColors.Black)
                .Polygon(new PointD(0, 0), new PointD(0, radius), new PointD(radius, 0))
                .Polygon(new PointD(mask.Width, 0), new PointD(mask.Width, radius), new PointD(mask.Width - radius, 0))
                //.Polygon(new PointD(0, mask.Height), new PointD(0, mask.Height - radius), new PointD(radius, mask.Height))
                //.Polygon(new PointD(mask.Width, mask.Height), new PointD(mask.Width, mask.Height - radius), new PointD(mask.Width - radius, mask.Height))
                .FillColor(MagickColors.White)
                .StrokeColor(MagickColors.White)
                .Circle(radius, radius, radius, 0)
                .Circle(mask.Width - radius, radius, mask.Width - radius, 0)
                //.Circle(radius, mask.Height - radius, 0, mask.Height - radius)
                //.Circle(mask.Width - radius, mask.Height - radius, mask.Width - radius, mask.Height)
                .Draw(mask);

            // Copy Alpha
            using (IMagickImage<ushort> imageAlpha = srcImage.Clone())
            {
                imageAlpha.Alpha(AlphaOption.Extract);
                imageAlpha.Opaque(MagickColors.White, MagickColors.None);
                mask.Composite(imageAlpha, CompositeOperator.Over);
            }

            mask.HasAlpha = false;
            srcImage.HasAlpha = false;
            srcImage.Composite(mask, CompositeOperator.CopyAlpha);

            return srcImage.ToByteArray();
        }

        private byte[] applyGradient(byte[] srcBytes)
        {
            using MagickImage srcImage = new MagickImage(srcBytes);

            MagickImage gradient = new("gradient:none-black", srcImage.Width, srcImage.Height);
            gradient.Alpha(AlphaOption.Set);
            gradient.Evaluate(Channels.Alpha, EvaluateOperator.Divide, 2);

            srcImage.Composite(gradient, CompositeOperator.Over);

            return srcImage.ToByteArray();
        }

        private byte[] bitmapToArray(Bitmap src)
        {
            using MemoryStream stream = new();
            src.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

            return stream.ToArray();
        }

        private void exportImage(byte[] srcImage, string filePath)
        {
            using MagickImage outImage = new(srcImage);
            try
            {
                outImage.Write(filePath);
            }
            catch (MagickBlobErrorException e)
            {
                return;
                //MessageBox.Show("Error writing thumb.ico! Please check if the file is being used by another application!", "Write Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
