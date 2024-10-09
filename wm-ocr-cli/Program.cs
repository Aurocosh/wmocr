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

namespace wm_ocr_cli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string imagePath = "D:\\Temp\\test.jpg";

            try
            {
                var result = RecognizeAsync(imagePath, "ru").GetAwaiter().GetResult();

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
            }
            Console.ReadLine();
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