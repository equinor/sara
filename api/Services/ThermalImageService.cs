using System.Runtime.InteropServices;
using api.Database.Models;
using BitMiracle.LibTiff.Classic;

namespace api.Services;

public record ThermalImageData(
    byte[] FloatBytes,
    int Width,
    int Height,
    float MinTemperature,
    float MaxTemperature
);

public interface IThermalImageService
{
    Task<ThermalImageData> GetThermalImageDataAsync(BlobStorageLocation location);
}

public class ThermalImageService(IBlobStorageService blobStorageService) : IThermalImageService
{
    public async Task<ThermalImageData> GetThermalImageDataAsync(BlobStorageLocation location)
    {
        using var tiffStream = await blobStorageService.DownloadBlobAsync(location);

        var (temperatures, width, height) = ReadTiffTemperatures(tiffStream);

        float minTemp = float.MaxValue;
        float maxTemp = float.MinValue;
        for (int i = 0; i < temperatures.Length; i++)
        {
            float temp = temperatures[i];
            if (temp < minTemp)
                minTemp = temp;
            if (temp > maxTemp)
                maxTemp = temp;
        }

        // Serialize float[] to little-endian bytes.
        var floatBytes = new byte[temperatures.Length * sizeof(float)];
        MemoryMarshal.AsBytes(temperatures.AsSpan()).CopyTo(floatBytes);

        return new ThermalImageData(floatBytes, width, height, minTemp, maxTemp);
    }

    /// <summary>
    /// Read pixel data from a TIFF stream as float temperatures.
    /// Supports 32-bit float, 64-bit float, 16-bit unsigned, and 8-bit unsigned sample formats.
    /// </summary>
    private static (float[] temperatures, int width, int height) ReadTiffTemperatures(
        Stream tiffStream
    )
    {
        using var tiff = Tiff.ClientOpen("thermal", "r", tiffStream, new TiffStream());
        if (tiff == null)
            throw new InvalidOperationException("Failed to open TIFF stream.");

        int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

        int bitsPerSample = 32;
        var bpsField = tiff.GetField(TiffTag.BITSPERSAMPLE);
        if (bpsField != null)
            bitsPerSample = bpsField[0].ToInt();

        int sampleFormat = (int)SampleFormat.UINT;
        var sfField = tiff.GetField(TiffTag.SAMPLEFORMAT);
        if (sfField != null)
            sampleFormat = sfField[0].ToInt();

        var temperatures = new float[width * height];
        int scanlineSize = tiff.ScanlineSize();
        byte[] buffer = new byte[scanlineSize];

        for (int y = 0; y < height; y++)
        {
            tiff.ReadScanline(buffer, y);

            for (int x = 0; x < width; x++)
            {
                float value = (bitsPerSample, sampleFormat) switch
                {
                    (32, (int)SampleFormat.IEEEFP) => BitConverter.ToSingle(buffer, x * 4),
                    (64, (int)SampleFormat.IEEEFP) => (float)BitConverter.ToDouble(buffer, x * 8),
                    (16, (int)SampleFormat.UINT or (int)SampleFormat.VOID) => BitConverter.ToUInt16(
                        buffer,
                        x * 2
                    ),
                    (16, (int)SampleFormat.INT) => BitConverter.ToInt16(buffer, x * 2),
                    (8, _) => buffer[x],
                    _ => throw new NotSupportedException(
                        $"Unsupported TIFF pixel format: {bitsPerSample} bits/sample, sample format {sampleFormat}."
                    ),
                };

                temperatures[y * width + x] = value;
            }
        }

        return (temperatures, width, height);
    }
}
