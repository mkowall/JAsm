#define RozmycieCPP _declspec(dllexport)
#include <iostream>
#include <stdio.h>
#include <cstdint>
#include <algorithm>

extern "C"
{
    struct Params
    {
        uint8_t* input;
        uint8_t* output;
        uint64_t radius;
        float* kernell;
        uint64_t width;
        uint64_t height;
    };

    void calculate(uint8_t* input, uint8_t* output, float* kernell, uint64_t width, uint64_t height, uint64_t radius)
    {
        int counter = 0;
        int kernellSize = 2 * radius + 1;
        for (int i = radius; i < height - radius; i++)
        {
            for (int j = radius; j < width - radius; j++)
            {
                int pixelSize = /*sizeof(uint8_t) * 3;*/3;

                float redSum = 0;
                float greenSum = 0;
                float blueSum = 0;

                int integerRadius = radius;
                int minusRadius = -1 * radius;
                for (int x = minusRadius; x < integerRadius + 1; x++)
                {
                    for (int y = minusRadius; y < integerRadius + 1; y++)
                    {
                        int offset = (i + x) * width + j + y;
                        float kernellValue = *(kernell + (x + radius) * kernellSize + y + radius);
                        uint8_t redPixel = *(input + offset * pixelSize);
                        uint8_t greenPixel = *(input + offset * pixelSize + 1);
                        uint8_t bluePixel = *(input + offset * pixelSize + 2);

                        redSum += redPixel * kernellValue;
                        greenSum += greenPixel * kernellValue;
                        blueSum += bluePixel * kernellValue;
                    }
                }

                if (redSum > 255) redSum = 255;
                if (greenSum > 255) greenSum = 255;
                if (blueSum > 255) blueSum = 255;

                *(output + counter) = redSum;
                ++counter;
                *(output + counter) = greenSum;
                ++counter;
                *(output + counter) = blueSum;
                ++counter;
            }
        }
        return;
    }

    RozmycieCPP int SendToC(Params* parameters)
    {
        calculate(parameters->input, parameters->output, parameters->kernell, parameters->width, parameters->height, parameters->radius);
        return 0;
    }
}