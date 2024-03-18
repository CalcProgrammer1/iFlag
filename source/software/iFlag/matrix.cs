using System;
using System.Windows.Forms;

using iFlag.Properties;

namespace iFlag
{
    public partial class mainForm : Form
    {
        byte[, ,] matrix = new byte[2, 8, 8];           // Current combined matrix buffer
        byte[, ,] deviceMatrix = new byte[2, 8, 8];     // Current combined matrix buffer on the device
        byte[, ,] flagMatrix = new byte[2, 8, 8];       // Current flag matrix buffer
        byte[, ,] overlayMatrix = new byte[2, 8, 8];    // Current overlay matrix buffer
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
        const byte NO_COLOR =          255;

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

        OpenRGB.NET.Models.Color flag_idx_to_openrgb_color(Byte idx)
        {
            OpenRGB.NET.Models.Color color = new OpenRGB.NET.Models.Color(0, 0, 0);

            switch(idx)
            {
                case COLOR_BLACK:
                    color = new OpenRGB.NET.Models.Color(0, 0, 0);
                    break;

                case COLOR_WHITE:
                    color = new OpenRGB.NET.Models.Color(255, 255, 255);
                    break;

                case COLOR_RED:
                    color = new OpenRGB.NET.Models.Color(255, 0, 0);
                    break;

                case COLOR_GREEN:
                    color = new OpenRGB.NET.Models.Color(0, 255, 0);
                    break;

                case COLOR_BLUE:
                    color = new OpenRGB.NET.Models.Color(0, 0, 255);
                    break;

                case COLOR_YELLOW:
                    color = new OpenRGB.NET.Models.Color(255, 255, 0);
                    break;

                case COLOR_TEAL:
                    color = new OpenRGB.NET.Models.Color(0, 255, 255);
                    break;

                case COLOR_PURPLE:
                    color = new OpenRGB.NET.Models.Color(255, 0, 255);
                    break;

                case COLOR_ORANGE:
                    color = new OpenRGB.NET.Models.Color(255, 64, 0);
                    break;

                case COLOR_DIM_WHITE:
                    color = new OpenRGB.NET.Models.Color(64, 64, 0);
                    break;

                case COLOR_DIM_RED:
                    color = new OpenRGB.NET.Models.Color(64, 0, 0);
                    break;

                case COLOR_DIM_GREEN:
                    color = new OpenRGB.NET.Models.Color(0, 64, 0);
                    break;

                case COLOR_DIM_BLUE:
                    color = new OpenRGB.NET.Models.Color(0, 0, 64);
                    break;

                case COLOR_DIM_YELLOW:
                    color = new OpenRGB.NET.Models.Color(64, 64, 0);
                    break;

                case COLOR_DIM_TEAL:
                    color = new OpenRGB.NET.Models.Color(0, 64, 64);
                    break;

                case COLOR_DIM_PURPLE:
                    color = new OpenRGB.NET.Models.Color(64, 0, 64);
                    break;
            }

            return color;
        }

        OpenRGB.NET.OpenRGBClient openrgb_client = new OpenRGB.NET.OpenRGBClient("127.0.0.1", 6742, "iFlag", true, 1000, 2);
        OpenRGB.NET.Models.Device[] openrgb_devices;

        private void startMatrix()
        {
            resetOverlay();
            setMatrixLuma();
            // matrixToDevice();

            openrgb_client.Connect();
            openrgb_devices = openrgb_client.GetAllControllerData();
        }

        private void setMatrixLuma()
        {
            COMMAND_LUMA[3] = matrixLuma;
            SP_SendData(COMMAND_LUMA);
        }
                                                  // Translates the flag pattern to color data
                                                  // and feeds then to the matrix buffer
                                                  // to be broadcasted right away.
        public void patternToMatrix(ref byte[, ,] matrix, byte[, ,] pattern, byte[] color, bool speed)
        {
            byte colorIndex = 0;

            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    colorIndex = pattern[0, x, y];
                    if (colorIndex != 9) matrix[0, x, y] = color[colorIndex];

                                                  // For single-frame patterns the second frame of the matrix
                                                  // is a clone of the first one.
                    colorIndex = pattern[pattern.Length >= 128 ? 1 : 0, x, y];
                    if (colorIndex != 9) matrix[1, x, y] = color[colorIndex];
                }

            blinkSpeed = speed;
        }

        public void patternToMatrix(ref byte[, ,] matrix, byte[, ,] pattern, byte[] color)
        {
            patternToMatrix(ref matrix, pattern, color, blinkSpeed);
        }
        
                                                  // This repopulates the matrixOverlay with NO_COLOR
                                                  // rather than black populated by default
        public void resetOverlay()
        {
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        overlayMatrix[f, x, y] = NO_COLOR;
        }

                                                  // Compares matrix in software memory with a snapshot
                                                  // of matrix currently on the device dot by dot
                                                  // and returns `true` if not exactly the same
        private bool worthBroadcasting()
        {
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        if (matrix[f, x, y] != deviceMatrix[f, x, y])
                            return true;
            return false;
        }

                                                  // Combines matrix with overlays and determines
                                                  // if there is something to transmit to the device
        private bool broadcastMatrix()
        {
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        matrix[f, x, y] = overlayMatrix[f, x, y] == NO_COLOR ? flagMatrix[f, x, y] : overlayMatrix[f, x, y];

            if (worthBroadcasting())
            {
                Console.WriteLine("{0} {1} {2}", DateTime.Now, flagOnDisplayLabel, overlaysOnDisplayLabel);
                updateSignalLabels();

                for (int f = 0; f < 2; f++)
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                            deviceMatrix[f, x, y] = matrix[f, x, y];

                matrixToDevice();
            } 

            for (uint device = 0; device < openrgb_devices.Length; device++)
            {
                uint led_offset = 0;

                for (uint zone = 0; zone < openrgb_devices[device].Zones.Length; zone++)
                {
                    uint zone_leds = openrgb_devices[device].Zones[zone].LedCount;
                    OpenRGB.NET.Models.MatrixMap zone_matrix = openrgb_devices[device].Zones[zone].MatrixMap;

                    switch (openrgb_devices[device].Zones[zone].Type)
                    {
                        case OpenRGB.NET.Enums.ZoneType.Single:
                            for (uint led_in_zone = 0; led_in_zone < zone_leds; led_in_zone++)
                            {
                                // Set all LEDs in a Single type zone to the center of the flag (position 4,4)
                                openrgb_devices[device].Colors[led_in_zone + led_offset] = flag_idx_to_openrgb_color(flagMatrix[0, 4, 4]);
                            }
                            break;

                        case OpenRGB.NET.Enums.ZoneType.Matrix:
                            // If matrix map is populated, set all LEDs in a Matrix type zone to a scaled representation of the flag matrix
                            if(zone_matrix != null)
                            {
                                for (uint index_in_matrix = 0; index_in_matrix < zone_matrix.Matrix.Length; index_in_matrix++)
                                {
                                    uint matrix_x = index_in_matrix % zone_matrix.Width;
                                    uint matrix_y = index_in_matrix / zone_matrix.Width;

                                    uint x = ((8 * matrix_x) / zone_matrix.Width);
                                    uint y = ((8 * matrix_y) / zone_matrix.Height);

                                    if (zone_matrix.Matrix[matrix_y, matrix_x] != 0xFFFFFFFF)
                                    {
                                        openrgb_devices[device].Colors[led_offset + zone_matrix.Matrix[matrix_y, matrix_x]] = flag_idx_to_openrgb_color(flagMatrix[0, x, y]);
                                    }
                                }
                                break;
                            }

                            // Intentional fall-through, treat matrix zones without map as linear zones
                            goto case OpenRGB.NET.Enums.ZoneType.Linear;

                        case OpenRGB.NET.Enums.ZoneType.Linear:
                            for (uint led_in_zone = 0; led_in_zone < zone_leds; led_in_zone++)
                            {
                                // Set all LEDs in a Linear type zone to the center row of the flag (line 4), scaled to device length
                                uint x = ((8 * led_in_zone) / zone_leds);
                                openrgb_devices[device].Colors[led_in_zone + led_offset] = flag_idx_to_openrgb_color(flagMatrix[0, x, 4]);
                            }
                            break;
                    }

                    led_offset += zone_leds;
                }

                openrgb_client.UpdateLeds((int)device, openrgb_devices[device].Colors);
            }

            return true;
        }

                                                  // Processes the matrix pixels into data packets
                                                  // and sends them out through the USB connection
        private bool matrixToDevice()
        {
            SP_SendData(COMMAND_NOBLINK);

            for (int frame = 0; frame < 2; frame++)
            {
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x += 4)
                        SP_SendData(new byte[8] {
                            0xFF,                                           // FF
                            Convert.ToByte( x ),                            // 00..07
                            Convert.ToByte( y ),                            // 00..07
                            Convert.ToByte( matrixDot( frame, x + 0, y ) ), // 00..FE
                            Convert.ToByte( matrixDot( frame, x + 1, y ) ), // 00..FE
                            Convert.ToByte( matrixDot( frame, x + 2, y ) ), // 00..FE
                            Convert.ToByte( matrixDot( frame, x + 3, y ) ), // 00..FE
                            0x00,
                        });
                SP_SendData(COMMAND_DRAW);
            }

            SP_SendData(blinkSpeed ? COMMAND_BLINK_FAST : COMMAND_BLINK_SLOW);

            return true;
        }

                                          // A matrix rotation is performed based on in what
                                          // direction is Arduino USB connector sticking out
                                          // of the hardware assembly.
        private byte matrixDot(int frame, int x, int y)
        {
            switch (connectorSide)
            {
                case 0x01: return matrix[frame, 8 - x - 1, 8 - y - 1];       // Right
                case 0x02: return matrix[frame, x, y];                       // Left
                case 0x03: return matrix[frame, y, 8 - x - 1];               // Up
                default: return matrix[frame, 8 - y - 1, x];                 // Down
            }
        }
    }
}
