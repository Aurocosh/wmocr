using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using CommandLine;
using Windows.Storage.Streams;
//using Windows.UI.Xaml.Data;

namespace wm_ocr_cli
{
    public class Options
    {
        [Option('i', "input", Required = false, HelpText = "Path to the image file to process with OCR.")]
        public string? Input { get; set; }

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

        [Option('l', "lang", HelpText = "Which language should be used for OCR. Only languages with installed OCR support can be used.")]
        public string? Language { get; set; }

        [Option('x', "lang-list", HelpText = "Print the list of all locally available languages for OCR.")]
        public bool LanguageList { get; set; }
    }

    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var result = await Parser.Default.ParseArguments<Options>(args)
              .MapResult(Execute, HandleErrors);
        }

        static async Task<int> Execute(Options options)
        {
            if (options.LanguageList)
            {
                Console.WriteLine("Supported languages:");
                foreach (var language in OcrEngine.AvailableRecognizerLanguages)
                    Console.WriteLine(language.LanguageTag);
            }

            try
            {
                return await PerformOcr(options);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Caught exception: {error.Message}");
                return 1;
            }
        }

        static async Task<int> PerformOcr(Options options)
        {
            if (options.Input == null)
            {
                Console.Error.WriteLine($"No input file provided. To show help enter: wmocr --help.");
                return 1;
            }

            var fullImagePath = Path.GetFullPath(options.Input);
            if (!File.Exists(fullImagePath))
            {
                Console.Error.WriteLine($"Provided input file does not exist.");
                return 1;
            }

            using var fileStream = new FileStream(fullImagePath, FileMode.Open, FileAccess.Read);
            var decoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var result = await RecognizeAsync(bitmap, options.Language);
            if (result == null)
            {
                Console.Error.WriteLine($"Failed to parse image text");
                return 1;
            }

            await OutputResult(options, bitmap, decoder.DecoderInformation.CodecId, result);
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
                boundingBox = boundingBox.Inflate(options.BoundingBoxMargin);

                var imageRect = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
                boundingBox.Intersect(imageRect);

                if (boundingBox.IsEmpty)
                {
                    Console.Error.WriteLine($"Failed to crop the image. Bounding box is invalid");
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
        }

        public static async Task<SoftwareBitmap> CropSoftwareBitmapAsync(SoftwareBitmap originalBitmap, BitmapBounds bitmapBounds)
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
                Console.Error.WriteLine($"Error: {error}");
            return 1;
        }

        static async Task<OcrResult?> RecognizeAsync(SoftwareBitmap bitmap, string? language)
        {
            OcrEngine ocrEngine;
            if (language == null)
            {
                ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
            else
            {
                var lang = new Language(language);
                ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
            }

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