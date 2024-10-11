using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using CommandLine;

namespace wmocr
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var result = await Parser.Default.ParseArguments<Options>(args)
              .WithParsedAsync(Execute);
        }

        private static async Task<int> Execute(Options options)
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

            OcrEngine ocrEngine = GetOcrEngine(options.Language);
            if (ocrEngine == null)
            {
                Console.Error.WriteLine("Selected language is not available.");
                return 1;
            }
            if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension)
            {
                Console.Error.WriteLine($"Bitmap dimensions ({bitmap.PixelWidth}x{bitmap.PixelHeight}) are too big for OCR. Max image dimension is {OcrEngine.MaxImageDimension}.");
                return 1;
            }

            var orcResult = await ocrEngine.RecognizeAsync(bitmap);
            await OutputHandler.OutputResult(options, bitmap, decoder.DecoderInformation.CodecId, orcResult);
            return 0;
        }

        private static OcrEngine GetOcrEngine(string? language)
        {
            if (language == null)
                return OcrEngine.TryCreateFromUserProfileLanguages();

            var lang = new Language(language);
            return OcrEngine.TryCreateFromLanguage(lang);
        }
    }
}