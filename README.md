# wmocr
## Overview
Command line interface wrapper for Windows.Media.Ocr. Performs image optical character recognition through command line. This tool relies on Windows language packs for OCR.

## Usage examples

- Perform OCR and print result to the console
wmocr -i test.jpg

- Perform OCR and save result to the text file
wmocr -i test.jpg -o out.txt

- Perform OCR and append result to the text file as one line
wmocr -i test.jpg -o out.txt --append --one-line

- Crop out recognised text from the image and save it to a separate file
wmocr -i test.jpg -c cropped.jpg

- Show list of all supported languages
wmocr --lang-list

- Show help
wmocr --help  