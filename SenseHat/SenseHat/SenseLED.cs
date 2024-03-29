﻿using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading;

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
        
        // Libc instance of memcpy
        [DllImport("libc.so.6")]
        private static extern IntPtr memcpy(IntPtr dst, IntPtr src, uint n);
        
        // Gamma functions. Requires patched libsense (libsense_fb_gamma.patch)
        
        // Set sense hat gamma (sbyte[] needs 32 values, value range is 0 - 31)
        [DllImport("libsense.so")]
        private static extern sbyte sense_fb_set_gamma(IntPtr fb, sbyte[] gamma);
        
        // Get gamma settings from kernel driver
        [DllImport("libsense.so")]
        private static extern sbyte sense_fb_get_gamma(IntPtr fb, [Out] sbyte[] gamma);
        
        // Reset gamma settings to default
        [DllImport("libsense.so")]
        private static extern sbyte sense_fb_reset_gamma(IntPtr fb);
        
        // Apply low light mode gamma settings
        [DllImport("libsense.so")]
        private static extern sbyte sense_fb_set_lowlight(IntPtr fb, sbyte val);
        
        // Check if low light mode is enabled
        [DllImport("libsense.so")]
        private static extern sbyte sense_fb_get_lowlight(IntPtr fb);
        

        //             front buffer         back buffer
        private IntPtr fbptr = IntPtr.Zero, fb2ptr = IntPtr.Zero;

        private byte rot; // Rotation of LED matrix value
        private static bool instance = false; // Block multiple instances of object

        // Constants for LED buffer rotation/flip
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
            
            public const byte flip_h    = 4;
            public const byte flip_v    = 5;
        }

        public SenseLED()
        {
            Init();
        }

        // Destroy everything mercilessly
        ~SenseLED()
        {
            Free();
        }
        
        /// <summary>
        /// Initializes SenseLED internals
        /// </summary>
        public void Init() {
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
            
            // Reset all gamma settings
            //ResetGamma();
        }
        
        /// <summary>
        /// Destroys this instance of SenseLED
        /// </summary>
        public void Free() {
            if (!instance)
                return;
            
            sense_free_bitmap(fbptr);
            sense_free_bitmap(fb2ptr);
            instance = false;
        }
        
        /// <summary>
        /// Convert RGB565 to RGB565 byte[]
        /// </summary>
        /// <returns>Byte array of R, G, B (in RGB565)</returns>
        /// <param name="raw">The "raw" RGB565 bits</param>
        public static byte[] rgb565_to_565(ushort raw) {
            byte[] px = new byte[3];
            px[2] = (byte)(raw & 31);
            px[1] = (byte)((raw >> 5) & 63);
            px[0] = (byte)(raw >> 11);
            
            return px;
        }
        
        /// <summary>
        /// Convert RGB565 to RGB888 byte[]
        /// </summary>
        /// <returns>Byte array of R, G, B (in RGB888)</returns>
        /// <param name="raw">The "raw" RGB565 bits</param>
        public static byte[] rgb565_to_888(ushort raw) {
            byte[] px = rgb565_to_565(raw); 
            px[0] = (byte)(px[0] * 255 / 31);
            px[1] = (byte)(px[1] * 255 / 63);
            px[2] = (byte)(px[2] * 255 / 31);
            
            return px;
        }
        
        /// <summary>
        /// Check if character is printable
        /// </summary>
        /// <returns><c>true</c> if char is printable, otherwise <c>false</c></returns>
        /// <param name="c">The character</param>
        public static bool charIsPrintable(char c) {
            return ' ' <= c && c <= '~';
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
                                front[(x << 3) | (7 - y)] = back[(y << 3) | x];
                        }
                        return;
                    case Rotation.deg_180:
                        for (byte y = 0; y < 8; ++y)
                        {
                            for (byte x = 0; x < 8; ++x)
                                front[((7 - y) << 3) | (7 - x)] = back[(y << 3) | x];
                        }
                        return;
                    case Rotation.deg_270:
                        for (byte y = 0; y < 8; ++y)
                        {
                            for (byte x = 0; x < 8; ++x)
                                front[((7 - x) << 3) | y] = back[(y << 3) | x];
                        }
                        return;
                        
                    case Rotation.flip_h:
                        for (byte y = 0; y < 8; ++y) {
                            for (byte x = 0; x < 8; ++x)
                                front[(y << 3) | (7 - x)] = back[(y << 3) | x];
                        }
                        return;
                    case Rotation.flip_v:
                        for (byte y = 0; y < 8; ++y) {
                            for (byte x = 0; x < 8; ++x)
                                front[((7 - y) << 3) | x] = back[(y << 3) | x];
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
            IntPtr bufPtr = sense_bitmap_get_buffer(buffer ? fb2ptr : fbptr);

            // Allocate appropriate varibles
            ushort dt;

            unsafe
            {
                dt = ((ushort*)bufPtr)[(y << 3) | x]; // Get the RGB565 raw pixel data (using bitshifts to locate pixel)
            }

            return rgb565_to_888(dt);
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
            IntPtr bufPtr = sense_bitmap_get_buffer(buffer ? fb2ptr : fbptr);

            // Allocate appropriate variables
            ushort dt;

            unsafe
            {
                dt = ((ushort*)bufPtr)[(y << 3) | x]; // Get the RGB565 raw pixel data (using bitshifts to locate pixel)
            }

            // Extract R, G, B
            return rgb565_to_565(dt);
        }

        /// <summary>
        /// Acquires all pixels of the selected buffer as an array of RGB565 values
        /// </summary>
        /// <returns>The pixels raw.</returns>
        /// <param name="buffer">If set to <c>true</c> use the back buffer, otherwise use front buffer.</param>
        public ushort[] GetPixelsRaw(bool buffer = true)
        {
            // Get pointer to desired buffer
            IntPtr bufPtr = sense_bitmap_get_buffer(buffer ? fb2ptr : fbptr);

            // Allocate appropriate variables
            ushort[] pxs = new ushort[64];

            unsafe {
                fixed (ushort* pxPtr = pxs)
                    memcpy ((IntPtr)pxPtr, bufPtr, 128); // raw memory copy, 128 = 64 * 2 (ushort = 2 bytes)
            }

            return pxs;
        }

        /// <summary>
        /// Set the back buffer to the RGB565 values contained in an array of ushort (length 64)
        /// </summary>
        /// <param name="pxs">Pixel array of length 64</param>
        public void SetPixelsRaw(ushort[] pxs)
        {
            if (pxs.Length != 64)
                throw new InvalidOperationException("Pixel array must have length of 64");

            // Get pointer to back buffer
            IntPtr bufPtr = sense_bitmap_get_buffer(fb2ptr);

            unsafe
            {
                fixed (ushort* pxPtr = pxs)
                    memcpy (bufPtr, (IntPtr)pxPtr, 128); // raw memory copy, 128 = 64 * 2 (ushort = 2 bytes)
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
        /// Draws a bitmap onto back buffer (and scale to 8x8)
        /// </summary>
        /// <param name="bmp">Desired bitmap to display</param>
        /// <param name="interpMode">Interpolation mode for scaling, default is high quality bicubic</param>
        public void DrawBitmapScaled(Bitmap bmp, InterpolationMode interpMode = InterpolationMode.HighQualityBicubic) {
            if (bmp.Height == 8 && bmp.Width == 8) { // If size is correct, skip rescale code
                DrawBitmap(bmp);
                return;
            }
            
            Bitmap bmp2 = new Bitmap(8, 8); // Allocate scaled bitmap buffer
            Graphics g = Graphics.FromImage(bmp2); // Create graphics object on buffer
            
            g.InterpolationMode = interpMode; // Set scaling interpolation mode
            g.DrawImage(bmp, 0, 0, 8, 8); // Draw image, scaling to 8x8
            g.Dispose(); // Cleanup graphics obj
            
            DrawBitmap(bmp2); // Draw the scaled buffer
        }
        
        /// <summary>
        /// Converts contents of front/back buffer into a bitmap
        /// </summary>
        /// <returns>The resulting Bitmap object.</returns>
        /// <param name="buffer">If set to <c>true</c> use back buffer, otherwise use front buffer.</param>
        public Bitmap ToBitmap(bool buffer = true) {
            Bitmap bmp = new Bitmap(8, 8);
            
            Rectangle rect = new Rectangle(0, 0, 8, 8);
            BitmapData dt = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb); // Get internal data array of bitmap obj
            IntPtr bufPtr = sense_bitmap_get_buffer(buffer ? fb2ptr : fbptr); // Select front/back buffer

            unsafe {
                uint* arr = (uint*)dt.Scan0; // Cast to array of 32 bit ARGB
                ushort* buf = (ushort*)bufPtr; // Cast back buffer address to array

                // Extract RGB565 from buffer, convert to 32-bit ARGB, write to bitmap object
                for (int i = 0; i < 64; ++i) {
                    byte[] px = rgb565_to_888(buf[i]);
                    
                    uint bpx = (uint)(px[2] | (px[1] << 8) | (px[0] << 16) | 0xff000000);
                    arr[i] = bpx;
                }
            }

            bmp.UnlockBits (dt); // Release bitmap raw bit lock
            return bmp;
        }
        
        /// <summary>
        /// Saves front/back buffer to a PNG file
        /// </summary>
        /// <param name="path">Path to save the image to</param>
        /// <param name="buffer">If set to <c>true</c> use back buffer, otherwise use front buffer.</param>
        public void SaveBitmap(string path, bool buffer = true) {
            ToBitmap(buffer).Save(path, ImageFormat.Png);
        }
        
        /// <summary>
        /// Draws an image file onto the back buffer
        /// </summary>
        /// <param name="path">Path to desired image file</param>
        /// <param name="interpMode">Interpolation mode for scaling, default is high quality bicubic</param>
        public void DrawImage(string path, InterpolationMode interpMode = InterpolationMode.HighQualityBicubic) {
            DrawBitmapScaled(new Bitmap(path), interpMode); // Create bitmap obj from file, then call scaled draw
        }
        
        /// <summary>
        /// Draws a letter onto the back buffer
        /// </summary>
        /// <param name="ltr">Character to draw on screen</param>
        /// <param name="color">Letter color in RGB565</param>
        /// <param name="font">Font to draw letter with</param>
        public void ShowLetter(char ltr, ushort color = 0x7bef, ushort font = SenseLEDFont.defaultFont) {
            if (!charIsPrintable(ltr)) { // If character is non-printable, clear screen and return
                sense_bitmap_paint(fb2ptr, 0);
                return;
            }
        
            ulong[] fontData = SenseLEDFont.getFontById(font); // Retrieve font data
            // Calculate index in array for letter (+2 is for skipping over dimensions)
            byte w = (byte)fontData[0], h = (byte)fontData[1], bitLen = (byte)(w * h);
            byte idx = (byte)(ltr - SenseLEDFont.startChar + 2);
            
            ulong bits = fontData[idx];
            
            IntPtr bufPtr = sense_bitmap_get_buffer(fb2ptr); // Get internal data array of back buffer
            
            unsafe {
                ushort* buf = (ushort*)bufPtr; // Cast back buffer address to array
                
                for (byte y = 0; y < h; ++y) {
                    for (byte x = 0; x < w; ++x) {
                        byte bit = (byte)((bits >> (bitLen - y * w - x - 1)) & 1);
                        buf[(y << 3) | x] = (bit == 1) ? color : (ushort)0;
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws a letter onto the back buffer
        /// </summary>
        /// <param name="ltr">Character to draw on screen</param>
        /// <param name="color">Letter color</param>
        /// <param name="font">Font to draw letter with</param>
        public void ShowLetter(char ltr, Color color, ushort font = SenseLEDFont.defaultFont) {
            ShowLetter(ltr, sense_make_color_rgb(color.R, color.G, color.B), font);
        }
        
        /// <summary>
        /// Draws a letter onto the back buffer
        /// </summary>
        /// <param name="ltr">Character to draw on screen</param>
        /// <param name="color">Letter color in RGB888 byte[]</param>
        /// <param name="font">Font to draw letter with</param>
        public void ShowLetter(char ltr, byte[] color, ushort font = SenseLEDFont.defaultFont) {
            ShowLetter(ltr, sense_make_color_rgb(color[0], color[1], color[2]), font);
        }
        
        /// <summary>
        /// Shows a scrolling message on front buffer
        /// </summary>
        /// <param name="msg">Message to show</param>
        /// <param name="color">RGB565 color to show message in</param>
        /// <param name="scroll_delay">Delay between scroll update events (ms)</param>
        public void ShowMessage(string msg, ushort color = 0x7bef, ushort font = SenseLEDFont.defaultFont, int scroll_delay = 100) {
            IntPtr bufPtr = sense_bitmap_get_buffer(fb2ptr); // Get ptr to back buffer
            
            ulong[] fontData = SenseLEDFont.getFontById(font);
            byte w = (byte)fontData[0], h = (byte)fontData[1], bitLen = (byte)(w * h);
            byte[] columns = new byte[msg.Length * (w + 1) - 1 + 16]; // Calculate columns (chars, gaps, -1 for last gap, 2*8 blank cols)
            
            int j = 8, cols = columns.Length;
            // Fill columns array, leaving first and last 8 columns blank for effect
            for (int i = 0; i < msg.Length; ++i) {
                char c = msg[i];
                
                if (!charIsPrintable(c)) { // Ignore non-printable chars
                    cols -= w + 1; // Remove extra space
                    continue;
                }
                
                byte idx = (byte)(c - SenseLEDFont.startChar + 2);
                ulong bits = fontData[idx];
                
                // Assemble columns
                for (byte k = 0; k < w; ++k, ++j) {
                    byte col = 0;
                    for (byte l = 0; l < h; ++l) {
                        byte bit = (byte)((bits >> (bitLen - 1 - l * w - k)) & 1);
                        col |= (byte)(bit << (7 - l));
                    }
                    
                    columns[j] = col;
                }
                ++j; // Leave gap between letters
            }
            
            unsafe {
                // Draw array, screenful at a time, and animate
                //     Draw columns, shifting the 8 col window right 1 col at a time
                
                ushort* buf = (ushort*)bufPtr;
                for (int i = 0; i <= cols - 8; ++i) {
                    sense_bitmap_paint(fb2ptr, 0); // Clear back buffer
                    
                    for (j = 0; j < 8; ++j) {
                        byte col = columns[i + j];
                        
                        for (int k = 0; k < h; ++k) {
                            byte bit = (byte)((col >> (7 - k)) & 1);
                            buf[(k << 3) | j] = (bit == 1) ? color : (ushort)0;
                        }
                    }
                    
                    Show(); // Update to front buffer
                    Thread.Sleep(scroll_delay); // Wait for next frame
                }
            }
        }
        
        /// <summary>
        /// Shows a scrolling message on front buffer
        /// </summary>
        /// <param name="msg">Message to show</param> 
        /// <param name="color">RGB565 color to show message in</param>
        /// <param name="scroll_delay">Delay between scroll update events (ms)</param>
        public void ShowMessage(string msg, Color color, ushort fontId = SenseLEDFont.defaultFont, int scroll_delay = 100) {
            ShowMessage(msg, sense_make_color_rgb(color.R, color.G, color.B), fontId, scroll_delay);
        }
        
        /// <summary>
        /// Shows a scrolling message on front buffer
        /// </summary>
        /// <param name="msg">Message to show</param> 
        /// <param name="color">RGB565 color to show message in</param>
        /// <param name="scroll_delay">Delay between scroll update events (ms)</param>
        public void ShowMessage(string msg, byte[] color, ushort fontId = SenseLEDFont.defaultFont, int scroll_delay = 100) {
            ShowMessage(msg, sense_make_color_rgb(color[0], color[1], color[2]), fontId, scroll_delay);
        }
        
        /// <summary>
        /// Clear the contents of the back buffer. Redraws by default.
        /// </summary>
        /// <param name="redraw">If set to <c>true</c> redraw, otherwise don't.</param>
        public void Clear(bool redraw = true)
        {
            sense_bitmap_paint(fb2ptr, 0);

            if (redraw)
                sense_bitmap_cpy(fbptr, fb2ptr);
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
        
        
        // Gamma functions, requires patched libsense (libsense_fb_gamma.patch)
        
        /// <summary>
        /// Gets the current gamma look up table
        /// </summary>
        /// <returns>Signed byte array representing the gamma LUT (32 values ranging from 0 - 31)</returns>
        public sbyte[] GetGamma() {
            sbyte[] gamma = new sbyte[32];
            if (sense_fb_get_gamma(fbptr, gamma) == -1)
                throw new InvalidOperationException("Failed to get gamma table values");
            
            return gamma;
        }
        
        /// <summary>
        /// Sets gamma look up table values
        /// </summary>
        /// <param name="gamma">Array of 32 signed bytes (ranging from 0 - 31) representing the gamma LUT</param>
        public void SetGamma(sbyte[] gamma) {
            if (gamma.Length != 32)
                throw new InvalidOperationException("Gamma table must be 32 values in length");
            
            for (byte i = 0; i < 32; ++i) {
                if (gamma[i] > 31 || gamma[i] < 0)
                    throw new InvalidOperationException("Gamma table values out of range (valid range is 0 - 31)");
            }
            
            if (sense_fb_set_gamma (fbptr, gamma) == -1)
                throw new InvalidOperationException ("Failed to set gamma table");
        }
        
        /// <summary>
        /// Resets gamma look up table values to default
        /// </summary>
        public void ResetGamma() {
            if (sense_fb_reset_gamma (fbptr) == -1)
                throw new InvalidOperationException ("Failed to reset gamma table");
        }
        
        /// <summary>
        /// Gets low light mode value
        /// </summary>
        /// <returns><c>true</c> if low light mode is enabled, otherwise <c>false</c></returns>
        public bool GetLowLight() {
            sbyte ret = sense_fb_get_lowlight(fbptr);
            if (ret == -1)
                throw new InvalidOperationException("Failed to determine current low light setting");
            
            return ret == 1;
        }
        
        /// <summary>
        /// Set low light mode
        /// </summary>
        /// <param name="val">If <c>true</c> enable low light mode, else disable it</param>
        public void SetLowLight(bool val) {
            if (sense_fb_set_lowlight (fbptr, (sbyte)(val ? 1 : 0)) == -1)
                throw new InvalidOperationException ("Failed to change low light setting");
        }
    }
}
