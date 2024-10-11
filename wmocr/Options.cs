using CommandLine;

namespace wmocr
{
    public class Options
    {
        [Option('i', "input", Required = false, HelpText = "Input image file path.")]
        public string? Input { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output text file path to save OCR result.")]
        public string? Output { get; set; }

        [Option('s', "stdout", Required = false, HelpText = "If specifed then the OCR result will be printed to stdout (Console). Default behaviour if no other output argument is specified.")]
        public bool Stdout { get; set; }

        [Option('c', "crop", Required = false, HelpText = "Cropped image file path. If specified then the original image will be cropped by the bounding box of the detected text and saved to file.")]
        public string? Crop { get; set; }

        [Option('b', "bb-margin", Required = false, Default = 10, HelpText = "Cropping bounding box margin.The bounding box will be expanded by this value before cropping.")]
        public int BoundingBoxMargin { get; set; }

        [Option('a', "append", HelpText = "When writing to text file append instead of overwriting the file.")]
        public bool Append { get; set; }

        [Option('l', "lang", HelpText = "Selected language for OCR.")]
        public string? Language { get; set; }

        [Option('x', "lang-list", HelpText = "Print the list of all available languages for OCR.")]
        public bool LanguageList { get; set; }

        [Option('n', "one-line", HelpText = "Return OCR result as a single line.")]
        public bool OneLine { get; set; }
    }
}