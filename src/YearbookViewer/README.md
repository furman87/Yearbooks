# Furman Yearbook Web Viewer

A modern, responsive web application for browsing Furman University yearbooks (1980-1994) with advanced photo viewing capabilities.

## ?? Features

- **?? Responsive Design**: Works perfectly on desktop, tablet, and mobile devices
- **?? Photo Gallery**: High-quality image viewing with PhotoSwipe integration
- **? Dynamic Image Processing**: On-the-fly image resizing and optimization
- **?? Intuitive Navigation**: Browse by year with keyboard shortcuts
- **??? Lightbox Viewer**: Full-screen photo viewing with zoom, pan, and navigation
- **?? Docker Support**: Easy containerized deployment
- **?? Performance Optimized**: Lazy loading, caching, and image compression

## ??? Architecture

- **Frontend**: ASP.NET Core Razor Pages with Bootstrap 5
- **Backend**: RESTful API controllers
- **Image Processing**: SixLabors.ImageSharp for dynamic resizing
- **Photo Viewer**: PhotoSwipe 5 for advanced image viewing
- **Containerization**: Docker and Docker Compose support

## ?? Quick Start

### Prerequisites

- **.NET 10 SDK** or later
- **Downloaded yearbook folders** with `full`, `thumbs`, and optional `text` directories

### Local Development

1. **Navigate to the web viewer directory:**
   ```bash
   cd YearbookViewer
   ```

2. **Restore packages:**
   ```bash
   dotnet restore
   ```

3. **Run the application:**
   ```bash
   dotnet run
   ```

4. **Open your browser:**
   - Navigate to `https://localhost:5011` or `http://localhost:5010`
   - The application will automatically detect yearbook directories

### Docker Deployment

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up --build
   ```

2. **Access the application:**
   - Open `http://localhost:8080`

## ?? Configuration

### appsettings.json

```json
{
  "YearbookPath": "..",  // Path to directory containing Bonhomie-* folders
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Directory Structure Expected

```
/
??? Bonhomie-1980/
?   ??? bonhomie_1980_001.jpg
?   ??? bonhomie_1980_002.jpg
?   ??? ...
??? Bonhomie-1981/
?   ??? bonhomie_1981_001.jpg
?   ??? ...
??? YearbookViewer/
```

## ?? Usage

### Main Gallery
- **Browse Years**: Click on year badges to jump to specific yearbooks
- **View Yearbooks**: Click "Browse" or "View Yearbook" buttons
- **Statistics**: See total yearbooks and pages available

### Photo Viewer
- **Navigation**: Use arrow keys or click navigation buttons
- **Zoom**: Mouse wheel, double-click, or pinch to zoom
- **Fullscreen**: Click fullscreen button or press F11
- **Download**: Click download button to save images
- **Close**: Press Escape or click close button

### Keyboard Shortcuts
- **Left/Right Arrows**: Navigate between years
- **Escape**: Return to main gallery
- **Space/Enter**: Open photo viewer
- **F11**: Toggle fullscreen in photo viewer

## ?? API Endpoints

### GET /api/yearbook
Returns all available yearbooks with metadata.

### GET /api/yearbook/{year}
Returns specific yearbook information.

### GET /api/yearbook/{year}/image/{fileName}
Serves yearbook images with optional resizing.

**Query Parameters:**
- `w`: Target width
- `h`: Target height  
- `quality`: JPEG quality (1-100, default: 85)

**Example:**
```
/api/yearbook/1984/image/bonhomie_1984_001.jpg?w=800&h=1200&quality=90
```

### GET /health
Health check endpoint for monitoring.

## ?? Docker

### Dockerfile
- Based on official .NET 10 runtime
- Includes image processing libraries
- Runs as non-root user
- Optimized for production

### Docker Compose
- Automated container orchestration
- Volume mapping for yearbook data
- Health checks and restart policies
- Environment configuration

## ?? Development

### Project Structure
```
YearbookViewer/
??? Controllers/           # API controllers
??? Models/               # Data models
??? Pages/                # Razor Pages
??? Services/             # Business logic
??? wwwroot/              # Static assets
??? Dockerfile            # Container definition
??? appsettings.json      # Configuration
```

### Key Components

- **YearbookService**: Scans and manages yearbook data
- **YearbookController**: API for yearbook and image serving
- **IndexModel**: Main gallery page logic
- **BrowseModel**: Individual yearbook viewing

## ?? Customization

### CSS Styling
Modify `wwwroot/css/site.css` for custom styling:
- **Colors**: Update CSS variables
- **Layout**: Modify grid layouts and spacing  
- **Animations**: Customize hover effects and transitions

### Photo Viewer
Configure PhotoSwipe options in `Browse.cshtml`:
- **UI Elements**: Customize toolbar buttons
- **Gestures**: Modify touch and mouse interactions
- **Animation**: Adjust transition effects

## ?? Performance

### Optimizations
- **Lazy Loading**: Images load as needed
- **Dynamic Resizing**: Images served at optimal sizes
- **Caching Headers**: Browser and CDN caching
- **Compressed Images**: JPEG optimization

### Monitoring
- Health check endpoint at `/health`
- Structured logging with Serilog
- Performance counters available

## ??? Troubleshooting

### Common Issues

**No yearbooks found:**
- Check `YearbookPath` configuration
- Ensure Bonhomie-* directories exist
- Verify image files are present

**Images not loading:**
- Check file permissions
- Verify image file formats (JPG/JPEG)
- Check browser console for errors

**PhotoSwipe not working:**
- Ensure JavaScript is enabled
- Check browser compatibility
- Verify CDN resources are accessible

### Logs
Check application logs for detailed error information:
```bash
dotnet run --logging:loglevel:default=Debug
```

## ?? License

This project is part of the Furman University Digital Archives initiative.

## ?? Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

---

**Built with ?? for Furman University**
