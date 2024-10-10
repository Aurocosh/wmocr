using CommandLine;

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
}