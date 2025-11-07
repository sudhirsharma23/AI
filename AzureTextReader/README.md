# ImageTextExtractor

This is a C# console application that extracts text content from images using the Azure AI Content Understanding API. The app sends image URLs to the API, polls for extraction results, and prints the extracted content to the console.

## Features
- Sends images to Azure Content Understanding for analysis
- Polls the API until extraction is complete
- Supports multiple images in a single run
- Outputs extracted markdown content for each image

## Prerequisites
- .NET 9.0 SDK or later
- An Azure Content Understanding endpoint and subscription key

## Setup
1. Clone this repository or copy the source files to your machine.
2. Open the solution (`imageextract.sln`) in Visual Studio or your preferred IDE.
3. In `Program.cs`, replace the following placeholders with your actual values:
   - `<your-endpoint>`: Your Azure Content Understanding endpoint URL
   - `<your-analyzer-name>`: The analyzer name you have configured in Azure
   - `<your-content-understanding-key>`: Your Azure subscription key
   - `<your-image-url_01>`, `<your-image-url_02>`: URLs of the images you want to extract text from

## Usage

### Build the app
```powershell
# From the root directory
cd src
# Build the project
 dotnet build
```

### Run the app
```powershell
# From the src directory
 dotnet run
```

The app will process each image URL, poll the Azure API until extraction is complete, and print the extracted content to the console.

## Project Structure
- `src/Program.cs` - Main application logic
- `src/ImageTextExtractor.csproj` - Project file
- `src/images/` - Example images (not used directly by the app, but you can upload these to a public location and use their URLs)

## License
This project is licensed under the MIT License.
