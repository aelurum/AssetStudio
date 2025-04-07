using System;

namespace AssetStudio
{
    public class LZ4
    {
        public static LZ4 Instance => new LZ4();

        public virtual int Decompress(ReadOnlySpan<byte> cmp, Span<byte> dec)
        {
            int cmpPos = 0;
            int decPos = 0;

            do
            {
                var (encCount, litCount) = GetLiteralToken(cmp, ref cmpPos);

                //Copy literal chunk
                litCount = GetLength(litCount, cmp, ref cmpPos);

                cmp.Slice(cmpPos, litCount).CopyTo(dec.Slice(decPos));

                cmpPos += litCount;
                decPos += litCount;

                if (cmpPos >= cmp.Length)
                {
                    break;
                }

                //Copy compressed chunk
                int back = GetChunkEnd(cmp, ref cmpPos);

                encCount = GetLength(encCount, cmp, ref cmpPos) + 4;

                int encPos = decPos - back;

                if (encCount <= back)
                {
                    dec.Slice(encPos, encCount).CopyTo(dec.Slice(decPos));
                    decPos += encCount;
                }
                else
                {
                    while (encCount-- > 0)
                    {
                        dec[decPos++] = dec[encPos++];
                    }
                }
            } while (cmpPos < cmp.Length && decPos < dec.Length);

            return decPos;
        }

        protected virtual (int encCount, int litCount) GetLiteralToken(ReadOnlySpan<byte> cmp, ref int cmpPos) =>
            ((cmp[cmpPos] >> 0) & 0xf, (cmp[cmpPos++] >> 4) & 0xf);

        protected virtual int GetChunkEnd(ReadOnlySpan<byte> cmp, ref int cmpPos) =>
            cmp[cmpPos++] << 0 | cmp[cmpPos++] << 8;

        protected virtual int GetLength(int length, ReadOnlySpan<byte> cmp, ref int cmpPos)
        {
            byte sum;

            if (length == 0xf)
            {
                do
                {
                    length += sum = cmp[cmpPos++];
                } while (sum == 0xff);
            }
            return length;
        }
    }
}