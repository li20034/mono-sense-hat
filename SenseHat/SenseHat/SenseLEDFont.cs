namespace SenseHat {
    // Font definitions for SenseLED
    // Format
    //     ulong = 64 bits, 1 bit per LED
    //     array contains:
    //           2 elements representing width and height respectively
    //           95 elements representing ASCII char (decimal) 32 to 126
    //     to save space, only LSBs are used (if font needs 40 bits, we use 40LSBs)
    //     bit order is still MSB first
    public class SenseLEDFont {
        public const char startChar = ' '; // ASCII 32
        public const char endChar = '~'; // ASCII 126
        public const byte fontLen = endChar - startChar + 1; // Length of font def arrays
        
        public static ulong[] defaultFont = {
            5, 8,
            0x0, 0x108421004, 0x294a00000, 0x295f57d4a, 0x11f4717c4, 0x632222263, 0x32544564d, 0x308800000, 0x88842082, 0x208210888, 
            0x9575480, 0x84f9080, 0x3088, 0xf8000, 0x18c, 0x2222200, 0x3a33ae62e, 0x11842108e, 0x3a211111f, 0x7c441062e, 
            0x8ca97c42, 0x7e1e0862e, 0x1910f462e, 0x7c2221084, 0x3a317462e, 0x3a317844c, 0x18c03180, 0x18c03088, 0x88882082, 0x1f07c00, 
            0x208208888, 0x3a2111004, 0x3a216d6ae, 0x3a31fc631, 0x7a31f463e, 0x3a308422e, 0x72518c65c, 0x7e10f421f, 0x7e10f4210, 0x3a30bc62f, 
            0x4631fc631, 0x38842108e, 0x1c4210a4c, 0x4654c5251, 0x42108421f, 0x4775ac631, 0x4639ace31, 0x3a318c62e, 0x7a31f4210, 0x3a318d64d, 
            0x7a31f5251, 0x3e107043e, 0x7c8421084, 0x46318c62e, 0x46318c544, 0x4631ad771, 0x462a22a31, 0x462a21084, 0x7c222221f, 0x39084210e, 
            0x20820820, 0x38421084e, 0x0, 0x1f, 0x0, 0xe0be2f, 0x4216cc62e, 0xe8422e, 0x42d9c62f, 0xe8fe0e, 
            0x8a471084, 0xf8bc2e, 0x4216cc631, 0x100c2108e, 0x80210a4c, 0x210953149, 0x30842108e, 0x1aad6b5, 0x16cc631, 0xe8c62e, 
            0x1e8fa10, 0xf8bc21, 0xb62108, 0xf8383e, 0x8e210a2, 0x118c66d, 0x118c544, 0x118d6aa, 0x1931193, 0x1149898, 
            0x1f1111f, 0x0, 0x108421084, 0x0, 0xdb0000
        };

    }
}
