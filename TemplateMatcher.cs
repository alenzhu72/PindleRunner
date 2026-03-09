using System;

namespace PindleRunner
{
    public sealed class TemplateMatcher
    {
        public bool Find(ImageData source, ImageData template, RectI search, double threshold, out PointI hit)
        {
            hit = default;
            if (template.Width > search.Width || template.Height > search.Height) return false;
            var best = double.NegativeInfinity;
            var bestX = 0;
            var bestY = 0;
            var tMean = Mean(template);
            var tStd = Std(template, tMean);
            if (tStd <= 1e-9) return false;
            for (int y = 0; y <= search.Height - template.Height; y++)
            {
                for (int x = 0; x <= search.Width - template.Width; x++)
                {
                    var sx = search.X + x;
                    var sy = search.Y + y;
                    var score = NccAt(source, template, sx, sy, tMean, tStd);
                    if (score > best)
                    {
                        best = score;
                        bestX = sx;
                        bestY = sy;
                    }
                }
            }
            if (best >= threshold)
            {
                hit = new PointI(bestX, bestY);
                return true;
            }
            return false;
        }

        double NccAt(ImageData src, ImageData tpl, int sx, int sy, double tMean, double tStd)
        {
            double sum = 0;
            double sMean = 0;
            int w = tpl.Width;
            int h = tpl.Height;
            int n = w * h;
            for (int j = 0; j < h; j++)
            {
                var so = (sy + j) * src.Stride + sx * 4;
                var to = j * tpl.Stride;
                for (int i = 0; i < w; i++)
                {
                    var sb = src.Buffer[so + i * 4 + 0];
                    var sg = src.Buffer[so + i * 4 + 1];
                    var sr = src.Buffer[so + i * 4 + 2];
                    var tb = tpl.Buffer[to + i * 4 + 0];
                    var tg = tpl.Buffer[to + i * 4 + 1];
                    var tr = tpl.Buffer[to + i * 4 + 2];
                    var sv = (sr + sg + sb) / 3.0;
                    var tv = (tr + tg + tb) / 3.0;
                    sMean += sv;
                    sum += (sv) * (tv - tMean);
                }
            }
            sMean /= n;
            double sStd = 0;
            for (int j = 0; j < h; j++)
            {
                var so = (sy + j) * src.Stride + sx * 4;
                var to = j * tpl.Stride;
                for (int i = 0; i < w; i++)
                {
                    var sb = src.Buffer[so + i * 4 + 0];
                    var sg = src.Buffer[so + i * 4 + 1];
                    var sr = src.Buffer[so + i * 4 + 2];
                    var sv = (sr + sg + sb) / 3.0;
                    var d = sv - sMean;
                    sStd += d * d;
                }
            }
            sStd = Math.Sqrt(sStd);
            if (sStd <= 1e-9) return -1;
            return sum / (sStd * tStd);
        }

        double Mean(ImageData img)
        {
            double m = 0;
            int n = img.Width * img.Height;
            for (int y = 0; y < img.Height; y++)
            {
                var o = y * img.Stride;
                for (int x = 0; x < img.Width; x++)
                {
                    var b = img.Buffer[o + x * 4 + 0];
                    var g = img.Buffer[o + x * 4 + 1];
                    var r = img.Buffer[o + x * 4 + 2];
                    m += (r + g + b) / 3.0;
                }
            }
            return m / n;
        }

        double Std(ImageData img, double mean)
        {
            double s = 0;
            for (int y = 0; y < img.Height; y++)
            {
                var o = y * img.Stride;
                for (int x = 0; x < img.Width; x++)
                {
                    var b = img.Buffer[o + x * 4 + 0];
                    var g = img.Buffer[o + x * 4 + 1];
                    var r = img.Buffer[o + x * 4 + 2];
                    var v = (r + g + b) / 3.0;
                    var d = v - mean;
                    s += d * d;
                }
            }
            return Math.Sqrt(s);
        }
    }
}
