namespace PsCloneTester.Services;

public static class Crc32Helper
{
    private static readonly uint[] Table;

    static Crc32Helper()
    {
        Table = new uint[256];
        const uint poly = 0xEDB88320;
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ poly;
                else
                    crc >>= 1;
            }
            Table[i] = crc;
        }
    }

    /// <summary>
    /// Computes PlayStation standard reflected CRC-32 over a byte buffer.
    /// Bluetooth DualSense output reports require seeding/prefixing with 0xA2.
    /// </summary>
    public static uint ComputePsBluetoothCrc(byte[] report, int lengthWithoutCrc)
    {
        uint crc = 0xFFFFFFFF;

        // Sony Bluetooth CRC incorporates 0xA2 (BT output header)
        byte btHeader = 0xA2;
        crc = Table[(crc ^ btHeader) & 0xFF] ^ (crc >> 8);

        for (int i = 0; i < lengthWithoutCrc; i++)
        {
            byte b = report[i];
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }
}
