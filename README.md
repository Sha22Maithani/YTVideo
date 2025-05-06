# YShorts - YouTube Video Processing App

A web application for processing YouTube videos, creating shorts, and handling video transcription.

## Features

- YouTube video uploading and processing
- Automatic transcription using AssemblyAI
- Short video generation from longer content
- User-friendly web interface

## Technologies Used

- ASP.NET Core MVC
- C#
- AssemblyAI for transcription
- HTML/CSS/JavaScript

## Getting Started

### Prerequisites

- .NET 7.0 or higher
- Visual Studio 2022 or Visual Studio Code

### Installation

1. Clone the repository
   ```
   git clone https://github.com/Sha22Maithani/YTVideo.git
   ```

2. Open the solution in Visual Studio or Visual Studio Code

3. Restore NuGet packages

4. Run the application
   ```
   dotnet run
   ```

5. Navigate to `https://localhost:5001` in your browser

## Configuration

The application uses AssemblyAI for transcription services. You'll need to set up your API key in `appsettings.json` or use environment variables.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 