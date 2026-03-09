namespace PindleRunner
{
    public struct RectI
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public RectI(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; }
    }

    public struct PointI
    {
        public int X;
        public int Y;
        public PointI(int x, int y) { X = x; Y = y; }
    }

    public struct ImageData
    {
        public int Width;
        public int Height;
        public int Stride;
        public byte[] Buffer;
        public ImageData(int width, int height, int stride, byte[] buffer)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Buffer = buffer;
        }
    }
}
