namespace Microsoft.Xna.Framework
{
    public struct Rectangle { public Rectangle(int x, int y, int w, int h) { } }
    public struct Color
    {
        public static Color FromNonPremultiplied(int r, int g, int b, int a) => new();
    }
    namespace Graphics
    {
        public class Texture2D : System.IDisposable
        {
            public int Width => 0;
            public int Height => 0;
            public Texture2D(GraphicsDevice d, int w, int h) { }
            public void GetData<T>(int level, Rectangle? rect, T[] data, int start, int count) where T : struct { }
            public void SetData<T>(T[] data) where T : struct { }
            public void SaveAsPng(System.IO.Stream s, int w, int h) { }
            public void Dispose() { }
        }
        public class GraphicsDevice { }
    }
}
