﻿using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace SenseHat
{
    /// <summary>
    /// LED Matrix Object for Raspberry Pi SenseHat
    /// </summary>
    public class SenseLED
    {
        // Allocate a new frame buffer for LED matrix 
        [DllImport("libsense.so")]
        private static extern IntPtr sense_alloc_fb();

        // Allocate a new buffer for LED matrix
        [DllImport("libsense.so")]
        private static extern IntPtr sense_alloc_bitmap();

        // Free a frame buffer for LED matrix
        [DllImport("libsense.so")]
        private static extern void sense_free_bitmap(IntPtr bitmap);

        // Generate RGB565(16-bit) value from components r, g, and b (8-bits each)
        [DllImport("libsense.so")]
        private static extern ushort sense_make_color_rgb(byte r, byte g, byte b);

        // Generate RGB565(16-bit) value from components in compressed state r(5-bit), g(6-bit), and b(5-bit)
        [DllImport("libsense.so")]
        private static extern ushort sense_make_color_rgb565(byte r, byte g, byte b);

        // Set buffer pixel to a specified RGB565 value
        [DllImport("libsense.so")]
        private static extern int sense_bitmap_set_pixel(IntPtr bitmap, byte x, byte y, ushort color);

        // Paint buffer to a specified RGB565 value
        [DllImport("libsense.so")]
        private static extern void sense_bitmap_paint(IntPtr bitmap, ushort color);

        // Gets raw buffer array from buffer
        [DllImport("libsense.so")]
        private static extern IntPtr sense_bitmap_get_buffer(IntPtr bitmap);

        // Copy contents between two buffers
        [DllImport("libsense.so")]
        private static extern void sense_bitmap_cpy(IntPtr dst, IntPtr src);

        //             front buffer         back buffer
        private IntPtr fbptr = IntPtr.Zero, fb2ptr = IntPtr.Zero;

        private byte rot; // Rotation of LED matrix value
        private static bool instance = false; // Block multiple instances of object

        // Constants for LED buffer rotation
        public static class Rotation
        {
            public const byte deg_0     = 0; // Degrees
            public const byte deg_90    = 1;
            public const byte deg_180   = 2;
            public const byte deg_270   = 3;
            public const byte grad_0    = 0; // Gradians
            public const byte grad_100  = 1;
            public const byte grad_200  = 2;
            public const byte grad_300  = 3;
            public const byte rad_0     = 0; // Radians
            public const byte rad_0_5pi = 1;
            public const byte rad_pi    = 2;
            public const byte rad_1_5pi = 3;
        }

        public SenseLED()
        {
            if (instance) // Block multiple instances
                throw new InvalidOperationException ("Multiple instances of SenseLED not permitted");

            instance = true;
            fbptr = sense_alloc_fb(); // Allocate front buffer (what's displayed)
            fb2ptr = sense_alloc_bitmap(); // Allocate back buffer (what you manipulate)

            // Ensure buffers were allocated successfully
            if (fbptr == IntPtr.Zero || fb2ptr == IntPtr.Zero)
                throw new InvalidOperationException ("Failed to initialize SenseLED (libsense alloc fb/bitmap failed)");

            rot = Rotation.deg_0;

            // Clear all displays
            sense_bitmap_paint(fbptr, 0);
            sense_bitmap_paint(fb2ptr, 0);
        }

        // Destroy everything mercilessly
        ~SenseLED()
        {
            sense_free_bitmap(fbptr);
            sense_free_bitmap(fb2ptr);
            instance = false;
        }

        /// <summary>
        /// Copy the back buffer into the front buffer (with rotations)
        /// </summary>
        public void Show()
        {
            // No rotation, use fast copy
            if (rot == Rotation.deg_0)
            {
                sense_bitmap_cpy(fbptr, fb2ptr);
                return;
            }

            // Get memory addresses of internal buffers
            IntPtr backPtr = sense_bitmap_get_buffer(fb2ptr), frontPtr = sense_bitmap_get_buffer(fbptr);

            unsafe
            {
                // typecast memory addresses to arrays
                ushort* back = (ushort*)backPtr, front = (ushort*)frontPtr;

                // Apply rotation, optimization using bitshift instead of multiplication
                switch (rot)
                {
                    case Rotation.deg_90:
                        for (byte y = 0; y < 8; ++y)
                        {
                            for (byte x = 0; x < 8; ++x)
                            {
                                front[(x << 3) | (7 - y)] = back[(y << 3) | x];
                            }
                        }
                        return;
                    case Rotation.deg_180:
                        for (byte y = 0; y < 8; ++y)
                        {
                            for (byte x = 0; x < 8; ++x)
                            {
                                front[((7 - y) << 3) | (7 - x)] = back[(y << 3) | x];
                            }
                        }
                        return;
                    case Rotation.deg_270:
                        for (byte y = 0; y < 8; ++y)
                        {
                            for (byte x = 0; x < 8; ++x)
                            {
                                front[((7 - x) << 3) | y] = back[(y << 3) | x];
                            }
                        }
                        return;
                }
            }
        }

        /// <summary>
        /// Gets byte[] containing Values of pixel at given x,y. R,G,B are all 8-bits.
        /// </summary>
        /// <returns>RGB888 pixel array</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="buffer">If set to <c>true</c> use the back buffer, otherwise use front buffer.</param>
        public byte[] GetPixel(int x, int y, bool buffer = true)
        {
            // Get pointer to desired buffer
            IntPtr bufPtr;
            if (!buffer)
                bufPtr = sense_bitmap_get_buffer(fbptr);
            else
                bufPtr = sense_bitmap_get_buffer(fb2ptr);

            // Allocate appropriate varibles
            byte[] px = new byte[3];
            ushort dt;

            unsafe
            {
                dt = ((ushort*)bufPtr)[(y << 3) | x]; // Get the RGB565 raw pixel data (using bitshifts to locate pixel)
            }

            // Extract R, G, B and convert/zero pad to 888
            px[0] = (byte)((dt >> 8) & 248);
            px[1] = (byte)((dt >> 3) & 252);
            px[2] = (byte)(((uint)dt << 3) & 248);

            return px;
        }

        /// <summary>
        /// Gets byte[] containing Values of pixel at given x,y. R,G,B are in 565 format.
        /// </summary>
        /// <returns>RGB565 pixel array<c/returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="buffer">If set to <c>true</c> use the back buffer, otherwise use front buffer.</param>
        public byte[] GetPixelRaw(int x, int y, bool buffer = true)
        {
            // Get pointer to desired buffer
            IntPtr bufPtr;
            if (!buffer)
                bufPtr = sense_bitmap_get_buffer(fbptr);
            else
                bufPtr = sense_bitmap_get_buffer(fb2ptr);

            // Allocate appropriate variables
            byte[] px = new byte[3];
            ushort dt;

            unsafe
            {
                dt = ((ushort*)bufPtr)[(y << 3) | x]; // Get the RGB565 raw pixel data (using bitshifts to locate pixel)
            }

            // Extract R, G, B
            px[0] = (byte)(dt >> 11);
            px[1] = (byte)((dt >> 5) & 63);
            px[2] = (byte)(dt & 31);

            return px;
        }

        /// <summary>
        /// Acquires all pixels of the selected buffer as an array of RGB565 values
        /// </summary>
        /// <returns>The pixels raw.</returns>
        /// <param name="buffer">If set to <c>true</c> use the back buffer, otherwise use front buffer.</param>
        public ushort[] GetPixelsRaw(bool buffer = true)
        {
            // Get pointer to desired buffer
            IntPtr bufPtr;
            if (!buffer)
                bufPtr = sense_bitmap_get_buffer(fbptr);
            else
                bufPtr = sense_bitmap_get_buffer(fb2ptr);

            // Allocate appropriate variables
            ushort[] pxs = new ushort[64];

            unsafe
            {
                // Get the RGB 565 pixel data for each pixel
                ushort* buf = (ushort*)bufPtr;
                for (ushort i = 0; i < 64; ++i)
                    pxs[i] = buf[i];
            }

            return pxs;
        }

        /// <summary>
        /// Set the back buffer to the RGB565 values contained in an array of ushort (length 64)
        /// </summary>
        /// <param name="pxs">Pixel array of length 64</param>
        public void SetPixelsRaw(ushort[] pxs)
        {
            // Get pointer to back buffer
            IntPtr bufPtr = sense_bitmap_get_buffer(fb2ptr);

            unsafe
            {
                ushort* buf = (ushort*)bufPtr;
                for (ushort i = 0; i < 64; ++i) // Set all pixels
                    buf[i] = pxs[i];
            }
        }

        /// <summary>
        /// Sets the pixel at given x,y coordinates
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="c">Desired colour</param>
        public void SetPixel(int x, int y, Color c)
        {
            sense_bitmap_set_pixel(fb2ptr, (byte)x, (byte)y, sense_make_color_rgb(c.R, c.G, c.B));
        }

        /// <summary>
        /// Sets the pixel at given x,y coordinates
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public void SetPixel(int x, int y, int r, int g, int b)
        {
            sense_bitmap_set_pixel(fb2ptr, (byte)x, (byte)y, sense_make_color_rgb((byte)r, (byte)g, (byte)b));
        }

        /// <summary>
        /// Sets the pixel at given x,y coordinates
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="c">Array containing RGB components</param>
        public void SetPixel(int x, int y, byte[] c) {
            sense_bitmap_set_pixel(fb2ptr, (byte)x, (byte)y, sense_make_color_rgb(c[0], c[1], c[2]));
        }

        /// <summary>
        /// Sets the pixel at given x,y coordinates using RGB565 values.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public void SetPixelRaw(int x, int y, int r, int g, int b)
        {
            sense_bitmap_set_pixel(fb2ptr, (byte)x, (byte)y, sense_make_color_rgb565((byte)r, (byte)g, (byte)b));
        }

        /// <summary>
        /// Sets the pixel at given x,y coordinates using byte array of components
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="c">Array containing RGB565 components</param>
        public void SetPixelRaw(int x, int y, byte[] c)
        {
            sense_bitmap_set_pixel (fb2ptr, (byte)x, (byte)y, sense_make_color_rgb565 (c [0], c [1], c [2]));
        }

        /// <summary>
        /// Creates colour using sint32 R,G,B components (8-bits each)
        /// </summary>
        /// <returns>A ushort representing the equiv. RGB565 value</returns>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public static ushort makeColor(int r, int g, int b)
        {
            return sense_make_color_rgb((byte)r, (byte)g, (byte)b);
        }

        /// <summary>
        /// Creates colour using sint32 R,G,B components in RGB565 format
        /// </summary>
        /// <returns>A ushort representing the equiv. RGB565 value</returns>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public static ushort makeColorRaw(int r, int g, int b)
        {
            return sense_make_color_rgb565((byte)r, (byte)g, (byte)b);
        }

        /// <summary>
        /// Sets the rotation of front buffer
        /// </summary>
        /// <param name="r">Rotation value (see SenseLed.Rotation class)</param>
        /// <param name="redraw">If set to <c>true</c> redraw after set.</param>
        public void SetRotation(byte r, bool redraw)
        {
            if (r <= 3 && r >= 0) // Check if valid
            {
                rot = r;
                if (redraw)
                    this.Show();
            }
        }

        /// <summary>
        /// Draws a bitmap onto back buffer
        /// </summary>
        /// <param name="bmp">Desired bitmap to display. Must be 8x8</param>
        public void DrawBitmap(Bitmap bmp) {
            if (bmp.Height != 8 || bmp.Width != 8) // Check if desired dimensions are satsified
                throw new InvalidOperationException ("Only 8x8 bitmaps are supported");

            Rectangle rect = new Rectangle(0, 0, 8, 8);
            BitmapData dt = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); // Get internal data array of bitmap obj
            IntPtr bufPtr = sense_bitmap_get_buffer(fb2ptr); // Get internal data array of back buffer

            unsafe {
                uint* arr = (uint*)dt.Scan0; // Cast to array of 32 bit ARGB
                ushort* buf = (ushort*)bufPtr; // Cast back buffer address to array

                // Extract R, G, B from ARGB, convert to RGB565, and copy to back buffer
                for (int i = 0; i < 64; ++i) {
                    uint px = arr [i];

                    byte b = (byte)(px & 255);
                    byte g = (byte)((px >> 8) & 255);
                    byte r = (byte)((px >> 16) & 255);

                    ushort c = sense_make_color_rgb (r, g, b);
                    buf [i] = c;
                }
            }

            bmp.UnlockBits (dt); // Release bitmap raw bit lock
        }

        /// <summary>
        /// Clear the contents of the back buffer. Redraws by default.
        /// </summary>
        /// <param name="redraw">If set to <c>true</c> redraw, otherwise don't.</param>
        public void Clear(bool redraw = true)
        {
            Fill(0);

            if (redraw)
                Show ();
        }

        /// <summary>
        /// Fill back buffer with the specified colour.
        /// </summary>
        /// <param name="c">Desired colour</param>
        public void Fill(Color c)
        {
            sense_bitmap_paint(fb2ptr, sense_make_color_rgb(c.R, c.G, c.B));
        }

        /// <summary>
        /// Fill back buffer with the specified colour
        /// </summary>
        /// <param name="c">Desired colour in RGB565 format</param>
        public void Fill(ushort c)
        {
            sense_bitmap_paint(fb2ptr, c);
        }

        /// <summary>
        /// Fill the back buffer with the specified r, g and b values.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public void Fill(byte r, byte g, byte b)
        {
            sense_bitmap_paint(fb2ptr, sense_make_color_rgb(r, g, b));
        }

        /// <summary>
        /// Fill the back buffer with the specified color values.
        /// </summary>
        /// <param name="c">Array representing R, G, B 8-bit color components.</param>
        public void Fill(byte[] c)
        {
            sense_bitmap_paint(fb2ptr, sense_make_color_rgb(c[0], c[1], c[2]));
        }
    }
}