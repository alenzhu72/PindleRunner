using System;

namespace PindleRunner
{
    public sealed class TemplateMatcher
    {
        public bool Find(ImageData source, ImageData template, RectI search, double threshold, out PointI hit)
        {
            return Find(source, template, search, threshold, out hit, out _);
        }

        public bool Find(ImageData source, ImageData template, RectI search, double threshold, out PointI hit, out double bestScore)
        {
            hit = default;
            bestScore = double.NegativeInfinity;
            if (template.Width > search.Width || template.Height > search.Height) return false;
            var tMean = Mean(template);
            var tStd = Std(template, tMean);
            if (tStd <= 1e-9) return false;

            int maxX = search.Width - template.Width;
            int maxY = search.Height - template.Height;
            long positions = (long)(maxX + 1) * (maxY + 1);
            int step = positions > 2_000_000 ? 8 : (positions > 1_000_000 ? 4 : (positions > 500_000 ? 2 : 1));

            const int keep = 16;
            var candScore = new double[keep];
            var candX = new int[keep];
            var candY = new int[keep];
            for (int i = 0; i < keep; i++) candScore[i] = double.NegativeInfinity;

            void PushCandidate(double score, int x, int y)
            {
                if (score <= candScore[keep - 1]) return;
                int k = keep - 1;
                while (k > 0 && score > candScore[k - 1])
                {
                    candScore[k] = candScore[k - 1];
                    candX[k] = candX[k - 1];
                    candY[k] = candY[k - 1];
                    k--;
                }
                candScore[k] = score;
                candX[k] = x;
                candY[k] = y;
            }

            for (int y = 0; y <= maxY; y += step)
            {
                for (int x = 0; x <= maxX; x += step)
                {
                    var sx = search.X + x;
                    var sy = search.Y + y;
                    var score = NccAt(source, template, sx, sy, tMean, tStd);
                    PushCandidate(score, sx, sy);
                }
            }

            int bestX = candX[0];
            int bestY = candY[0];
            double best = candScore[0];

            if (step > 1)
            {
                for (int i = 0; i < keep; i++)
                {
                    if (double.IsNegativeInfinity(candScore[i])) break;
                    int cx = candX[i];
                    int cy = candY[i];
                    int rx0 = Math.Max(search.X, cx - step);
                    int ry0 = Math.Max(search.Y, cy - step);
                    int rx1 = Math.Min(search.X + maxX, cx + step);
                    int ry1 = Math.Min(search.Y + maxY, cy + step);
                    for (int y = ry0; y <= ry1; y++)
                    {
                        for (int x = rx0; x <= rx1; x++)
                        {
                            var score = NccAt(source, template, x, y, tMean, tStd);
                            if (score > best)
                            {
                                best = score;
                                bestX = x;
                                bestY = y;
                            }
                        }
                    }
                }
            }

            bestScore = best;
            if (best >= threshold)
            {
                hit = new PointI(bestX, bestY);
                return true;
            }
            return false;
        }

        double NccAt(ImageData src, ImageData tpl, int sx, int sy, double tMean, double tStd)
        {
            double sMean = 0;
            int w = tpl.Width;
            int h = tpl.Height;
            int n = w * h;
            for (int j = 0; j < h; j++)
            {
                var so = (sy + j) * src.Stride + sx * 4;
                for (int i = 0; i < w; i++)
                {
                    var sb = src.Buffer[so + i * 4 + 0];
                    var sg = src.Buffer[so + i * 4 + 1];
                    var sr = src.Buffer[so + i * 4 + 2];
                    var sv = (sr + sg + sb) / 3.0;
                    sMean += sv;
                }
            }
            sMean /= n;
            double sStd = 0;
            double sum = 0;
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
                    var tb = tpl.Buffer[to + i * 4 + 0];
                    var tg = tpl.Buffer[to + i * 4 + 1];
                    var tr = tpl.Buffer[to + i * 4 + 2];
                    var tv = (tr + tg + tb) / 3.0;
                    var sd = sv - sMean;
                    var td = tv - tMean;
                    sum += sd * td;
                    sStd += sd * sd;
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
