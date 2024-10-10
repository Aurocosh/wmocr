using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Foundation;
using CommandLine;
using Windows.Storage.Streams;
//using Windows.UI.Xaml.Data;

namespace wm_ocr_cli
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path to the image file to process with OCR.")]
        public required string Input { get; set; }

        [Option('o', "output", Required = false, HelpText = "Path to output text file. Writes OCR result to this file.")]
        public string? Output { get; set; }

        [Option('s', "stdout", Required = false, HelpText = "Perform OCR and print result to stdout. Default behaviour if no other output options is specified.")]
        public bool Stdout { get; set; }

        [Option('c', "crop", Required = false, HelpText = "Path to cropped output image.Detect bounding box for all text and crop original image to this bounding box.")]
        public string? Crop { get; set; }

        [Option('b', "bb-margin", Required = false, Default = 10, HelpText = "Bounding box margin.The bounding box will be expanded by this value.")]
        public int BoundingBoxMargin { get; set; }

        [Option('a', "append", HelpText = "When writing to text file append instead of overwriting the file.")]
        public bool Append { get; set; }
    }

    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var result = await Parser.Default.ParseArguments<Options>(args)
              .MapResult(
                  options => Execute(options),
                  errors => HandleErrors(errors));
        }

        static async Task<int> Execute(Options options)
        {
            try
            {
                return await DoStuff(options);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Caught exception: {error.Message}");
                return 1;
            }
        }

        static async Task<int> DoStuff(Options options)
        {
            var fullImagePath = Path.GetFullPath(options.Input);
            if (!File.Exists(fullImagePath))
            {
                Console.Error.WriteLine($"Provided input file does not exist.");
                return 1;
            }

            (SoftwareBitmap bitmap, Guid decoderCodecId) = await GetIputBitmap(fullImagePath);
            var result = await RecognizeAsync(bitmap, "ru");
            if (result == null)
            {
                Console.Error.WriteLine($"Failed to parse image text");
                return 1;
            }

            await OutputResult(options, bitmap, decoderCodecId, result);
            return 0;
        }

        static string ProcessResult(string result)
        {
            result = result.Trim();
            // TODO
            return result;
        }

        static async Task OutputResult(Options options, SoftwareBitmap bitmap, Guid decoderCodecId, OcrResult ocrResult)
        {
            string textResult = ProcessResult(ocrResult.Text);

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
                Rect boundingBox = GetCompleteOcrBoundingBox(ocrResult);
                Console.WriteLine($"Total bounding box: {boundingBox}");

                boundingBox = boundingBox.Inflate(options.BoundingBoxMargin);
                Console.WriteLine($"Expanded bounding box: {boundingBox}");

                var imageRect = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
                boundingBox.Intersect(imageRect);

                Console.WriteLine($"Cropping bounding box: {boundingBox}");
                if (boundingBox.IsEmpty)
                {
                    Console.Error.WriteLine($"Failed to crop the image. Bounding box is invalid");
                }
                else
                {
                    uint cropX = (uint)boundingBox.X;
                    uint cropY = (uint)boundingBox.Y;
                    uint cropWidth = (uint)boundingBox.Width;
                    uint cropHeight = (uint)boundingBox.Height;
                    SoftwareBitmap croppedBitmap = await CropSoftwareBitmapAsync(bitmap, cropX, cropY, cropWidth, cropHeight);

                    var fullCropPath = Path.GetFullPath(options.Crop);
                    //StorageFile storageFile = await StorageFile.GetFileFromPathAsync(fullCropPath);

                    Guid encoderCodecId = GetEncoderIdFromDecoder(decoderCodecId);
                    await SaveSoftwareBitmapToFileAsync(croppedBitmap, fullCropPath, encoderCodecId);
                }
            }
        }

        public static async Task<SoftwareBitmap> CropSoftwareBitmapAsync(SoftwareBitmap originalBitmap, uint cropX, uint cropY, uint cropWidth, uint cropHeight)
        {
            var convertedBitmap = SoftwareBitmap.Convert(originalBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            using var stream = new InMemoryRandomAccessStream();

            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(convertedBitmap);
            await encoder.FlushAsync();

            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            var bounds = new BitmapBounds
            {
                X = cropX,
                Y = cropY,
                Width = cropWidth,
                Height = cropHeight
            };

            var transform = new BitmapTransform
            {
                Bounds = bounds
            };

            SoftwareBitmap croppedBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return croppedBitmap;
        }

        public static async Task SaveSoftwareBitmapToFileAsync(SoftwareBitmap softwareBitmap, string filePath, Guid codecId)
        {
            var bitmapToSave = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            using var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(codecId, fileStream.AsRandomAccessStream());
            encoder.SetSoftwareBitmap(bitmapToSave);
            await encoder.FlushAsync();
        }

        // Handle parsing errors
        private static async Task<int> HandleErrors(IEnumerable<CommandLine.Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"Error: {error}");
            }
            return 1;
        }

        static OcrEngine GetOcrEngine(string language)
        {
            if (language.Length == 0)
                return OcrEngine.TryCreateFromUserProfileLanguages();

            var lang = new Language(language);
            return OcrEngine.TryCreateFromLanguage(lang);
        }

        static async Task<Tuple<SoftwareBitmap, Guid>> GetIputBitmap(string imagePath)
        {
            using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            var decoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
            Guid decoderCodecId = decoder.DecoderInformation.CodecId;
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            return Tuple.Create(bitmap, decoderCodecId);
        }

        static async Task<OcrResult?> RecognizeAsync(SoftwareBitmap bitmap, string language)
        {
            OcrEngine ocrEngine = GetOcrEngine(language);
            if (ocrEngine == null)
            {
                Console.Error.WriteLine("Selected language is not available.");
                return null;
            }
            if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension)
            {
                Console.Error.WriteLine($"Bitmap dimensions ({bitmap.PixelWidth}x{bitmap.PixelHeight}) are too big for OCR. Max image dimension is {OcrEngine.MaxImageDimension}.");
                return null;
            }

            // Recognize text from image.
            return await ocrEngine.RecognizeAsync(bitmap);
        }

        static Rect GetCompleteOcrBoundingBox(OcrResult ocrResult)
        {
            Rect boundingBox = Rect.Empty;
            foreach (var line in ocrResult.Lines)
                foreach (var word in line.Words)
                    boundingBox.Union(word.BoundingRect);
            return boundingBox;
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