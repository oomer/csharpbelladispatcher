using System.Drawing;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using SkiaSharp;
using bella;
using libBella = bella.bella;
using System.Runtime.InteropServices.Marshalling;

namespace oomer_imgui_learn3;

/// <summary>
/// Interface defining the core functionality for Bella renderer integration.
/// Implementations handle events and data received from the rendering engine.
/// </summary>
public interface IBella {
    /// <summary>
    /// Handles rendered image data from a specific render pass.
    /// </summary>
    /// <param name="pass">The name of the render pass</param>
    /// <param name="img">The image data received from the renderer</param>
    void ShowImage(string pass, bella.Image img);
    
    /// <summary>
    /// Handles scene loaded notification from the renderer.
    /// </summary>
    /// <param name="scene">The loaded scene data</param>
    void onSceneLoaded(bella.Scene scene);
    
    /// <summary>
    /// Handles error messages from the renderer.
    /// </summary>
    /// <param name="pass">The render pass where the error occurred</param>
    /// <param name="msg">The error message</param>
    void onError(string pass, string msg);
    
    /// <summary>
    /// Handles progress updates from the renderer.
    /// </summary>
    /// <param name="pass">The render pass currently in progress</param>
    /// <param name="progress">Progress information from the renderer</param>
    void onProgress(string pass, bella.Progress progress);
}

/// <summary>
/// Observer implementation that connects the Bella rendering engine to the application.
/// Acts as a bridge between the engine and the application's GUI.
/// </summary>
public class EngineObserver : bella.EngineObserver {
    private readonly IBella _appInterface;
    
    /// <summary>
    /// Initializes a new instance of the EngineObserver class.
    /// </summary>
    /// <param name="appInterface">The application interface to forward engine events to</param>
    public EngineObserver(IBella appInterface) {
        _appInterface = appInterface ?? throw new ArgumentNullException(nameof(appInterface));
        Console.WriteLine("Engine observer initialized");
    }
    
    /// <summary>
    /// Called by the engine when an image is rendered.
    /// Forwards the image to the application interface.
    /// </summary>
    /// <param name="renderpass">The name of the render pass</param>
    /// <param name="image">The rendered image data</param>
    public override void onImage(string renderpass, bella.Image image) {
        _appInterface.ShowImage(renderpass, image);
    }

    /// <summary>
    /// Called by the engine when a scene is loaded.
    /// Forwards the scene data to the application interface.
    /// </summary>
    /// <param name="scene">The loaded scene data</param>
    public override void onSceneLoaded(bella.Scene scene) {
        _appInterface.onSceneLoaded(scene);
    }
    
    /// <summary>
    /// Called by the engine when an error occurs.
    /// Forwards the error to the application interface.
    /// </summary>
    /// <param name="pass">The render pass where the error occurred</param>
    /// <param name="msg">The error message</param>
    public override void onError(string pass, string msg) {
        _appInterface.onError(pass, msg);
    }
    
    /// <summary>
    /// Called by the engine when progress is made.
    /// Forwards the progress information to the application interface.
    /// </summary>
    /// <param name="pass">The render pass currently in progress</param>
    /// <param name="progress">Progress information from the renderer</param>
    public override void onProgress(string pass, bella.Progress progress) {
        _appInterface.onProgress(pass, progress);
    }
}

/// <summary>
/// Main program entry point and application setup.
/// </summary>
public static class Program {
    // Constants
    private const string USAGE_MESSAGE = "Usage: program <file.bsz> [-p port]\n" +
                                        "A Bella scene file (.bsz, .bsa, or .bsx) is required";
    private const int DEFAULT_PORT = 8799;
    
    /// <summary>
    /// Application entry point. Parses command line arguments and initializes the GUI.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static void Main(string[] args) {
        try {
            // Parse command line arguments
            (string bellaFilePath, int port) = ParseCommandLineArguments(args);
            
            // Initialize and start GUI
            var gui = new GUI(bellaFilePath, port);
            gui.Start();
        }
        catch (ArgumentException ex) {
            Console.WriteLine(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex) {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    /// <summary>
    /// Parses command line arguments to extract Bella file path and port.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A tuple containing the Bella file path and port number</returns>
    /// <exception cref="ArgumentException">Thrown when required arguments are missing or invalid</exception>
    private static (string bellaFilePath, int port) ParseCommandLineArguments(string[] args) {
        if (args.Length == 0) {
            throw new ArgumentException(USAGE_MESSAGE);
        }
        
        string bellaFilePath = "";
        int port = DEFAULT_PORT;
        
        // Process arguments
        for (int i = 0; i < args.Length; i++) {
            string arg = args[i];
            
            // Check for Bella file
            if (IsBellaFile(arg)) {
                bellaFilePath = arg;
                continue;
            }
            
            // Check for port specification
            if ((arg == "-p" || arg == "--port") && i + 1 < args.Length) {
                if (int.TryParse(args[i + 1], out int parsedPort)) {
                    port = parsedPort;
                    i++; // Skip the next argument as we've processed it
                }
                else {
                    throw new ArgumentException($"Invalid port number: {args[i + 1]}");
                }
            }
        }
        
        // Validate we have a Bella file
        if (string.IsNullOrEmpty(bellaFilePath)) {
            throw new ArgumentException(USAGE_MESSAGE);
        }
        
        return (bellaFilePath, port);
    }
    
    /// <summary>
    /// Checks if a file path refers to a Bella scene file.
    /// </summary>
    /// <param name="filePath">The file path to check</param>
    /// <returns>True if the file has a Bella scene extension, false otherwise</returns>
    private static bool IsBellaFile(string filePath) {
        return filePath.EndsWith(".bsz", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".bsa", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".bsx", StringComparison.OrdinalIgnoreCase);
    }
}
