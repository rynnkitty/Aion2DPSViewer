using System;

namespace Aion2DPSViewer.Packet;

public static class Lz4Decoder
{
    public static int Decompress(
        byte[] src,
        int srcOffset,
        int srcLen,
        byte[] dst,
        int dstOffset,
        int dstLen)
    {
        int num1 = srcOffset + srcLen;
        int num2 = dstOffset + dstLen;
        int num3 = srcOffset;
        int num4 = dstOffset;
        while (num3 < num1)
        {
            byte[] numArray = src;
            int index1 = num3;
            int num5 = index1 + 1;
            byte num6 = numArray[index1];
            int num7 = (int)num6 >> 4;
            int num8 = (int)num6 & 15;
            if (num7 == 15)
            {
                while (num5 < num1)
                {
                    byte num9 = src[num5++];
                    num7 += (int)num9;
                    if (num9 != byte.MaxValue)
                        break;
                }
            }
            if (num4 + num7 > num2 || num5 + num7 > num1)
                return -1;
            Buffer.BlockCopy(src, num5, dst, num4, num7);
            int index2 = num5 + num7;
            num4 += num7;
            if (index2 < num1)
            {
                if (index2 + 2 > num1)
                    return -1;
                int num10 = (int)src[index2] | (int)src[index2 + 1] << 8;
                num3 = index2 + 2;
                if (num10 == 0)
                    return -1;
                int num11 = num8 + 4;
                if (((int)num6 & 15) == 15)
                {
                    while (num3 < num1)
                    {
                        byte num12 = src[num3++];
                        num11 += (int)num12;
                        if (num12 != byte.MaxValue)
                            break;
                    }
                }
                int num13 = num4 - num10;
                if (num13 < dstOffset || num4 + num11 > num2)
                    return -1;
                for (int index3 = 0; index3 < num11; ++index3)
                    dst[num4++] = dst[num13 + index3];
            }
            else
                break;
        }
        return num4 - dstOffset;
    }
}
