using System;
using System.Windows.Forms;

using iFlag.Properties;

namespace iFlag
{
    public partial class mainForm : Form
    {
        byte[, ,] matrix = new byte[2, 8, 8];     // The physical matrix buffer to hold the flag in
        bool blinkSpeed;                          // Blinking speed of the pattern
        const bool SLOW = false;                  // Symbol of slow blinking
        const bool FAST = true;                   // Symbol of fast blinking

                                                  // On what side of the matrix does the Arduino
                                                  // USB connector sticks out. Persistent user option.
        byte connectorSide = Settings.Default.UsbConnector;

                                                  // This defines the luminosity value of the matrix colors.
                                                  // Persistent user option.
        byte matrixLuma = Settings.Default.MatrixLuma;

                                                  // Indexes of colors usable in flag patterns, which
                                                  // match the v0.15 firmware palette, so don't change.
        const byte COLOR_BLACK =         0;
        const byte COLOR_WHITE =         1;
        const byte COLOR_RED =           2;
        const byte COLOR_GREEN =         3;
        const byte COLOR_BLUE =          4;
        const byte COLOR_YELLOW =        5;
        const byte COLOR_TEAL =          6;
        const byte COLOR_PURPLE =        7;
        const byte COLOR_ORANGE =        8;
        const byte COLOR_DIM_WHITE =     9;
        const byte COLOR_DIM_RED =      10;
        const byte COLOR_DIM_GREEN =    11;
        const byte COLOR_DIM_BLUE =     12;
        const byte COLOR_DIM_YELLOW =   13;
        const byte COLOR_DIM_TEAL =     14;
        const byte COLOR_DIM_PURPLE =   15;

        private void startMatrix()
        {
        }

        private void setMatrixLuma()
        {
            COMMAND_LUMA[3] = matrixLuma;
            SP_SendData(COMMAND_LUMA);
        }
                                                  // Translates the flag pattern to color data
                                                  // and feeds then to the matrix buffer
                                                  // to be broadcasted right away.
        public void flagToMatrix(byte[, ,] pattern, byte[] color, bool speed)
        {
            int matrixX = 0, matrixY = 0;
            byte colorIndex = 0;

            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                                                  // A matrix rotation is performed based on in what
                                                  // direction is Arduino USB connector sticking out
                                                  // of the hardware assembly.
                    switch (connectorSide)
                    {
                        case 0x00:                // Down
                            matrixX = 8 - y - 1;
                            matrixY = x;
                            break;
                        case 0x01:                // Right
                            matrixX = 8 - x - 1;
                            matrixY = 8 - y - 1;
                            break;
                        case 0x02:                // Left
                            matrixX = x;
                            matrixY = y;
                            break;
                        case 0x03:                // Up
                            matrixX = y;
                            matrixY = 8 - x - 1;
                            break;
                    }
                    colorIndex = pattern[0, matrixX, matrixY];
                    if (colorIndex != 9) matrix[0, x, y] = color[colorIndex];
                                                  // For single-frame patterns the second frame of the matrix
                                                  // is a clone of the first one.
                    colorIndex = pattern[pattern.Length >= 128 ? 1 : 0, matrixX, matrixY];
                    if (colorIndex != 9) matrix[1, x, y] = color[colorIndex];
                }

            blinkSpeed = speed;
        }

        public void flagToMatrix(byte[, ,] pattern, byte[] color)
        {
            flagToMatrix(pattern, color, blinkSpeed);
        }

                                                  // Processes the matrix pixels into data packets
                                                  // and sends them out through the USB connection
        private bool broadcastMatrix()
        {
            SP_SendData(COMMAND_NOBLINK);

            for (int frame = 0; frame < 2; frame++)
            {
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x += 4)
                    {
                        SP_SendData(new byte[8] {
                            0xFF,                                        // FF
                            Convert.ToByte( x ),                         // 00..07
                            Convert.ToByte( y ),                         // 00..07
                            Convert.ToByte( matrix[ frame, x, y ] ),     // 00..FE
                            Convert.ToByte( matrix[ frame, x + 1, y ] ), // 00..FE
                            Convert.ToByte( matrix[ frame, x + 2, y ] ), // 00..FE
                            Convert.ToByte( matrix[ frame, x + 3, y ] ), // 00..FE
                            0x00
                        });
                    }
                if (frame == 0) SP_SendData(COMMAND_DRAW);
                else SP_SendData(blinkSpeed ? COMMAND_BLINK_FAST : COMMAND_BLINK_SLOW);
            }

            SP_SendData(COMMAND_DRAW);

            return true;
        }

    }
}
