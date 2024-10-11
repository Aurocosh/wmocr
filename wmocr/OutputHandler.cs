using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;
using System.Text;

namespace wmocr
{
    class OutputHandler
    {
        public static async Task<int> OutputResult(Options options, SoftwareBitmap bitmap, Guid decoderCodecId, OcrResult ocrResult)
        {
            string textResult;
            if (options.OneLine)
            {
                textResult = ocrResult.Text;
            }
            else
            {
                var stringBuilder = new StringBuilder();
                foreach (var line in ocrResult.Lines)
                    stringBuilder.AppendLine(line.Text);
                textResult = stringBuilder.ToString();
            }

            // Output result to file
            if (options.Output != null)
            {
                var fullOutputPath = Path.GetFullPath(options.Output);
                if (options.Append)
                    File.AppendAllText(fullOutputPath, textResult);
                else
                    File.WriteAllText(fullOutputPath, textResult);
            }

            // Output result to stdout
            if (options.Stdout || (options.Output == null && options.Crop == null))
            {
                Console.WriteLine(textResult);
            }

            // Crop image to file
            if (options.Crop != null)
            {
                Rect boundingBox = Rect.Empty;
                foreach (var line in ocrResult.Lines)
                    foreach (var word in line.Words)
                        boundingBox.Union(word.BoundingRect);
                boundingBox = boundingBox.Inflate(options.BoundingBoxMargin);
                boundingBox.Intersect(new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

                if (boundingBox.IsEmpty)
                {
                    Console.Error.WriteLine($"Failed to crop the image. Bounding box is invalid");
                    return 1;
                }
                else
                {
                    var bounds = new BitmapBounds
                    {
                        X = (uint)boundingBox.X,
                        Y = (uint)boundingBox.Y,
                        Width = (uint)boundingBox.Width,
                        Height = (uint)boundingBox.Height
                    };
                    SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(bitmap, bounds);

                    var fullCropPath = Path.GetFullPath(options.Crop);
                    Guid encoderCodecId = GetEncoderIdFromDecoder(decoderCodecId);
                    await SaveSoftwareBitmapToFileAsync(croppedBitmap, fullCropPath, encoderCodecId);
                }
            }

            return 0;
        }

        private static async Task<SoftwareBitmap> CropSoftwareBitmapAsync(SoftwareBitmap originalBitmap, BitmapBounds bitmapBounds)
        {
            var convertedBitmap = SoftwareBitmap.Convert(originalBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var stream = new InMemoryRandomAccessStream();

            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(convertedBitmap);
            await encoder.FlushAsync();

            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var transform = new BitmapTransform
            {
                Bounds = bitmapBounds
            };

            SoftwareBitmap croppedBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return croppedBitmap;
        }

        private static async Task SaveSoftwareBitmapToFileAsync(SoftwareBitmap softwareBitmap, string filePath, Guid codecId)
        {
            var bitmapToSave = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(codecId, fileStream.AsRandomAccessStream());
            encoder.SetSoftwareBitmap(bitmapToSave);
            await encoder.FlushAsync();
        }

        // Function to map decoder's container format to an appropriate encoder
        private static Guid GetEncoderIdFromDecoder(Guid containerFormat)
        {
            if (containerFormat == BitmapDecoder.PngDecoderId)
                return BitmapEncoder.PngEncoderId;
            else if (containerFormat == BitmapDecoder.JpegDecoderId)
                return BitmapEncoder.JpegEncoderId;
            else if (containerFormat == BitmapDecoder.BmpDecoderId)
                return BitmapEncoder.BmpEncoderId;
            else if (containerFormat == BitmapDecoder.GifDecoderId)
                return BitmapEncoder.GifEncoderId;
            else if (containerFormat == BitmapDecoder.TiffDecoderId)
                return BitmapEncoder.TiffEncoderId;
            else
                return Guid.Empty;
        }
    }
}