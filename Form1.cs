using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Threading;
using System.Collections.Concurrent;

namespace Mandlebrot
{
    public partial class Form1 : Form
    {
        private int RESOLUTION;

        private double X_START;
        private double X_STOP;

        private double Y_START;
        private double Y_STOP;

        private const double START_R = 51;
        private const double START_G = 0;
        private const double START_B = 102;

        private const int WINDOW_PERCENT = 5;
        private const int OPTIMAL_RESOLUTION = 500;
        private const double START_POS_X = -.5;
        private const double START_POS_Y = 0;
        private const double START_WIDTH = 4;

        private bool[] colors;

        private string text;
        private bool showText;
        private bool showMaxIntensity;

        private double zoom;

        private Bitmap background;
        private Rectangle maxIntensitySquare;

        // Threading stuff
        private byte[,] pixelData;
        private ConcurrentQueue<int> tasks;

        private const int SECTIONS = 2048;

        public Form1()
        {
            InitializeComponent();

            zoom = .5;
            text = "";
            showText = true;
            colors = new bool[3];
        }

        public int divergence(Complex number, int iterations)
        {
            Complex lastTerm = new Complex(0, 0);

            for (int i = 1; i <= iterations; i++)
            {
                lastTerm = Complex.Multiply(lastTerm, lastTerm) + number;

                if (lastTerm.Magnitude > 2)
                    return i;
            }

            return 0;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            RESOLUTION = OPTIMAL_RESOLUTION;
            X_START = START_POS_X - START_WIDTH / 2;
            X_STOP = START_POS_X + START_WIDTH / 2;

            double startHeight = START_WIDTH * this.Height / this.Width;
            Y_START = START_POS_Y - startHeight / 2;
            Y_STOP = START_POS_Y + startHeight / 2;

            updateText();

            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            //sw.Start();

            drawMultithread();

            //sw.Stop();

            //MessageBox.Show(sw.ElapsedMilliseconds.ToString());
        }

        private void drawMultithread()
        {
            canZoom = false;

            Thread[] threads = new Thread[Environment.ProcessorCount];

            pixelData = new byte[this.Width * this.Height, 3];

            tasks = new ConcurrentQueue<int>();

            for (int i = 0; i < SECTIONS; i++)
                tasks.Enqueue(i);

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(drawSection);

                threads[i].Start();
            }

            // Wait for them to finish
            foreach (Thread t in threads)
                t.Join();

            if (background != null)
                background.Dispose();

            background = new Bitmap(this.Width, this.Height);

            LockBitmap lBitmap = new LockBitmap(background);

            lBitmap.LockBits();

            for (int x = 0; x < this.Width; x++)
            {
                for (int y = 0; y < this.Height; y++)
                    lBitmap.SetPixel(x, this.Height - 1 - y, Color.FromArgb((int)pixelData[x * this.Height + y, 0], 
                        (int)pixelData[x * this.Height + y, 1], (int)pixelData[x * this.Height + y, 2]));
            }

            maxIntensitySquare = findMaxBrightness(lBitmap);

            lBitmap.UnlockBits();

            this.Invalidate();

            canZoom = true;
        }

        private void drawSection()
        {
            while (tasks.Count > 0)
            {
                int section = -1;

                if (!tasks.TryDequeue(out section))
                    return;

                for (int x = this.Width * section / SECTIONS; x < this.Width * (section + 1) / SECTIONS; x++)
                {
                    for (int y = 0; y < this.Height; y++)
                    {
                        double xVal = X_START + (X_STOP - X_START) * x / this.Width;
                        double yVal = Y_START + (Y_STOP - Y_START) * y / this.Height;

                        int div = divergence(new Complex(xVal, yVal), RESOLUTION);

                        pixelData[x * this.Height + y, 0] = colors[0] || div == 0 ? (byte)0 : (byte)((255 - START_R) * div / RESOLUTION + START_R);
                        pixelData[x * this.Height + y, 1] = colors[1] || div == 0 ? (byte)0 : (byte)((255 - START_G) * div / RESOLUTION + START_G);
                        pixelData[x * this.Height + y, 2] = colors[2] || div == 0 ? (byte)0 : (byte)((255 - START_B) * div / RESOLUTION + START_B);
                    }
                }
            }
        }

        bool canZoom = true;

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (!canZoom)
                return;

            double xVal = X_START + (X_STOP - X_START) * e.X / this.Width;
            double yVal = Y_START + (Y_STOP - Y_START) * (this.Height - 1 - e.Y) / this.Height;

            double newWidth = (X_STOP - X_START) * (e.Button == MouseButtons.Left ? zoom : 1 / zoom);
            double newHeight = (Y_STOP - Y_START) * (e.Button == MouseButtons.Left ? zoom : 1 / zoom);

            X_START = xVal - newWidth / 2;
            X_STOP = xVal + newWidth / 2;

            Y_START = yVal - newHeight / 2;
            Y_STOP = yVal + newHeight / 2;

            updateText();
            drawMultithread();
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && canZoom)
            {
                canZoom = false;
                drawMultithread();
            }

            else if (e.KeyCode == Keys.D1)
                colors[0] = !colors[0];
            else if (e.KeyCode == Keys.D2)
                colors[1] = !colors[1];
            else if (e.KeyCode == Keys.D3)
                colors[2] = !colors[2];
            else if (e.KeyCode == Keys.Escape)
                Environment.Exit(0);
            else if (e.KeyCode == Keys.T)
                showText = !showText;
            else if (e.KeyCode == Keys.B)
                showMaxIntensity = !showMaxIntensity;
            else if (e.KeyCode == Keys.S)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "Save Image";
                    sfd.Filter = "Png Image|*.bmp";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        background.Save(sfd.FileName, ImageFormat.Jpeg);
                    }
                }
            }

            updateText();
            this.Invalidate();
        }
        
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
                RESOLUTION += 10;
            else if (e.KeyCode == Keys.Down)
                RESOLUTION -= 10;
            else if (e.KeyCode == Keys.Right)
                RESOLUTION += 100;
            else if (e.KeyCode == Keys.Left)
                RESOLUTION -= 100;
            else if (e.KeyCode == Keys.P)
                zoom += .05;
            else if (e.KeyCode == Keys.O)
                zoom -= .05;
            else if (e.KeyCode == Keys.D0)
                zoom += .01;
            else if (e.KeyCode == Keys.D9)
                zoom -= .01;
            else if (e.KeyCode == Keys.L)
                zoom += .001;
            else if (e.KeyCode == Keys.K)
                zoom -= .001;

            updateText();
        }

        private void updateText()
        {
            text = "Position: [(" + X_START + ", " + X_STOP + "), (" + Y_START + ", " + Y_STOP + ")]\nWidth: " + (X_STOP - X_START) + "\nHeight: " + (Y_STOP - Y_START) + "\n[R: " + !colors[0] + " G: " + !colors[1] + " B: " + !colors[2] + "]\nZoom: " + zoom + "\nResolution: " + RESOLUTION;
        
            if (showText)
                this.Invalidate();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            if (background != null)
                e.Graphics.DrawImage(background, new Point(0, 0));

            if (showText)
                e.Graphics.DrawString(text, this.Font, new SolidBrush(Color.White), new Point(10, 10));

            if (showMaxIntensity)
                e.Graphics.DrawRectangle(Pens.White, maxIntensitySquare);
        }

        private Rectangle findMaxBrightness(LockBitmap lBitmap)
        {
            float max = 0;
            Rectangle rectMax = new Rectangle(0, 0, 0, 0);

            int pixel_width = lBitmap.Width * WINDOW_PERCENT / 100;
            int pixel_height = lBitmap.Height * WINDOW_PERCENT / 100;

            for (int i = 0; i < 100 / WINDOW_PERCENT; i++)
            {
                float brightness = 0;

                for (int x = pixel_width * i; x < pixel_width * (i + 1); x++)
                {
                    for (int y = pixel_height * i; y < pixel_height * (i + 1); y++)
                    {
                        Color l = lBitmap.GetPixel(x, y);
                        brightness += (float)Math.Pow(l.GetBrightness(), 4);
                    }
                }

                if (brightness > max)
                {
                    max = brightness;
                    rectMax = new Rectangle(pixel_width * i, pixel_height * i, pixel_width, pixel_height);
                }
            }

            return rectMax;
        }
    }

    public class LockBitmap
    {
        public Bitmap source = null;
        IntPtr Iptr = IntPtr.Zero;
        BitmapData bitmapData = null;

        public byte[] Pixels { get; set; }
        public int Depth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public LockBitmap(Bitmap source)
        {
            this.source = source;
        }

        /// <summary>
        /// Lock bitmap data
        /// </summary>
        public void LockBits()
        {
            try
            {
                // Get width and height of bitmap
                Width = source.Width;
                Height = source.Height;

                // get total locked pixels count
                int PixelCount = Width * Height;

                // Create rectangle to lock
                Rectangle rect = new Rectangle(0, 0, Width, Height);

                // get source bitmap pixel format size
                Depth = System.Drawing.Bitmap.GetPixelFormatSize(source.PixelFormat);

                // Check if bpp (Bits Per Pixel) is 8, 24, or 32
                if (Depth != 8 && Depth != 24 && Depth != 32)
                {
                    throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
                }

                // Lock bitmap and return bitmap data
                bitmapData = source.LockBits(rect, ImageLockMode.ReadWrite,
                                             source.PixelFormat);

                // create byte array to copy pixel values
                int step = Depth / 8;
                Pixels = new byte[PixelCount * step];
                Iptr = bitmapData.Scan0;

                // Copy data from pointer to array
                Marshal.Copy(Iptr, Pixels, 0, Pixels.Length);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Unlock bitmap data
        /// </summary>
        public void UnlockBits()
        {
            try
            {
                // Copy data from byte array to pointer
                Marshal.Copy(Pixels, 0, Iptr, Pixels.Length);

                // Unlock bitmap data
                source.UnlockBits(bitmapData);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Test");
            }
        }

        /// <summary>
        /// Get the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Color GetPixel(int x, int y)
        {
            Color clr = Color.Empty;

            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (i > Pixels.Length - cCount)
                throw new IndexOutOfRangeException();

            if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                byte a = Pixels[i + 3]; // a
                clr = Color.FromArgb(a, r, g, b);
            }
            if (Depth == 24) // For 24 bpp get Red, Green and Blue
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                clr = Color.FromArgb(r, g, b);
            }
            if (Depth == 8)
            // For 8 bpp get color value (Red, Green and Blue values are the same)
            {
                byte c = Pixels[i];
                clr = Color.FromArgb(c, c, c);
            }
            return clr;
        }

        /// <summary>
        /// Set the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        public void SetPixel(int x, int y, Color color)
        {
            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
                Pixels[i + 3] = color.A;
            }
            if (Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
            }
            if (Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                Pixels[i] = color.B;
            }
        }
    }
}
