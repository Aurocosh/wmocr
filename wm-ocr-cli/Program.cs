using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.Ocr;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Foundation;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using CommandLine;
using Windows.UI.Xaml.Data;

namespace wm_ocr_cli
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path to the image file to process with OCR.")]
        public string Input { get; set; }

        [Option('o', "output", Required = false, HelpText = "Path to output text file. Writes OCR result to this file.")]
        public string Output { get; set; }

        [Option('s', "stdout", Required = false, HelpText = "Perform OCR and print result to stdout. Default behaviour if no other output options is specified.")]
        public string Stdout { get; set; }

        [Option('c', "crop", Required = false, HelpText = "Path to cropped output image.Detect bounding box for all text and crop original image to this bounding box.")]
        public string Crop { get; set; }

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
                  options => Execute(options), // Execute and capture result
                  errors => HandleErrors(errors)); // Handle errors

            //Console.WriteLine($"retValue= {result}");
        }

        static async Task<int> Execute(Options options)
        {
            var fullImagePath = Path.GetFullPath(options.Input);
            if (!File.Exists(fullImagePath))
            {
                Console.Error.WriteLine($"Provided input file does not exist.");
                return 1;
            }

            //string imagePath = "D:\\Temp\\test.jpg";

            try
            {
                var result = RecognizeAsync(fullImagePath, "ru").GetAwaiter().GetResult();

                if (result != null)
                {
                    Console.WriteLine($"Result text:\n {result.Text}");

                    Rect boundingBox = GetCompleteOcrBoundingBox(result);
                    Console.WriteLine($"Total bounding box: {boundingBox}");
                }
                else
                {
                    Console.WriteLine($"Failed to parse image text");
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Error: " + error.Message);
                return 1;
            }
            Console.ReadLine();
            return 0;
        }


        // Handle parsing errors
        private static Task<int> HandleErrors(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"Error: {error}");
            }
            //Environment.Exit(1); // Exit with error code
            return Task.FromResult(1);
        }

        static async Task<SoftwareBitmap> LoadImageAsync(StorageFile file)
        {
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
        }

        static OcrEngine GetOcrEngine(string language)
        {
            if (language.Length == 0)
                return OcrEngine.TryCreateFromUserProfileLanguages();

            var lang = new Language(language);
            return OcrEngine.TryCreateFromLanguage(lang);
        }

        static async Task<OcrResult> RecognizeAsync(string imagePath, string language)
        {
            OcrEngine ocrEngine = GetOcrEngine(language);
            if (ocrEngine == null)
            {
                Console.Error.WriteLine("Selected language is not available.");
                return null;
            }

            var fullImagePath = Path.GetFullPath(imagePath);
            StorageFile storageFile = await StorageFile.GetFileFromPathAsync(fullImagePath);
            SoftwareBitmap bitmap = await LoadImageAsync(storageFile);

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
    }
}