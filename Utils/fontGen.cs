using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace fontGen {
    class Program {
        static Bitmap extractCharImg(Bitmap bmp, byte pos) {
            Bitmap result = new Bitmap(8, 5);
            
            Rectangle rect = new Rectangle(0, 5 * pos, 8, 5);
            Rectangle rect2 = new Rectangle(0, 0, 8, 5);
            BitmapData dt = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dt2 = result.LockBits(rect2, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
            unsafe {
                Buffer.MemoryCopy((byte*)dt.Scan0, (byte*)dt2.Scan0, 160, 160);
            }
            
            result.UnlockBits(dt2);
            bmp.UnlockBits (dt);
            result.RotateFlip(RotateFlipType.Rotate270FlipNone);
            
            return result;
        }
        
        static ulong tileToBits(Bitmap bmp) {
            Rectangle rect = new Rectangle(0, 0, 5, 8);
            BitmapData dt = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        
            ulong bits = 0;
            
            unsafe {
                uint* arr = (uint*)dt.Scan0;
                
                for (int i = 0; i < 40; ++i) {
                    uint px = arr[i] & 0xffffff;
                    
                    if (px > 0xffffff - px)
                        bits |= 1ul << (39 - i);
                }
            }
            
            bmp.UnlockBits(dt);
            
            return bits;
        }
        
        static void Main(string[] args) {
            /*if (args.Length != 1)
                return;*/
        
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string fontSrcPath = Path.Combine(basePath, "sense_hat_text.png");
            string fontTxtPath = Path.Combine(basePath, "sense_hat_text.txt");
            
            Bitmap fontSrc = new Bitmap(fontSrcPath);
            StreamReader sr = new StreamReader(fontTxtPath);
            string fontChars = sr.ReadLine();
            sr.Close();
            
            // Test code
            /*byte which = Convert.ToByte(args[0]);
            Bitmap charImg = extractCharImg(fontSrc, which);
            //charImg.Save(string.Format("char{0}.png", which), ImageFormat.Png);
            ulong bits = tileToBits(charImg);
            
            string hex = string.Format("0x{0:x}", bits);
            Console.WriteLine(hex);
            
            string pfx = "";
            Console.Write("public static ulong[] defaultFont = {\n    5, 8,\n    ");
            for (byte i = 0; i < (byte)(fontSrc.Height / 5); ++i) {
                Bitmap charImg = extractCharImg(fontSrc, i);
                ulong bits = tileToBits(charImg);
                string hex = string.Format("0x{0:x}", bits);
                
                Console.Write(pfx + hex);
                pfx = ", ";
                
                if (i % 10 == 0 && i != 0) {
                    Console.Write(", \n    ");
                    pfx = "";
                }
            }
            
            Console.WriteLine("\n};");*/
            
            if (string.IsNullOrEmpty(fontChars))
                return;
                
            ulong[] font = new ulong[95];
            for (byte i = 0; i < font.Length; ++i)
                font[i] = 0;
            
            for (byte i = 0; i < fontChars.Length; ++i) {
                Bitmap charImg = extractCharImg(fontSrc, i);
                ulong bits = tileToBits(charImg);
                
                font[fontChars[i] - ' '] = bits;
            }
            
            string pfx = "";
            Console.Write("public static ulong[] defaultFont = {\n    5, 8,\n    ");
            for (byte i = 0; i < font.Length; ++i) {
                ulong bits = font[i];
                string hex = string.Format("0x{0:x}", bits);
                
                Console.Write(pfx + hex);
                pfx = ", ";
                
                if ((i + 1) % 10 == 0) {
                    Console.Write(", \n    ");
                    pfx = "";
                }
                
                if (bits == 0 && i != 0)
                    Console.Error.WriteLine("missing data for ascii char {0} ({1})", 32 + i, (char)(32 + i));
            }
            
            Console.WriteLine("\n};");
        }
    }
}
